using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace QQImageSwitch
{
    public class InfoField {public string Name,Value;public InfoField(string name,string value){Name=name;Value=value??"";}}
    public class SamplerInfo {public string Name;public readonly List<InfoField> Fields=new List<InfoField>();}
    public class InspectionResult
    {
        public readonly List<InfoField> WebUI=new List<InfoField>();
        public readonly List<SamplerInfo> Comfy=new List<SamplerInfo>();
        public readonly List<string> Notes=new List<string>();
        public string PromptJson,WorkflowJson,Parameters;
    }
    public static class Inspection
    {
        const int TextLimit=16*1024*1024;
        static readonly Encoding Latin=Encoding.GetEncoding(28591);
        public static JavaScriptSerializer Json(){return new JavaScriptSerializer {MaxJsonLength=TextLimit,RecursionLimit=128};}
        static Dictionary<string,object> Dict(object value){return value as Dictionary<string,object>;}
        static object[] ArrayOf(object value){return value as object[]??new object[0];}
        static object Get(Dictionary<string,object> data,string key){object result;return data!=null&&data.TryGetValue(key,out result)?result:null;}
        static string Str(object value){return value==null?"":Convert.ToString(value,CultureInfo.InvariantCulture);}
        static string Display(object value){return value is object[]||value is Dictionary<string,object>?Json().Serialize(value):Str(value);}
        public static InspectionResult Read(string path)
        {
            var result=new InspectionResult();var metadata=ReadText(ImageTools.Read(path),result.Notes);
            string text;
            if(metadata.TryGetValue("parameters",out text)){result.Parameters=text;result.WebUI.AddRange(ParseWebUI(text));}
            if(metadata.TryGetValue("prompt",out text))result.PromptJson=text;
            if(metadata.TryGetValue("workflow",out text))result.WorkflowJson=text;
            if(result.PromptJson!=null)
            {
                try
                {
                    var graph=Dict(Json().DeserializeObject(result.PromptJson));
                    // Workflow output names disambiguate custom multi-output nodes. Executed inputs win.
                    if(result.WorkflowJson!=null)try{EnrichGraph(graph,WorkflowGraph(Dict(Json().DeserializeObject(result.WorkflowJson))));}catch(Exception ex){result.Notes.Add("工作流补充信息无法解析："+ex.Message);}
                    result.Comfy.AddRange(ParseGraph(graph));
                }
                catch(Exception ex){result.Notes.Add("prompt 节点信息无法解析："+ex.Message);}
            }
            if(result.Comfy.Count==0&&result.WorkflowJson!=null)
            {
                try{result.Comfy.AddRange(ParseGraph(WorkflowGraph(Dict(Json().DeserializeObject(result.WorkflowJson)))));result.Notes.Add("ComfyUI 信息来自界面工作流，部分自定义节点的控件值无法确定含义。优先读取执行 prompt 快照。");}
                catch(Exception ex){result.Notes.Add("workflow 信息无法解析："+ex.Message);}
            }
            if(result.Comfy.Count==0&&(result.PromptJson!=null||result.WorkflowJson!=null))result.Notes.Add("发现 ComfyUI 信息，但没有可识别的采样节点；可复制原始 prompt / workflow 查看。");
            return result;
        }
        public static List<InfoField> ParseWebUI(string text)
        {
            var fields=new List<InfoField>();text=(text??"").Replace("\r\n","\n").TrimEnd('\0');
            var lines=text.Split('\n');int parameterLine=-1;
            for(int i=lines.Length-1;i>=0;i--)if(Regex.IsMatch(lines[i],@"^(?:Steps|Sampler|CFG scale|Seed|Size):\s")&&Regex.Matches(lines[i],@"(?:^|,\s*)[\w .\-/]+:").Count>=2){parameterLine=i;break;}
            string prompts=string.Join("\n",parameterLine<0?lines:lines.Take(parameterLine));
            var negative=Regex.Match(prompts,@"(?:^|\n)Negative prompt:\s*");
            fields.Add(new InfoField("正面 tag",negative.Success?prompts.Substring(0,negative.Index).TrimEnd():prompts));
            fields.Add(new InfoField("负面 tag",negative.Success?prompts.Substring(negative.Index+negative.Length):""));
            if(parameterLine>=0)
            {
                string parameters=string.Join("\n",lines.Skip(parameterLine));
                foreach(string part in SplitParameters(parameters))
                {
                    int split=part.IndexOf(':');if(split<1)continue;string name=part.Substring(0,split).Trim(),value=part.Substring(split+1).Trim();
                    if(value.Length>=2&&value[0]=='"'&&value[value.Length-1]=='"')try{value=Str(Json().DeserializeObject(value));}catch(ArgumentException){}
                    fields.Add(new InfoField(ParameterName(name),value));
                }
            }
            fields.Add(new InfoField("完整 WebUI 参数",text));return fields;
        }
        static IEnumerable<string> SplitParameters(string text)
        {
            int start=0,depth=0;bool quoted=false,escaped=false;
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];if(escaped){escaped=false;continue;}if(quoted&&c=='\\'){escaped=true;continue;}if(c=='"'){quoted=!quoted;continue;}
                if(quoted)continue;if(c=='{'||c=='['||c=='(')depth++;else if(c=='}'||c==']'||c==')')depth=Math.Max(0,depth-1);
                if(c==','&&depth==0&&Regex.IsMatch(text.Substring(i+1),@"^\s*[\w .\-/]+:\s*")){yield return text.Substring(start,i-start).Trim();start=i+1;}
            }
            yield return text.Substring(start).Trim();
        }
        static string ParameterName(string name)
        {
            switch(name){case "Steps":case "steps":return "步数 Steps";case "CFG scale":case "cfg":case "guidance":return "CFG / Guidance";case "Seed":case "seed":case "noise_seed":return "种子 Seed";case "Sampler":case "sampler_name":return "采样器 Sampler";case "scheduler":case "Schedule type":return "调度器 Scheduler";case "Model":case "ckpt_name":return "模型 Model";case "Size":return "尺寸 Size";case "denoise":case "Denoising strength":return "降噪 Denoise";default:return name;}
        }
        static byte[] Inflate(byte[] bytes,int offset,int count)
        {
            if(count<6||(bytes[offset]&15)!=8||((bytes[offset]<<8|bytes[offset+1])%31)!=0||(bytes[offset+1]&32)!=0)throw new InvalidDataException("压缩文本头无效。");
            using(var input=new MemoryStream(bytes,offset+2,count-6))using(var stream=new DeflateStream(input,CompressionMode.Decompress))using(var output=new MemoryStream())
            {
                var buffer=new byte[8192];int n;uint a=1,b=0;
                while((n=stream.Read(buffer,0,buffer.Length))>0){if(output.Length+n>TextLimit)throw new InvalidDataException("生成信息超过 16 MB。");output.Write(buffer,0,n);for(int i=0;i<n;i++){a=(a+buffer[i])%65521;b=(b+a)%65521;}}
                if((b<<16|a)!=Codec.Read32(bytes,offset+count-4))throw new InvalidDataException("压缩文本校验失败。");return output.ToArray();
            }
        }
        static int Zero(byte[] data,int start){int i=System.Array.IndexOf(data,(byte)0,start);if(i<0)throw new InvalidDataException("文本字段不完整。");return i;}
        static Dictionary<string,string> ReadText(byte[] bytes,List<string> notes)
        {
            var meta=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            Action<string,string> add=(key,value)=>{if(value.Length>TextLimit)throw new InvalidDataException("生成信息过大。");meta[key]=value.TrimEnd('\0');};
            if(bytes.Length>8&&bytes[0]==137)
            {
                foreach(var chunk in Codec.Parse(bytes,false))try
                {
                    var d=chunk.Data;string key;int z,p;
                    switch(chunk.Type)
                    {
                        case "tEXt":case "comf":z=Zero(d,0);add(Latin.GetString(d,0,z),Latin.GetString(d,z+1,d.Length-z-1));break;
                        case "zTXt":z=Zero(d,0);key=Latin.GetString(d,0,z);if(z+2>d.Length||d[z+1]!=0)throw new InvalidDataException("文本压缩方式无效。");add(key,Latin.GetString(Inflate(d,z+2,d.Length-z-2)));break;
                        case "iTXt":z=Zero(d,0);key=Latin.GetString(d,0,z);if(z+3>d.Length||d[z+2]!=0||d[z+1]>1)throw new InvalidDataException("国际文本头无效。");p=Zero(d,z+3)+1;p=Zero(d,p)+1;byte[] value=d[z+1]==1?Inflate(d,p,d.Length-p):d.Skip(p).ToArray();add(key,Encoding.UTF8.GetString(value));break;
                        case "eXIf":Exif(d,add);break;
                    }
                }
                catch(Exception ex){notes.Add(chunk.Type+" 读取失败："+ex.Message);}
            }
            else if(bytes.Length>12&&Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&Encoding.ASCII.GetString(bytes,8,4)=="WEBP")
            {
                long end=8L+BitConverter.ToUInt32(bytes,4);if(end>bytes.Length)throw new InvalidDataException("WebP 数据不完整。");
                for(int p=12;p<end;)
                {if(p+8>end)throw new InvalidDataException("WebP 块不完整。");uint n=BitConverter.ToUInt32(bytes,p+4);if(p+8L+n+(n&1)>end)throw new InvalidDataException("WebP 块长度无效。");if(Encoding.ASCII.GetString(bytes,p,4)=="EXIF")Exif(bytes.Skip(p+8).Take((int)n).ToArray(),add);p+=(int)(8+n+(n&1));}
            }
            else if(bytes.Length>4&&bytes[0]==255&&bytes[1]==216)
            {
                int p=2;while(p<bytes.Length)
                {
                    if(bytes[p++]!=255)throw new InvalidDataException("JPEG 标记无效。");while(p<bytes.Length&&bytes[p]==255)p++;if(p>=bytes.Length)break;int marker=bytes[p++];if(marker==0xda||marker==0xd9)break;if(marker==1||(marker>=0xd0&&marker<=0xd7))continue;
                    if(p+2>bytes.Length)break;int n=bytes[p]*256+bytes[p+1];if(n<2||p+n>bytes.Length)throw new InvalidDataException("JPEG 块长度无效。");if(marker==0xe1&&n>=8&&Encoding.ASCII.GetString(bytes,p+2,6)=="Exif\0\0")Exif(bytes.Skip(p+2).Take(n-2).ToArray(),add);p+=n;
                }
            }
            else throw new InvalidDataException("参数读取支持 PNG / APNG、JPEG、WebP。");return meta;
        }
        static void Exif(byte[] bytes,Action<string,string> add)
        {
            int origin=bytes.Length>=6&&Encoding.ASCII.GetString(bytes,0,6)=="Exif\0\0"?6:0;if(origin+8>bytes.Length)return;
            bool little=bytes[origin]==73&&bytes[origin+1]==73;if(!little&&!(bytes[origin]==77&&bytes[origin+1]==77))return;
            Func<int,int> shortAt=p=>little?bytes[p]|bytes[p+1]<<8:bytes[p]<<8|bytes[p+1];Func<int,uint> longAt=p=>little?BitConverter.ToUInt32(bytes,p):Codec.Read32(bytes,p);
            var visited=new HashSet<uint>();Action<uint> directory=null;
            directory=relative=>
            {
                if(!visited.Add(relative)||visited.Count>16||relative>bytes.Length-origin-2)return;int start=origin+(int)relative,count=shortAt(start);
                for(int i=0;i<count;i++)
                {
                    int p=start+2+i*12;if(p+12>bytes.Length)return;int tag=shortAt(p),type=shortAt(p+2);uint n=longAt(p+4);
                    if(tag==0x8769&&type==4&&n==1){directory(longAt(p+8));continue;}
                    if(type!=2&&type!=7)continue;if(n>TextLimit)continue;uint offset=n<=4?(uint)(p+8-origin):longAt(p+8);if((long)offset+n>bytes.Length-origin)continue;
                    byte[] data=bytes.Skip(origin+(int)offset).Take((int)n).ToArray();string value;
                    if(tag==0x9286&&data.Length>=8)
                    {
                        string code=Encoding.ASCII.GetString(data,0,8);var body=data.Skip(8).ToArray();
                        if(code.StartsWith("UNICODE")){bool le=body.Length>=2&&body[0]==255&&body[1]==254;int skip=body.Length>=2&&((body[0]==255&&body[1]==254)||(body[0]==254&&body[1]==255))?2:0;value=(le?Encoding.Unicode:Encoding.BigEndianUnicode).GetString(body,skip,body.Length-skip).TrimEnd('\0');}
                        else value=Encoding.UTF8.GetString(body).TrimEnd('\0');add("parameters",value);continue;
                    }
                    value=Encoding.UTF8.GetString(data).TrimEnd('\0');
                    if(value.StartsWith("prompt:"))add("prompt",value.Substring(7));else if(value.StartsWith("workflow:"))add("workflow",value.Substring(9));else if(tag==0x10e&&value.Contains("Steps:"))add("parameters",value);
                }
            };directory(longAt(origin+4));
        }
        static string Link(object value,Dictionary<string,Dictionary<string,object>> graph)
        {var a=ArrayOf(value);return a.Length==2&&graph.ContainsKey(Str(a[0]))?Str(a[0]):null;}
        static List<string> Upstream(string id,Dictionary<string,Dictionary<string,object>> graph)
        {
            var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(id);
            while(queue.Count>0&&seen.Count<10000){string current=queue.Dequeue();if(!seen.Add(current))continue;var inputs=Dict(Get(graph[current],"inputs"));if(inputs!=null)foreach(var value in inputs.Values){string link=Link(value,graph);if(link!=null&&!seen.Contains(link))queue.Enqueue(link);}}
            return seen.ToList();
        }
        public static List<SamplerInfo> ParseGraph(Dictionary<string,object> data)
        {
            if(data==null)return new List<SamplerInfo>();if(Dict(Get(data,"prompt"))!=null)data=Dict(Get(data,"prompt"));
            var graph=new Dictionary<string,Dictionary<string,object>>();foreach(var pair in data)if(Dict(pair.Value)!=null&&Get(Dict(pair.Value),"class_type")!=null)graph[pair.Key]=Dict(pair.Value);
            if(graph.Count>10000)throw new InvalidDataException("节点超过 10000 个。");var result=new List<SamplerInfo>();
            var roots=graph.Keys.Where(id=>IsSampler(graph[id])).ToList();
            foreach(string root in roots.Take(200))
            {
                var info=new SamplerInfo {Name="#"+root+" · "+Str(Get(graph[root],"class_type"))};var inputs=Dict(Get(graph[root],"inputs"));var upstream=Upstream(root,graph);
                object positive=ComfyPromptReader.RoleInput(inputs,false)??Get(inputs,"conditioning")??Get(inputs,"prompt")??Get(inputs,"text"),negative=ComfyPromptReader.RoleInput(inputs,true);
                object guider=Get(inputs,"guider");
                if(guider!=null){positive=positive??guider;negative=negative??guider;}
                object pipeline=inputs.Where(p=>ComfyPromptReader.PipelineKey(p.Key)).Select(p=>p.Value).FirstOrDefault();
                positive=positive??pipeline;negative=negative??pipeline;
                var reader=new ComfyPromptReader(graph);
                info.Fields.Add(new InfoField("正面 tag",reader.Read(positive,false)));info.Fields.Add(new InfoField("负面 tag",reader.Read(negative,true)));
                if(reader.Notes.Count>0)info.Fields.Add(new InfoField("提示词读取说明",string.Join("\n",reader.Notes)));
                foreach(string id in upstream)
                {
                    var node=graph[id];var ni=Dict(Get(node,"inputs"));if(ni==null)continue;
                    foreach(var input in ni)
                    {
                        if(Link(input.Value,graph)!=null||input.Key=="positive"||input.Key=="negative")continue;
                        string label=ParameterName(input.Key)+"  [#"+id+" "+Str(Get(node,"class_type"))+"]";
                        info.Fields.Add(new InfoField(label,Display(input.Value)));
                        if(info.Fields.Count>=500)break;
                    }
                    if(info.Fields.Count>=500){info.Fields.Add(new InfoField("更多节点信息","字段超过 500 项，请查看完整 prompt / workflow。"));break;}
                }
                result.Add(info);
            }
            return result;
        }
        static bool IsSampler(Dictionary<string,object> node)
        {
            var inputs=Dict(Get(node,"inputs"));if(inputs==null)return false;
            bool branch=inputs.Keys.Any(k=>ComfyPromptReader.Polarity(k)!=0||ComfyPromptReader.PipelineKey(k)||k=="guider"||k=="latent_image"||k=="conditioning"||k=="prompt"||k=="text");
            return branch&&(Str(Get(node,"class_type")).IndexOf("Sampler",StringComparison.OrdinalIgnoreCase)>=0||(Get(inputs,"steps")!=null&&Get(inputs,"cfg")!=null&&(Get(inputs,"seed")!=null||Get(inputs,"noise_seed")!=null)));
        }
        static void EnrichGraph(Dictionary<string,object> executed,Dictionary<string,object> workflow)
        {
            if(Dict(Get(executed,"prompt"))!=null)executed=Dict(Get(executed,"prompt"));if(executed==null)return;
            foreach(var pair in executed)
            {
                var node=Dict(pair.Value);var saved=Dict(Get(workflow,pair.Key));if(node==null||saved==null||Str(Get(node,"class_type"))!=Str(Get(saved,"class_type")))continue;
                node["_output_names"]=Get(saved,"_output_names");
                var inputs=Dict(Get(node,"inputs"));var extra=Dict(Get(saved,"inputs"));if(inputs==null||extra==null)continue;
                foreach(var field in extra)if(!inputs.ContainsKey(field.Key)&&!(field.Value is object[]))inputs[field.Key]=field.Value;
            }
        }
        static Dictionary<string,object> WorkflowGraph(Dictionary<string,object> workflow)
        {
            var graph=new Dictionary<string,object>();var links=new Dictionary<string,object[]>();
            foreach(var value in ArrayOf(Get(workflow,"links")))
            {
                var a=ArrayOf(value);var named=Dict(value);
                if(named!=null)a=new[]{Get(named,"id"),Get(named,"origin_id"),Get(named,"origin_slot"),Get(named,"target_id"),Get(named,"target_slot")};
                if(a.Length>=4)links[Str(a[0])]=a;
            }
            foreach(var value in ArrayOf(Get(workflow,"nodes")))
            {
                var node=Dict(value);if(node==null)continue;string id=Str(Get(node,"id")),kind=Str(Get(node,"type"));var inputs=new Dictionary<string,object>();
                foreach(var slot in ArrayOf(Get(node,"inputs")))
                {var s=Dict(slot);object[] link;if(s!=null&&links.TryGetValue(Str(Get(s,"link")),out link))inputs[Str(Get(s,"name"))]=new object[]{link[1],link[2]};}
                var widgets=ArrayOf(Get(node,"widgets_values"));var namedWidgets=Dict(Get(node,"widgets_values"));string[] names=null;
                var outputs=ArrayOf(Get(node,"outputs")).Select(o=>Str(Get(Dict(o),"name"))).ToArray();
                if(namedWidgets!=null)foreach(var field in namedWidgets)if(!inputs.ContainsKey(field.Key))inputs[field.Key]=field.Value;
                switch(kind)
                {
                    case "KSampler":names=widgets.Length>1&&widgets[1] is string?new[]{"seed","control_after_generate","steps","cfg","sampler_name","scheduler","denoise"}:new[]{"seed","steps","cfg","sampler_name","scheduler","denoise"};break;
                    case "CLIPTextEncode":names=new[]{"text"};break;case "CLIPTextEncodeSDXL":names=new[]{"width","height","crop_w","crop_h","target_width","target_height","text_g","text_l"};break;
                    case "CLIPTextEncodeFlux":names=new[]{"clip_l","t5xxl","guidance"};break;case "FluxGuidance":names=new[]{"guidance"};break;
                    case "CLIPTextEncodeSD3":names=new[]{"clip_l","clip_g","t5xxl","empty_padding"};break;
                    case "CheckpointLoaderSimple":names=new[]{"ckpt_name"};break;case "VAELoader":names=new[]{"vae_name"};break;case "UNETLoader":names=new[]{"unet_name","weight_dtype"};break;
                    case "LoraLoader":names=new[]{"lora_name","strength_model","strength_clip"};break;case "EmptyLatentImage":names=new[]{"width","height","batch_size"};break;
                    case "CFGGuider":names=new[]{"cfg"};break;case "BasicScheduler":names=new[]{"scheduler","steps","denoise"};break;case "KSamplerSelect":names=new[]{"sampler_name"};break;case "RandomNoise":names=new[]{"noise_seed","control_after_generate"};break;
                    case "KSamplerAdvanced":names=new[]{"add_noise","noise_seed","control_after_generate","steps","cfg","sampler_name","scheduler","start_at_step","end_at_step","return_with_leftover_noise"};break;
                }
                if(names!=null){for(int i=0;i<Math.Min(names.Length,widgets.Length);i++)if(!inputs.ContainsKey(names[i]))inputs[names[i]]=widgets[i];}
                else if(widgets.Length>0)
                {
                    // A single STRING widget has no ambiguous widget order. Named widget dictionaries are preferred.
                    if(widgets.Length==1&&widgets[0] is string&&ArrayOf(Get(node,"outputs")).Any(o=>Str(Get(Dict(o),"type"))=="STRING"))inputs["text"]=widgets[0];
                    inputs["原始 widgets_values（自定义节点）"]=widgets;
                }
                graph[id]=new Dictionary<string,object>{{"class_type",kind},{"inputs",inputs},{"_output_names",outputs}};
            }
            return graph;
        }
    }
}
