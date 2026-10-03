using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQImageSwitch
{
    public class FileDisguisePage : ToolPage
    {
        readonly FileCard video,archive;
        readonly ProgressBar progress;
        public FileDisguisePage(ExportLocation location,Action<bool> changed)
            :base("文件伪装","测试功能 · 用 JPG 显示封面，完整保存文件。QQ 原图传输兼容性需实际往返验证。",location,changed)
        {
            Ui.Add(Body,Ui.Text("发送时勾选「原图」，接收后查看并保存原图，再用本工具还原。缩略图、截图和重新压缩后的图片不能保证还原。",9));
            video=new FileCard(this,true);archive=new FileCard(this,false);Ui.Add(Body,video.Card);Ui.Add(Body,archive.Card);
            progress=new ProgressBar {Dock=DockStyle.Top,Height=6,Margin=new Padding(0,0,0,8),Visible=false};Ui.Add(Footer,progress);
            ActionButton("校验 QQ 原图",delegate{if(OutputFolder.Ready())using(var f=new QQRoundTripForm(OutputFolder.Destination))f.ShowDialog(FindForm());},false);
            FinishLayout();
        }
        IProgress<FileProgress> Reporter(){return new Progress<FileProgress>(p=>{if(!IsDisposed&&Busy){Status.Text=p.Stage+" · "+p.Percent+"%";progress.Value=p.Percent;}});}
        async Task Work(Func<CancellationToken,IProgress<FileProgress>,Task<string>> work,bool report)
        {
            if(Busy)return;progress.Value=0;progress.Visible=true;
            try{await Run(t=>work(t,Reporter()),report);}finally{progress.Visible=false;}
        }
        public override void Demo(){video.Demo();archive.Demo();}
        internal string StateFingerprint(){return video.Fingerprint()+archive.Fingerprint();}
        protected override void Dispose(bool disposing){if(disposing){if(video!=null)video.Dispose();if(archive!=null)archive.Dispose();}base.Dispose(disposing);}
        internal async Task CheckBehavior(string folder)
        {
            folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);string cover=Path.Combine(folder,"封面.jpg"),other=Path.Combine(folder,"另一封面.jpg"),input=Path.Combine(folder,"输入.zip"),second=Path.Combine(folder,"另一个.zip");
            using(var b=new Bitmap(80,60)){using(var g=Graphics.FromImage(b))g.Clear(Color.Lavender);b.Save(cover,System.Drawing.Imaging.ImageFormat.Jpeg);using(var g=Graphics.FromImage(b))g.Clear(Color.Pink);b.Save(other,System.Drawing.Imaging.ImageFormat.Jpeg);}
            var zip=new byte[]{80,75,5,6,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};File.WriteAllBytes(input,zip);File.WriteAllBytes(second,zip);OutputFolder.TestSetFolder(folder);
            await video.Choose(cover,true);await archive.Choose(cover,true);await archive.Import(new[]{input,second});
            archive.Select(0);await archive.Choose(other,true);if(archive.items[0].CoverPath!=other||archive.items[1].CoverPath!=null)throw new Exception("Individual cover selection changed other rows");
            archive.coverText.Text="批量文字";archive.position.SelectedIndex=0;archive.canvas.TestDrag(new Point(50,60),new Point(180,90));
            if(archive.text.Position!=3||archive.text.X==50)throw new Exception("File cover free dragging did not update placement");
            string c=video.SharedCover;await archive.Generate(true,false);if(video.SharedCover!=c||archive.items.Count!=2||Busy||archive.LastOutputs.Count!=2)throw new Exception("Independent batch state lost");
            if(archive.LastOutputs.Any(p=>!Path.GetFileNameWithoutExtension(p).Contains("_a2i")))throw new Exception("Archive JPG filename does not have a2i marker");
            await archive.Restore(archive.LastOutputs.ToArray(),false);if(archive.LastRestored.Count!=2||archive.LastRestored.Any(p=>!File.ReadAllBytes(p).SequenceEqual(zip)))throw new Exception("File batch restore changed bytes");
            archive.Select(1);await archive.Generate(false,false);if(archive.LastOutputs.Count!=1||archive.items[0].CoverPath!=other)throw new Exception("Selected batch export lost individual covers");
            archive.ApplyAll();if(archive.items.Any(p=>p.CoverPath!=null))throw new Exception("Shared cover apply did not clear individual covers");
            string empty=Path.Combine(folder,"empty-restore-"+Guid.NewGuid().ToString("N"));OutputFolder.TestSetFolder(empty);await archive.Restore(new string[0],false);
            if(Directory.Exists(empty))throw new Exception("Empty file restore created a directory");OutputFolder.TestSetFolder(folder);
        }
        sealed class FileCard : IDisposable
        {
            readonly FileDisguisePage page;readonly bool video;
            internal readonly CoverCanvas canvas;internal readonly SoftInput coverText;internal readonly SoftCombo position;
            readonly NumericUpDown size,x,y;readonly SoftCheck numbered;readonly Label coverLabel,queueLabel;
            readonly DataGridView queue;readonly System.Windows.Forms.Timer previewTimer;
            internal readonly List<FileBatchItem> items=new List<FileBatchItem>();internal readonly CoverTextOptions text=new CoverTextOptions{Position=3};
            Bitmap baseImage,preview;string cachedPath;bool resetting,dragging,disposed;
            public readonly TableLayoutPanel Card;
            public string SharedCover;
            public List<string> LastOutputs=new List<string>(),LastRestored=new List<string>();
            static readonly int[] Positions={3,4,6,5,7,0,8,9,2,1};
            public FileCard(FileDisguisePage owner,bool movie)
            {
                page=owner;video=movie;Card=new FileCardLayout {ColumnCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Padding=new Padding(18,16,18,12),BackColor=Color.White,Margin=new Padding(0,0,0,16)};Card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Card.SuspendLayout();
                Ui.Add(Card,Ui.Text(video?"视频伪装":"压缩包伪装",14,true));
                Ui.Add(Card,Ui.Text(video?"批量封装 MP4 · 文件名加 _v2i；一键还原原视频，不转码。":"批量封装 ZIP / RAR / 7Z · 文件名加 _a2i。ZIP 改后缀可在 WinRAR / 7-Zip 解压；RAR 推荐 WinRAR；7Z 请用本工具还原。",9));
                canvas=new CoverCanvas {Dock=DockStyle.Top,Height=220,MinimumSize=new Size(0,150),Margin=new Padding(0,0,0,10)};Ui.Add(Card,canvas);
                coverLabel=Ui.Text("默认封面",9);Ui.Add(Card,coverLabel);
                Ui.Add(Card,Ui.Flow(Ui.Button("设置共用封面…",async delegate{await Pick(true,true);}),Ui.Button("选中换封面…",async delegate{await Pick(true,false);}),Ui.Button("当前封面应用全部",delegate{ApplyAll();}),Ui.Button("选中使用共用封面",delegate{foreach(int i in Selected()){items[i].CoverPath=null;items[i].State="待导出";}RefreshRows();})));
                coverText=new SoftInput {Width=330,MaxLength=200,Margin=new Padding(0,0,12,10)};
                position=new SoftCombo {Width=165,VisibleRows=3,Margin=new Padding(0,0,12,10)};position.Items.AddRange(new object[]{"自由拖动","上左","上右","上中","中左","正中","中右","下左","下中","下右"});position.SelectedIndex=0;
                size=FileToolPage.Number(2,18,7);x=FileToolPage.Number(0,100,50);y=FileToolPage.Number(0,100,50);
                Ui.Add(Card,Ui.Flow(Ui.Text("封面文字"),coverText,Ui.Text("放置位置"),position,Ui.Button("清空文字",delegate{coverText.Text="";text.Text="";previewTimer.Stop();RefreshPreview();})));
                numbered=new SoftCheck {Text="右下角自动编号",Checked=true,AutoSize=true,Margin=new Padding(0,0,16,10)};
                Ui.Add(Card,Ui.Flow(Ui.Text("文字大小（%）"),size,Ui.Text("横向（%）"),x,Ui.Text("纵向（%）"),y,Ui.Button("文字颜色…",delegate{using(var d=new ColorDialog{Color=text.Color,FullOpen=true})if(L.Show(d,page.FindForm())==DialogResult.OK){text.Color=d.Color;Changed();}}),numbered));
                Ui.Add(Card,Ui.Text("选中队列中的文件可预览自己的封面；自由拖动可直接在封面上放置文字。编号按队列顺序生成，改封面不会修改输入文件。",9));
                queueLabel=Ui.Text("文件队列 · 0 个",12,true);Ui.Add(Card,queueLabel);
                Ui.Add(Card,Ui.Flow(Ui.Button(video?"＋ 批量导入视频…":"＋ 批量导入压缩包…",async delegate{await Pick(false,false);},true),Ui.Button("移除选中",delegate{Remove();}),Ui.Button("上移",delegate{Move(-1);}),Ui.Button("下移",delegate{Move(1);}),Ui.Button("清空队列",delegate{items.Clear();RefreshRows();})));
                queue=new SoftGrid {Dock=DockStyle.Top,Height=280,MinimumSize=new Size(0,280),AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,ReadOnly=true,MultiSelect=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,BackgroundColor=Color.FromArgb(253,252,255),BorderStyle=BorderStyle.None,AutoGenerateColumns=false,ScrollBars=ScrollBars.Both,EnableHeadersVisualStyles=false,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize};
                queue.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(241,237,250);queue.ColumnHeadersDefaultCellStyle.ForeColor=Ui.Ink;queue.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;queue.DefaultCellStyle.ForeColor=Ui.Ink;queue.DefaultCellStyle.SelectionBackColor=Color.FromArgb(237,232,253);queue.DefaultCellStyle.SelectionForeColor=Ui.Ink;queue.DefaultCellStyle.Padding=new Padding(5);queue.RowTemplate.Height=34;queue.GridColor=Color.FromArgb(237,233,246);
                AddColumn("number","序号",80);AddColumn("file","原文件",210);queue.Columns[1].MinimumWidth=210;queue.Columns[1].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;AddColumn("cover","配对封面",160);AddColumn("size","文件大小",130);AddColumn("state","状态",110);Ui.Add(Card,queue);
                queue.SelectionChanged+=delegate{if(!resetting)RefreshPreview();};
                Ui.Add(Card,Ui.Flow(Ui.Button("批量生成 JPG",async delegate{await Generate(true,true);},true),Ui.Button("仅生成选中 JPG",async delegate{await Generate(false,true);}),Ui.Button("批量还原文件…",async delegate{if(page.Busy)return;using(var d=new OpenFileDialog{Filter="文件伪装 JPG|*.jpg;*.jpeg",Multiselect=true})if(L.Show(d,page.FindForm())==DialogResult.OK)await Restore(d.FileNames,true);})));
                previewTimer=new System.Windows.Forms.Timer{Interval=180};previewTimer.Tick+=delegate{previewTimer.Stop();RefreshPreview();};
                coverText.TextChanged+=delegate{Changed();};position.SelectedIndexChanged+=delegate{Changed();};size.ValueChanged+=delegate{Changed();};x.ValueChanged+=delegate{Changed();};y.ValueChanged+=delegate{Changed();};numbered.CheckedChanged+=delegate{Changed();};
                canvas.PositionChanged+=delegate(object sender,TextPositionEventArgs e){dragging=true;try{x.Value=e.X;y.Value=e.Y;text.X=e.X;text.Y=e.Y;RefreshPreview();}finally{dragging=false;}};
                foreach(Control c in new Control[]{Card,canvas,queue}){c.AllowDrop=true;c.DragEnter+=delegate(object s,DragEventArgs e){if(!page.Busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};c.DragDrop+=async delegate(object s,DragEventArgs e){if(page.Busy)return;var paths=(string[])e.Data.GetData(DataFormats.FileDrop);var covers=paths.Where(IsImage).ToArray();var payload=paths.Where(p=>!IsImage(p)).ToArray();if(covers.Length>1){page.Status.Text="一次请选择一张封面；原文件可以批量导入。";return;}if(covers.Length==1)await Choose(covers[0],true);if(payload.Length>0)await Import(payload);};}
                L.Changed+=LanguageChanged;Card.ResumeLayout(true);canvas.FollowViewport(page.Scroller,.48,220,660);RefreshPreview();
            }
            void AddColumn(string id,string caption,int width){queue.Columns.Add(new DataGridViewTextBoxColumn{Name=id,HeaderText=caption,Width=width,MinimumWidth=55,SortMode=DataGridViewColumnSortMode.NotSortable});}
            static bool IsImage(string p){return new[]{".jpg",".jpeg",".png",".webp",".bmp",".gif"}.Contains(Path.GetExtension(p).ToLowerInvariant());}
            void LanguageChanged(){cachedPath=null;if(!disposed){RefreshRows();Card.PerformLayout();}}
            void Changed(){if(resetting||dragging||page.Busy)return;text.Text=coverText.Text;text.Position=Positions[position.SelectedIndex];text.SizePercent=(int)size.Value;text.X=(int)x.Value;text.Y=(int)y.Value;x.Enabled=y.Enabled=text.Position==3;canvas.TextPlacement=text.Position==3&&!string.IsNullOrWhiteSpace(text.Text);foreach(var item in items)item.State="待导出";previewTimer.Stop();previewTimer.Start();}
            int[] Selected(){return queue.SelectedRows.Cast<DataGridViewRow>().Select(r=>r.Index).OrderBy(i=>i).ToArray();}
            int Current(){return queue.CurrentRow==null?-1:queue.CurrentRow.Index;}
            public void Select(int index){queue.ClearSelection();if(index>=0&&index<queue.Rows.Count){queue.CurrentCell=queue.Rows[index].Cells[1];queue.Rows[index].Selected=true;}RefreshPreview();}
            void RefreshRows(int focus=-1)
            {
                var selected=Selected();if(focus<0)focus=Current();resetting=true;queue.SuspendLayout();try{queue.Rows.Clear();for(int i=0;i<items.Count;i++){var item=items[i];queue.Rows.Add(Batch.SequenceLabel(i+1),item.Name,item.CoverPath==null?"共用封面":Path.GetFileName(item.CoverPath),(item.Length/1048576.0).ToString("0.00")+" MiB",item.State);}queue.ClearSelection();if(items.Count>0){focus=Math.Max(0,Math.Min(items.Count-1,focus));queue.CurrentCell=queue.Rows[focus].Cells[1];foreach(int i in selected.Where(i=>i<items.Count))queue.Rows[i].Selected=true;if(selected.Length==0)queue.Rows[focus].Selected=true;}}finally{queue.ResumeLayout();resetting=false;}queueLabel.Text="文件队列 · "+items.Count+" 个";RefreshPreview();
            }
            void RefreshPreview()
            {
                if(disposed)return;int index=Current();string path=index>=0&&index<items.Count?(items[index].CoverPath??SharedCover):SharedCover;string key=path??"builtin";
                try{if(baseImage==null||key!=cachedPath){if(baseImage!=null)baseImage.Dispose();baseImage=null;if(path==null)baseImage=Codec.DefaultCover(700,438,false);else{bool converted;using(var original=Codec.Decode(FileDisguise.Cover(path,out converted))){var dims=Codec.OutputSize(original,700);baseImage=Codec.Fit(original,dims.Width,dims.Height,Color.White);}}cachedPath=key;}var next=Batch.MakeCover(baseImage,baseImage.Width,baseImage.Height,index+1,numbered.Checked&&index>=0,Color.White,text);canvas.Image=next;if(preview!=null)preview.Dispose();preview=next;coverLabel.Text=(path==null?"默认封面":Path.GetFileName(path))+(index>=0?" · 当前第 "+(index+1)+" 个文件":" · 后续导入文件的共用封面");}
                catch(Exception ex){canvas.Image=null;coverLabel.Text="封面无法读取："+ex.Message;}
            }
            async Task Pick(bool cover,bool shared)
            {
                if(page.Busy)return;if(cover&&!shared&&items.Count==0){page.Status.Text="请先导入文件，或选择「设置共用封面」。";return;}
                using(var d=new OpenFileDialog{Filter=cover?"图片封面|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif":video?"MP4 视频|*.mp4":"压缩包|*.zip;*.rar;*.7z",Multiselect=!cover})if(L.Show(d,page.FindForm())==DialogResult.OK){if(cover)await Choose(d.FileName,true,shared);else await Import(d.FileNames);}
            }
            public async Task Choose(string path,bool cover,bool shared=false)
            {
                if(!cover){await Import(new[]{path});return;}if(page.Busy)return;
                await page.Work(async(t,p)=>{await Task.Run(()=>{t.ThrowIfCancellationRequested();bool converted;using(var decoded=Codec.Decode(FileDisguise.Cover(path,out converted))){}},t);if(shared||items.Count==0){SharedCover=path;foreach(var item in items.Where(i=>i.CoverPath==null))item.State="待导出";}else foreach(int i in Selected()){items[i].CoverPath=path;items[i].State="待导出";}RefreshRows();return "封面已选择；原图片保持不变。";},false);
            }
            public async Task Import(string[] paths)
            {
                if(page.Busy)return;await page.Work(async(t,p)=>{var accepted=new List<FileBatchItem>();var errors=new List<string>();await Task.Run(()=>{foreach(string path in paths){t.ThrowIfCancellationRequested();try{string ext=Path.GetExtension(path).ToLowerInvariant();if(video?ext!=".mp4":!new[]{".zip",".rar",".7z"}.Contains(ext))throw new InvalidDataException(video?"请选择 MP4 视频。":"请选择单文件 ZIP、RAR 或 7Z。分卷请先合并。");FileDisguise.ValidateFile(path,t);if(!items.Concat(accepted).Any(i=>string.Equals(i.Path,path,StringComparison.OrdinalIgnoreCase)))accepted.Add(new FileBatchItem{Path=path,Length=new FileInfo(path).Length});}catch(OperationCanceledException){throw;}catch(Exception ex){errors.Add(Path.GetFileName(path)+"："+L.T(ex.Message));}}},t);items.AddRange(accepted);RefreshRows();return L.T("已导入 {0} 个文件，共 {1} 个。").Replace("{0}",accepted.Count.ToString()).Replace("{1}",items.Count.ToString())+(errors.Count>0?"\n"+string.Join("\n",errors):"");},false);
            }
            public void ApplyAll(){if(page.Busy)return;int i=Current();if(i>=0)SharedCover=items[i].CoverPath??SharedCover;foreach(var item in items){item.CoverPath=null;item.State="待导出";}RefreshRows();}
            void Remove(){if(page.Busy)return;foreach(int i in Selected().OrderByDescending(i=>i))items.RemoveAt(i);RefreshRows();}
            void Move(int delta){if(page.Busy)return;var selected=Selected();if(selected.Length!=1){page.Status.Text="请只选中一个文件，再调整顺序。";return;}int i=selected[0],next=i+delta;if(next<0||next>=items.Count)return;var item=items[i];items.RemoveAt(i);items.Insert(next,item);RefreshRows(next);Select(next);}
            public async Task Generate(bool all,bool show)
            {
                if(page.Busy)return;var indices=all?Enumerable.Range(0,items.Count).ToArray():Selected();if(indices.Length==0){page.Status.Text="请先导入并选中文件。";return;}if(!page.OutputFolder.Ready())return;
                previewTimer.Stop();text.Text=coverText.Text;text.Position=Positions[position.SelectedIndex];text.SizePercent=(int)size.Value;text.X=(int)x.Value;text.Y=(int)y.Value;
                var originals=indices.Select(i=>items[i]).ToArray();var snapshot=originals.Select(i=>i.Copy()).ToArray();string shared=SharedCover,folder=page.OutputFolder.Destination;bool stamp=numbered.Checked;var options=text.Copy();var ordinals=indices.Select(i=>i+1).ToArray();
                var rows=new Progress<BatchProgress>(p=>{if(disposed||!page.Busy)return;originals[p.Index].State=snapshot[p.Index].State;queue.Rows[indices[p.Index]].Cells["state"].Value=originals[p.Index].State;page.Status.Text=L.T("已处理 {0}/{1} 个文件").Replace("{0}",p.Completed.ToString()).Replace("{1}",p.Total.ToString());});
                await page.Work(async(t,p)=>{var result=await Task.Run(()=>FileBatch.Export(snapshot,shared,folder,stamp,options,t,rows,p,ordinals),t);LastOutputs=result.Files.ToList();for(int i=0;i<originals.Length;i++)originals[i].State=snapshot[i].State;RefreshRows();return Summary(result);},show);
            }
            static string Summary(BatchResult r){return L.T(r.Cancelled?"任务已停止。成功 {0} 个，失败 {1} 个。":"处理完成。成功 {0} 个，失败 {1} 个。").Replace("{0}",r.Files.Count.ToString()).Replace("{1}",r.Errors.Count.ToString())+"\n"+string.Join("\n",r.Files)+(r.Errors.Count==0?"":"\n"+string.Join("\n",r.Errors.Select(L.T)));}
            public async Task Restore(string[] paths,bool show)
            {
                if(page.Busy)return;if(paths==null||paths.Length==0){page.Status.Text="请先导入并选中文件。";return;}if(!page.OutputFolder.Ready())return;string folder=page.OutputFolder.Destination;await page.Work(async(t,p)=>{var result=await Task.Run(()=>{var r=new BatchResult();foreach(string path in paths){try{t.ThrowIfCancellationRequested();r.Files.Add(FileDisguise.Restore(path,folder,t,p));}catch(OperationCanceledException){r.Cancelled=true;break;}catch(Exception ex){r.Errors.Add(Path.GetFileName(path)+"："+L.T(ex.Message));}}return r;},t);LastRestored=result.Files.ToList();return Summary(result);},show);
            }
            public void Demo(){coverText.Text=L.T("查看原文件");Changed();RefreshPreview();}
            public string Fingerprint(){return Inspection.Json().Serialize(new{SharedCover,Files=items.Select(i=>new{i.Path,i.CoverPath,i.Length,i.State}).ToArray(),Text=coverText.Text,Position=position.SelectedIndex,Size=size.Value,X=x.Value,Y=y.Value,Numbered=numbered.Checked,Color=text.Color.ToArgb()});}
            public void Dispose(){disposed=true;L.Changed-=LanguageChanged;previewTimer.Stop();previewTimer.Dispose();canvas.Image=null;if(preview!=null)preview.Dispose();if(baseImage!=null)baseImage.Dispose();}
        }
        sealed class FileCardLayout : SoftTable
        {
            readonly Dictionary<int,KeyValuePair<int,Size>> measured=new Dictionary<int,KeyValuePair<int,Size>>();
            static int Fingerprint(Control c)
            {
                unchecked{if(c.IsDisposed||c.Disposing)return 0;string caption=c is Label||c is ButtonBase?c.Text:string.Empty;int hash=caption.GetHashCode()*31+c.Font.GetHashCode();hash=hash*31+c.Margin.GetHashCode();hash=hash*31+c.Padding.GetHashCode();hash=hash*31+c.MinimumSize.GetHashCode();hash=hash*31+c.MaximumSize.GetHashCode();hash=hash*31+(c.Visible?1:0);if(!c.AutoSize&&!(c is TableLayoutPanel)&&!(c is FlowLayoutPanel))hash=hash*31+c.Height;if(!(c is UpDownBase)&&!(c is TextBoxBase)&&!(c is ComboBox))foreach(Control child in c.Controls)hash=hash*31+Fingerprint(child);return hash;}
            }
            public override Size GetPreferredSize(Size proposedSize)
            {
                if(IsDisposed||Disposing)return Size;
                int width=Math.Max(120,proposedSize.Width>0?proposedSize.Width:Width);if(!AutoSize)return new Size(width,HeightResize.Resolve(this,Height));int stamp=Fingerprint(this);KeyValuePair<int,Size> cached;
                if(measured.TryGetValue(width,out cached)&&cached.Key==stamp)return cached.Value;
                int height=Padding.Vertical;
                foreach(Control c in Controls)if(c.Visible)height+=c.GetPreferredSize(new Size(Math.Max(80,width-Padding.Horizontal-c.Margin.Horizontal),0)).Height+c.Margin.Vertical;
                Size result=new Size(width,height);if(measured.Count>8)measured.Clear();measured[width]=new KeyValuePair<int,Size>(stamp,result);return result;
            }
        }
    }
}
