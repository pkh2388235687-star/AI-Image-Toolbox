using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQImageSwitch
{
    class ParameterBox : SoftTable
    {
        readonly Panel scroll;
        readonly TableLayoutPanel fields;
        readonly Label file;
        readonly SoftCombo sampler;
        readonly Action<string> status;
        readonly ToolTip tips=new ToolTip();
        readonly List<InfoField> raw=new List<InfoField>();
        List<SamplerInfo> groups;
        Action<string> copyValue=Clipboard.SetText;
        public event Action<string> FileDropped;
        public ParameterBox(string title,EventHandler open,Action<string> changed,bool comfy)
        {
            status=changed;Dock=DockStyle.Top;AutoSize=true;ColumnCount=1;Padding=new Padding(16,14,16,14);BackColor=Color.White;Margin=new Padding(0,0,0,18);ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));SuspendLayout();
            Ui.Add(this,Ui.Flow(Ui.Text(title,15,true),Ui.Button("读取图片…",open)));
            file=Ui.Text("尚未读取图片",9);Ui.Add(this,file);
            if(comfy){sampler=new SoftCombo {Width=420,VisibleRows=5,Margin=new Padding(0,0,0,12)};Ui.Add(this,Ui.Flow(Ui.Text("采样节点"),sampler));sampler.SelectedIndexChanged+=delegate{ShowGroup();};}
            scroll=new SoftPanel {Dock=DockStyle.Top,Height=340,AutoScroll=true,BackColor=Color.FromArgb(250,248,254),Padding=new Padding(10)};
            fields=Ui.Column(new Padding(0));scroll.Controls.Add(fields);Ui.Add(this,scroll);
            scroll.ClientSizeChanged+=delegate{fields.Width=Math.Max(120,scroll.ClientSize.Width-scroll.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth);};
            foreach(Control control in new Control[]{this,scroll,fields})
            {
                control.AllowDrop=true;control.DragEnter+=delegate(object s,DragEventArgs e){if(Enabled&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
                control.DragDrop+=delegate(object s,DragEventArgs e){if(Enabled&&FileDropped!=null){var paths=(string[])e.Data.GetData(DataFormats.FileDrop);if(paths.Length>0)FileDropped(paths[0]);}};
            }
            ResumeLayout(true);ShowFields(new List<InfoField>{new InfoField("读取说明",comfy?"读取 ComfyUI prompt / workflow，按采样节点区分正负提示词、CFG、种子、模型、LoRA 等参数。":"读取 WebUI parameters；正负提示词、Steps、CFG、Sampler、Seed、模型等分别显示。")});
        }
        public void Set(string path,InspectionResult data,bool comfy)
        {
            file.Text=System.IO.Path.GetFileName(path);tips.SetToolTip(file,path);
            raw.Clear();groups=null;
            if(!comfy){ShowFields(data.WebUI.Count>0?data.WebUI:new List<InfoField>{new InfoField("读取结果","没有发现 WebUI 参数；图片可能未保存生成信息，或信息已被清理。")});return;}
            if(data.PromptJson!=null)raw.Add(new InfoField("原始 prompt JSON",data.PromptJson));
            if(data.WorkflowJson!=null)raw.Add(new InfoField("原始 workflow JSON",data.WorkflowJson));
            if(data.Notes.Count>0)raw.Add(new InfoField("解析说明",string.Join("\n",data.Notes)));
            sampler.Items.Clear();sampler.SelectedIndex=-1;groups=data.Comfy;
            foreach(var group in groups)sampler.Items.Add(group.Name);
            if(groups.Count>0)sampler.SelectedIndex=0;else ShowFields(raw.Count>0?raw:new List<InfoField>{new InfoField("读取结果","没有发现 ComfyUI prompt / workflow；图片可能未保存工作流，或信息已被清理。")});
            sampler.Enabled=groups.Count>0;
        }
        void ShowGroup(){if(groups==null||sampler.SelectedIndex<0)return;ShowFields(groups[sampler.SelectedIndex].Fields.Concat(raw).ToList());}
        void ShowFields(IList<InfoField> values)
        {
            fields.SuspendLayout();var old=fields.Controls.Cast<Control>().ToArray();fields.Controls.Clear();foreach(var control in old)control.Dispose();fields.RowCount=0;fields.RowStyles.Clear();
            foreach(var value in values)
            {
                var row=new SoftTable {Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,RowCount=1,BackColor=Color.Transparent,Margin=new Padding(0,0,0,10)};
                int labelWidth=Math.Max(170,Font.Height*8),buttonSize=Math.Max(42,Font.Height+18);
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,labelWidth));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,buttonSize+4));
                row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var label=Ui.Text(value.Name,Font.Size,true);label.Tag="fixedwrap";label.MaximumSize=new Size(labelWidth-12,0);label.Margin=new Padding(0,8,12,0);row.Controls.Add(label,0,0);
                int lines=Math.Max(1,Math.Min(5,value.Value.Length/60+value.Value.Count(c=>c=='\n')+1));
                var text=new ResizableTextBox {Tag=new[]{"读取说明","读取结果","解析说明","提示词读取说明"}.Contains(value.Name)?null:"raw",Text=value.Value,ReadOnly=true,Multiline=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Height=Math.Max(44,Font.Height*lines+14),MinimumSize=new Size(80,44),BorderStyle=BorderStyle.FixedSingle,BackColor=Color.FromArgb(254,253,255),ForeColor=Ui.Ink,Margin=new Padding(0,0,8,0)};row.Controls.Add(text,1,0);
                var copy=new SoftButton {Text="",NavigationIcon=0,Size=new Size(buttonSize,buttonSize),BackColor=Color.White,ForeColor=Ui.Ink,Cursor=Cursors.Hand,AccessibleName="复制 "+value.Name,Enabled=value.Value.Length>0};
                InfoField captured=value;copy.Click+=delegate{try{copyValue(captured.Value);status("已复制："+captured.Name);}catch(Exception ex){status("复制失败："+ex.Message);}};row.Controls.Add(copy,2,0);Ui.Add(fields,row);
            }
            fields.ResumeLayout(true);fields.Width=Math.Max(120,scroll.ClientSize.Width-scroll.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth);scroll.AutoScrollPosition=Point.Empty;
        }
        internal void CheckCopy()
        {
            var button=fields.Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<SoftButton>().First(b=>b.Enabled);
            string copied=null;var previous=copyValue;copyValue=value=>copied=value;
            try{button.PerformClick();if(string.IsNullOrEmpty(copied))throw new Exception("Parameter copy button failed");}
            finally{copyValue=previous;}
        }
        internal void FitViewport(Control viewport)
        {
            EventHandler fit=delegate{int height=HeightResize.Resolve(scroll,ImageCanvas.ViewportHeight(viewport,.40,220,460));if(scroll.Height!=height)scroll.Height=height;};
            viewport.ClientSizeChanged+=fit;viewport.FontChanged+=fit;HeightResize.Enable(scroll,h=>fit(null,EventArgs.Empty));HeightResize.Enable(this,h=>{HeightResize.Set(scroll,h>0?Math.Max(100,scroll.Height+h-Height):0);fit(null,EventArgs.Empty);});fit(null,EventArgs.Empty);
        }
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
    class InspectionPage : ToolPage
    {
        readonly ParameterBox webui,comfy;
        InspectionResult webData,comfyData;
        string webPath,comfyPath;
        public InspectionPage(ExportLocation location,Action<bool> changed):base("tag读取","读取生成图片中的 tag 和具体参数，每个字段可以单独复制或导出 TXT。支持 PNG / APNG、JPEG、WebP。",location,changed)
        {
            Ui.Add(Body,Ui.Flow(Ui.Button("读取图片到两个板块…",async delegate{await Pick(0);},true),Ui.Button("复制全部参数",delegate{CopyAll();})));
            ActionButton("一键导出 TXT",async delegate{await ExportText();});
            webui=new ParameterBox("WebUI 参数",async delegate{await Pick(1);},text=>Status.Text=text,false);Ui.Add(Body,webui);
            comfy=new ParameterBox("ComfyUI 参数",async delegate{await Pick(2);},text=>Status.Text=text,true);Ui.Add(Body,comfy);
            webui.FileDropped+=async path=>await LoadInfo(path,1);comfy.FileDropped+=async path=>await LoadInfo(path,2);
            foreach(var target in new Control[]{this})
            {
                var captured=target;target.AllowDrop=true;
                target.DragEnter+=delegate(object s,DragEventArgs e){if(!Busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
                target.DragDrop+=async delegate(object s,DragEventArgs e){if(!Busy){var files=(string[])e.Data.GetData(DataFormats.FileDrop);if(files.Length>0)await LoadInfo(files[0],0);}};
            }
            Status.Text="读取不会修改图片。已清信息的图片无法恢复原来被删除的 tag / 工作流。";FinishLayout();webui.FitViewport(Scroller);comfy.FitViewport(Scroller);
        }
        async Task Pick(int target){if(Busy)return;using(var d=new OpenFileDialog {Title="选择要读取参数的图片",Filter="生成图片|*.png;*.apng;*.jpg;*.jpeg;*.webp"})if(L.Show(d,FindForm())==DialogResult.OK)await LoadInfo(d.FileName,target);}
        async Task LoadInfo(string path,int target)
        {
            await Run(async token=>{var data=await Task.Run(()=>Inspection.Read(path));if(target!=2){webData=data;webPath=path;webui.Set(path,data,false);}if(target!=1){comfyData=data;comfyPath=path;comfy.Set(path,data,true);}return "读取完成；每项右侧可复制，底部可一键导出 TXT。"+(data.Notes.Count>0?"\n"+string.Join("\n",data.Notes):"");},false);
        }
        void CopyAll()
        {
            string text=InspectionReport.Build(webData,webPath,comfyData,comfyPath);if(text.Length==0){Status.Text="请先读取含生成信息的图片。";return;}
            try{Clipboard.SetText(text);Status.Text="全部参数已复制。";}catch(Exception ex){Status.Text="复制失败："+ex.Message;}
        }
        async Task ExportText(bool showResult=true)
        {
            if(Busy)return;
            if(InspectionReport.Build(webData,webPath,comfyData,comfyPath).Length==0){Status.Text="请先读取含生成信息的图片。";return;}
            if(!OutputFolder.Ready())return;string folder=OutputFolder.Destination;
            await Run(token=>Task.Run(()=>{token.ThrowIfCancellationRequested();var paths=InspectionReport.Export(folder,webData,webPath,comfyData,comfyPath);return "已分别导出 "+paths.Length+" 份 TXT；没有生成信息的板块已跳过。\n"+string.Join("\n",paths);}),showResult);
        }
#if SELF_TEST
        public override void Demo()
        {
            webData=comfyData=InspectionTests.DemoResult();webPath="WebUI 示例.png";comfyPath="ComfyUI 示例.png";webui.Set(webPath,webData,false);comfy.Set(comfyPath,comfyData,true);Status.Text="示例信息；真实读取以图片中保存的内容为准。";
        }
        internal void CheckBehavior(){Demo();webui.CheckCopy();comfy.CheckCopy();string text=InspectionReport.Build(webData,webPath,comfyData,comfyPath);if(!text.Contains("WebUI 示例.png")||!text.Contains("ComfyUI 示例.png")||!text.Contains("负面 tag")||!text.Contains("1234567890123456"))throw new Exception("TXT report omitted fields or source files");}
        internal async Task CheckExport(string folder)
        {
            CheckBehavior();string previous=OutputFolder.Folder;
            try
            {
                OutputFolder.TestSetFolder(Path.GetFullPath(folder));string destination=OutputFolder.Destination;
                var before=Directory.Exists(destination)?Directory.GetFiles(destination,"*.txt"):new string[0];await ExportText(false);
                var added=Directory.GetFiles(destination,"*.txt").Except(before).ToArray();
                if(added.Length!=2||!added.Any(p=>File.ReadAllText(p)==InspectionReport.Build(webData,webPath,null,null))||!added.Any(p=>File.ReadAllText(p)==InspectionReport.Build(null,null,comfyData,comfyPath)))throw new Exception("Separate TXT export omitted data or used wrong folder");
                await ExportText(false);if(Directory.GetFiles(destination,"*.txt").Length!=before.Length+4)throw new Exception("TXT export overwrote existing file");
                webData=null;webPath=null;await ExportText(false);
                if(Directory.GetFiles(destination,"*.txt").Length!=before.Length+5)throw new Exception("Unloaded WebUI panel exported a TXT");
                webData=new InspectionResult();webPath="无参数.png";await ExportText(false);
                if(Directory.GetFiles(destination,"*.txt").Length!=before.Length+6)throw new Exception("Empty WebUI metadata exported a TXT");
            }
            finally{Demo();OutputFolder.TestSetFolder(previous);}
        }
#endif
    }
    static class InspectionReport
    {
        internal static string Build(InspectionResult web,string webPath,InspectionResult comfy,string comfyPath)
        {
            var text=new StringBuilder();
            if(!string.IsNullOrEmpty(webPath)&&web!=null&&web.WebUI.Any(f=>!string.IsNullOrWhiteSpace(f.Value))){text.AppendLine("===== WebUI =====");text.AppendLine(L.T("图片：")+Path.GetFileName(webPath));foreach(var field in web.WebUI)Field(text,field);}
            if(!string.IsNullOrEmpty(comfyPath)&&comfy!=null&&(comfy.Comfy.Count>0||comfy.PromptJson!=null||comfy.WorkflowJson!=null))
            {
                text.AppendLine("===== ComfyUI =====");text.AppendLine(L.T("图片：")+Path.GetFileName(comfyPath));
                foreach(var group in comfy.Comfy){text.AppendLine("--- "+L.T(group.Name)+" ---");foreach(var field in group.Fields)Field(text,field);}
                if(comfy.PromptJson!=null)Field(text,new InfoField("原始 prompt JSON",comfy.PromptJson));
                if(comfy.WorkflowJson!=null)Field(text,new InfoField("原始 workflow JSON",comfy.WorkflowJson));
                if(comfy.Notes.Count>0)Field(text,new InfoField("解析说明",string.Join("\r\n",comfy.Notes)));
            }
            return text.ToString();
        }
        static void Field(StringBuilder text,InfoField field){text.AppendLine();text.AppendLine(L.T(field.Name)+(L.English?":":"："));text.AppendLine(new[]{"解析说明","提示词读取说明"}.Contains(field.Name)?L.T(field.Value):field.Value);}
        internal static string[] Export(string folder,InspectionResult web,string webPath,InspectionResult comfy,string comfyPath)
        {
            var paths=new List<string>();string text=Build(web,webPath,null,null);
            if(text.Length>0){Directory.CreateDirectory(folder);string path=ImageTools.Unique(folder,webPath,"_WebUI_tag",".txt");Save(path,text);paths.Add(path);}
            text=Build(null,null,comfy,comfyPath);
            if(text.Length>0){Directory.CreateDirectory(folder);string path=ImageTools.Unique(folder,comfyPath,"_ComfyUI_tag",".txt");Save(path,text);paths.Add(path);}
            return paths.ToArray();
        }
        internal static void Save(string path,string text){var encoding=new UTF8Encoding(true);Codec.SaveAtomic(path,encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());}
    }
}
