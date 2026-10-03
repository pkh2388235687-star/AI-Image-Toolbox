using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace QQImageSwitch
{
    // Traverse only conditioning/text/pipeline branches. Metadata is data, never executed.
    sealed class ComfyPromptReader
    {
        readonly Dictionary<string,Dictionary<string,object>> graph;
        readonly HashSet<string> path=new HashSet<string>();
        int visited;
        public readonly List<string> Notes=new List<string>();
        internal ComfyPromptReader(Dictionary<string,Dictionary<string,object>> nodes){graph=nodes;}
        static object Get(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value:null;}
        static string Str(object value){return value==null?"":Convert.ToString(value,CultureInfo.InvariantCulture);}
        static object[] ArrayOf(object value){return value as object[]??new object[0];}
        internal static string Key(string name){return Regex.Replace((name??"").ToLowerInvariant(),@"[\s_\-]+","");}
        internal static int Polarity(string name)
        {
            string key=Key(name);
            if(key=="conditioning"&&(name??"").TrimEnd().EndsWith("-"))return -1;
            if(key.Contains("negative")||key.Contains("negprompt")||key=="neg"||key.StartsWith("negcond")||key.Contains("负面")||key.Contains("负向"))return -1;
            if(key.Contains("positive")||key.Contains("posprompt")||key=="pos"||key.StartsWith("poscond")||key.Contains("正面")||key.Contains("正向")||key=="conditioning+")return 1;
            return 0;
        }
        internal static bool RoleKey(string name,bool negative)
        {
            int p=Polarity(name);string key=Key(name);
            return p==(negative?-1:1)&&!(key.Contains("strength")||key.Contains("weight")||key.Contains("normalization")||key.Contains("interpretation")||key.Contains("score")||key.Contains("balance")||key.Contains("style"));
        }
        internal static object RoleInput(Dictionary<string,object> inputs,bool negative)
        {
            if(inputs==null)return null;
            string exact=negative?"negative":"positive";object value=Get(inputs,exact);if(value!=null)return value;
            foreach(var pair in inputs)if(RoleKey(pair.Key,negative)&&pair.Value!=null)return pair.Value;
            return null;
        }
        internal static bool PipelineKey(string name){string key=Key(name);return key.Contains("pipe")||key.Contains("context")||key.Contains("bundle")||key=="sdxltuple";}
        internal static bool TextKey(string name)
        {
            string key=Key(name);
            return key=="clipl"||key=="clipg"||key=="t5xxl"||key.Contains("text")||key.Contains("prompt")||key=="string"||key.StartsWith("string")||key=="content"||key=="caption"||key.Contains("提示词")||key.Contains("文本");
        }
        static bool Excluded(string name)
        {
            string key=Key(name);
            return key=="model"||key=="clip"||key=="vae"||key.Contains("image")||key=="pixels"||key.Contains("latent")||key.Contains("mask")||key.Contains("controlnet")||key.Contains("filename")||key.Contains("path")||key.Contains("ckpt")||key.Contains("loraname")||key.Contains("modelname")||key=="seed"||key=="noiseseed"||key=="mode"||key=="action"||key=="operation"||key.Contains("sampler")||key.Contains("scheduler")||key.Contains("weight")||key.Contains("strength")||key.Contains("normalization")||key.Contains("interpretation")||key.Contains("style")||key=="separator"||key=="delimiter"||key=="deliminator"||key=="joinwith"||key=="format"||key=="outputprefix";
        }
        static bool ParameterValue(string text){return Regex.IsMatch(text??"",@"^(?:enable|disable|enabled|disabled|fixed|populate|reproduce|randomize|increment|decrement|normal|default|none|cpu|cuda|auto)$",RegexOptions.IgnoreCase)||Regex.IsMatch(text??"",@"(?:\.(?:safetensors|ckpt|gguf|pth|pt)|[\\/])$",RegexOptions.IgnoreCase);}
        string Link(object value){var a=ArrayOf(value);return a.Length==2&&graph.ContainsKey(Str(a[0]))?Str(a[0]):null;}
        void Note(string text){if(!Notes.Contains(text))Notes.Add(text);}
        internal string Read(object connection,bool negative){visited=0;path.Clear();return Walk(connection,negative,false,0);}
        int OutputRole(Dictionary<string,object> node,int slot)
        {
            var names=ArrayOf(Get(node,"_output_names"));
            if(slot>=0&&slot<names.Length)
            {
                string name=Str(names[slot]);int p=Polarity(name);if(p!=0)return p;
                if(name.EndsWith("+")&&Key(name).Contains("conditioning"))return 1;
                if(name.EndsWith("-")&&Key(name).Contains("conditioning"))return -1;
            }
            string kind=Str(Get(node,"class_type"));
            if(kind.StartsWith("ControlNetApply",StringComparison.Ordinal)&&(slot==0||slot==1))return slot==0?1:-1;
            if(kind.StartsWith("Efficient Loader",StringComparison.Ordinal)&&(slot==1||slot==2))return slot==1?1:-1;
            if(kind.StartsWith("easy ",StringComparison.Ordinal)&&kind.IndexOf("Loader",StringComparison.OrdinalIgnoreCase)>=0&&(slot==4||slot==5))return slot==4?1:-1;
            return 0;
        }
        string Walk(object value,bool negative,bool textContext,int depth)
        {
            if(value is string)return (string)value;
            if(depth>128||++visited>10000){Note("提示词连接过深或过多，已保留原始工作流。");return "";}
            var inline=value as Dictionary<string,object>;
            if(inline!=null){object selected=RoleInput(inline,negative);return selected==null?"":Walk(selected,negative,textContext,depth+1);}
            string id=Link(value);if(id==null)return "";
            var node=graph[id];var inputs=Get(node,"inputs") as Dictionary<string,object>;if(inputs==null)return "";
            int slot;int.TryParse(Str(ArrayOf(value)[1]),out slot);int role=OutputRole(node,slot);if(role!=0)negative=role<0;
            string token=id+":"+slot+":"+negative+":"+textContext;if(!path.Add(token))return "";
            try
            {
                string kind=Str(Get(node,"class_type"));
                if(kind=="ConditioningZeroOut")return "[ConditioningZeroOut：该分支置零]";
                // BasicGuider has one positive conditioning, with no negative prompt.
                if(negative&&kind.IndexOf("Guider",StringComparison.OrdinalIgnoreCase)>=0&&Get(inputs,"conditioning")!=null&&RoleInput(inputs,true)==null)return "";
                // Loader/pipe nodes can contain both polarities. Never mix their text.
                var selected=inputs.Where(p=>RoleKey(p.Key,negative)&&p.Value!=null).ToList();
                if(selected.Count>0)return Combine(selected.Select(p=>Walk(p.Value,negative,true,depth+1)),"\n",true);
                foreach(string name in new[]{"populated_text","processed_text","resolved_text","output_text","result_text","evaluated_text"})
                {
                    object snapshot=Get(inputs,name);if(snapshot!=null){string actual=Walk(snapshot,negative,true,depth+1);if(actual.Length>0)return actual;}
                }
                var textFields=inputs.Where(p=>!Excluded(p.Key)&&!PipelineKey(p.Key)&&Polarity(p.Key)==0&&TextKey(p.Key)&&((p.Value is string)||Link(p.Value)!=null)).ToList();
                if(textFields.Count==0)
                {
                    var literals=inputs.Where(p=>!Excluded(p.Key)&&Polarity(p.Key)==0&&p.Value is string&&!ParameterValue((string)p.Value)).ToList();
                    bool textNode=Regex.IsMatch(kind,@"text|string|prompt|wildcard|encoder|encode|primitive|文本|提示",RegexOptions.IgnoreCase);
                    if(textContext||textNode||literals.Count==1)
                    {
                        textFields=literals;
                        foreach(var pair in inputs)if(!Excluded(pair.Key)&&Polarity(pair.Key)==0&&Link(pair.Value)!=null&&!PipelineKey(pair.Key)&&!textFields.Any(p=>p.Key==pair.Key))textFields.Add(pair);
                        if(textFields.Count>0&&!textNode)Note("自定义节点 #"+id+"（"+kind+"）的文本按提示词连接和输入字段识别；未执行该节点代码。");
                    }
                }
                if(textFields.Count>0)
                {
                    bool join=Regex.IsMatch(kind,@"concat|join|拼接|连接|append",RegexOptions.IgnoreCase);
                    string separator="\n";
                    if(join){object configured=Get(inputs,"delimiter")??Get(inputs,"separator")??Get(inputs,"deliminator")??Get(inputs,"join_with");separator=configured is string?(string)configured:"";}
                    string text=Combine(textFields.Select(p=>Walk(p.Value,negative,true,depth+1)),separator,!join);
                    if(text.Length>0)return text;
                }
                var branches=inputs.Where(p=>!Excluded(p.Key)&&Polarity(p.Key)==0&&Link(p.Value)!=null).ToList();
                string joined=Combine(branches.Select(p=>Walk(p.Value,negative,textContext,depth+1)),"\n",true);
                if(joined.Length==0)Note("节点 #"+id+"（"+kind+"）未保存可识别的提示词文本；可查看原始 JSON。动态节点的结果未保存时无法从图片恢复。");
                return joined;
            }
            finally{path.Remove(token);}
        }
        static string Combine(IEnumerable<string> values,string separator,bool distinct)
        {
            var parts=values.Where(s=>!string.IsNullOrEmpty(s));if(distinct)parts=parts.Distinct(StringComparer.Ordinal);
            var list=new List<string>();int length=0;
            foreach(string part in parts){length+=part.Length+separator.Length;if(length>16*1024*1024)throw new System.IO.InvalidDataException("提示词文本超过 16 MB。");list.Add(part);}
            return string.Join(separator,list);
        }
    }
}
