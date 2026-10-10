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
    public sealed class BackgroundRevealPage : ToolPage
    {
        readonly List<string> files=new List<string>();
        readonly SoftCombo mode,bound,priority,coverStyle;
        readonly NumericUpDown strength,effectiveStrength;
        readonly FlowLayoutPanel effectiveRow;bool manualEffective,updatingEffective;
        readonly FlowLayoutPanel strengthRow,coverStyleRow,processingRow;
        readonly SoftList queue;
        readonly ImageCanvas whiteView,blackView;
        readonly Label whiteInfo,info,modeHelp,sizeHelp,processingHelp,processingResult,coverHelp;
        readonly string[] coverNames={"按原图配色（自动花纹）","蓝紫花纹","暖橙花纹","青绿花纹","自选彩色封面图片"};
        int[] coverIds={0,4};
        int coverId,lastHelpWidth;bool rebuildingCover;
        BackgroundRevealAuto.Profile previewProfile;
        readonly TableLayoutPanel previews,whiteCard,blackCard;
        bool arrangingPreviews,stackedPreviews;
        readonly System.Windows.Forms.Timer debounce=new System.Windows.Forms.Timer{Interval=160};
        string light;
        Bitmap whiteImage,blackImage;
        CancellationTokenSource previewCancel;
        int ticket;
        public BackgroundRevealPage(ExportLocation location,Action<bool> changed)
            :base("背景显图","将两张图片合成透明 PNG：白底显示第一张，黑底显示第二张；不添加文字或编号。",location,changed)
        {
            OutputFolder.SetFeature("背景显图");
            whiteView=View("白底显示预览");blackView=View("黑底显示预览");
            Ui.Add(Body,Ui.Flow(Ui.Button("刷新效果预览",delegate{SchedulePreview();},true),Ui.Button("适应窗口",delegate{whiteView.ResetView();blackView.ResetView();})));
            previews=new TableLayoutPanel{ColumnCount=2,RowCount=1,AutoSize=false,Height=360,Dock=DockStyle.Top,BackColor=Color.Transparent,Margin=new Padding(0,0,0,8)};
            whiteCard=Card();blackCard=Card();Ui.Add(whiteCard,Ui.Text("白底显示预览",13,true));Ui.Add(whiteCard,whiteView);Ui.Add(blackCard,Ui.Text("黑底显示预览",13,true));Ui.Add(blackCard,blackView);
            previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));previews.RowStyles.Add(new RowStyle(SizeType.Absolute,360));
            previews.Controls.Add(whiteCard,0,0);previews.Controls.Add(blackCard,1,0);Ui.Add(Body,previews);
            info=Ui.Text("导入黑底图片后自动预览；封面可自动生成或自行选择。",9);Ui.Add(Body,info);
            previews.ClientSizeChanged+=delegate{ArrangePreviews();};whiteView.SizeChanged+=delegate{ArrangePreviews();};blackView.SizeChanged+=delegate{ArrangePreviews();};
            whiteCard.SizeChanged+=delegate{ArrangePreviews();};blackCard.SizeChanged+=delegate{ArrangePreviews();};

            mode=new SoftCombo{Width=250,VisibleRows=3,Margin=new Padding(0,0,12,10)};mode.Items.AddRange(new object[]{"彩色叠显","黑白显图"});mode.SelectedIndex=0;
            bound=new SoftCombo{Width=300,VisibleRows=3,Margin=new Padding(0,0,12,10)};bound.Items.AddRange(new object[]{"最长边 1200 像素","最长边 2048 像素","最长边 3200 像素","保留原尺寸"});bound.SelectedIndex=0;
            Ui.Add(Body,Ui.Flow(Ui.Text("生成模式"),mode));modeHelp=Ui.Text("",9);Ui.Add(Body,modeHelp);
            Ui.Add(Body,Ui.Flow(Ui.Text("导出尺寸："),bound));sizeHelp=Ui.Text("",9);Ui.Add(Body,sizeHelp);
            priority=new SoftCombo{Width=300,VisibleRows=3,Margin=new Padding(0,0,12,10)};priority.Items.AddRange(new object[]{"均衡色彩（原方式）","原图优先（标准黑底）","原色优化（柔和保色）","轮廓优先（统一色调）","二维码优化"});priority.SelectedIndex=2;
            strength=FileToolPage.Number(0,100,85);effectiveStrength=FileToolPage.Number(0,100,85);effectiveStrength.DecimalPlaces=1;effectiveRow=Ui.Flow(Ui.Text("自定义有效强度 %"),effectiveStrength,Ui.Button("恢复自动调节",delegate{manualEffective=false;SchedulePreview();}));strengthRow=Ui.Flow(Ui.Text("色彩强度 %"),strength,effectiveRow);processingRow=Ui.Flow(Ui.Text("处理方式"),priority);Ui.Add(Body,processingRow);processingHelp=Ui.Text("",9);processingHelp.AutoSize=false;processingHelp.Dock=DockStyle.Top;processingHelp.Tag="fixedwrap";Ui.Add(Body,processingHelp);processingResult=processingHelp;Ui.Add(Body,strengthRow);strengthRow.Visible=true;
            var input=Card();Ui.Add(input,Ui.Text("01  白底图片",13,true));coverStyle=new SoftCombo{Width=300,VisibleRows=3,Margin=new Padding(0,0,12,10)};coverStyle.Items.AddRange(new object[]{coverNames[0],coverNames[4]});coverStyle.SelectedIndex=0;coverStyleRow=Ui.Flow(Ui.Text("封面样式"),coverStyle);Ui.Add(input,coverStyleRow);coverHelp=Ui.Text("",9);Ui.Add(input,coverHelp);Ui.Add(input,Ui.Flow(Ui.Button("选择白底图片…",delegate{PickLight();},true),Ui.Button("使用自动封面",delegate{light=null;coverId=0;RebuildCoverChoices();SchedulePreview();})));
            whiteInfo=Ui.Text("自动封面 · 按原图配色与尺寸生成花纹",9);Ui.Add(input,whiteInfo);Ui.Add(Body,input);
            var dark=Card();Ui.Add(dark,Ui.Text("02  黑底图片队列",13,true));Ui.Add(dark,Ui.Flow(Ui.Button("＋ 批量导入黑底图片",delegate{PickDark();},true),Ui.Button("移除选中",delegate{int i=queue.SelectedIndex;if(i>=0){files.RemoveAt(i);FillQueue(Math.Min(i,files.Count-1));}}),Ui.Button("清空队列",delegate{files.Clear();FillQueue(-1);})));
            queue=new SoftList{Dock=DockStyle.Top,Height=210,IntegralHeight=false,BorderStyle=BorderStyle.None,HorizontalScrollbar=true,BackColor=Color.FromArgb(253,252,255),ForeColor=Ui.Ink,Margin=new Padding(0,0,0,8)};
            queue.SelectedIndexChanged+=delegate{SchedulePreview();};Ui.Add(dark,queue);Ui.Add(Body,dark);
            Ui.Add(Body,Ui.Text("透明 PNG 不是加密。请保留透明度；转成 JPG、截图或平台重新编码可能破坏效果。提取只能得到亮度调整后的显示图，不能还原原文件字节。",9));
            Ui.Add(Body,Ui.Flow(Ui.Button("提取白底与黑底显示图…",async delegate{await Extract();})));
            ActionButton("批量导出透明 PNG",async delegate{await Export(true);});ActionButton("导出选中透明 PNG",async delegate{await Export(false);});
            mode.SelectedIndexChanged+=delegate{RebuildCoverChoices();SchedulePreview();};priority.SelectedIndexChanged+=delegate{RebuildCoverChoices();SchedulePreview();};bound.SelectedIndexChanged+=delegate{UpdateHelp();};strength.ValueChanged+=delegate{SchedulePreview();};effectiveStrength.ValueChanged+=delegate{if(!updatingEffective){manualEffective=true;SchedulePreview();}};debounce.Tick+=async delegate{debounce.Stop();await Preview();};
            queue.AllowDrop=true;queue.DragEnter+=delegate(object s,DragEventArgs e){if(!Busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};queue.DragDrop+=delegate(object s,DragEventArgs e){if(!Busy)Add((string[])e.Data.GetData(DataFormats.FileDrop));};
            coverStyle.SelectedIndexChanged+=delegate{if(rebuildingCover||coverStyle.SelectedIndex<0)return;int id=coverIds[coverStyle.SelectedIndex];if(id==4){if(light==null)PickLight();else{coverId=4;UpdateHelp();}}else{coverId=id;light=null;RebuildCoverChoices();SchedulePreview();}};
            RebuildCoverChoices();UpdateHelp();
            FinishLayout();whiteView.FollowViewport(Scroller,.48,230,560);blackView.FollowViewport(Scroller,.48,230,560);ArrangePreviews();Body.SizeChanged+=delegate{if(lastHelpWidth!=Body.ClientSize.Width){lastHelpWidth=Body.ClientSize.Width;UpdateHelp();}};
        }
        void RebuildCoverChoices()
        {
            int id=light!=null?4:coverId;if(priority.SelectedIndex!=3&&id>0&&id<4)id=0;coverId=id;
            rebuildingCover=true;
            try{coverStyle.SelectedIndex=-1;coverStyle.Items.Clear();coverIds=priority.SelectedIndex==3?new[]{0,1,2,3,4}:new[]{0,4};foreach(int choice in coverIds)coverStyle.Items.Add(coverNames[choice]);coverStyle.SelectedIndex=Array.IndexOf(coverIds,id);coverStyle.Invalidate();}
            finally{rebuildingCover=false;}
            whiteInfo.Text=light!=null?Path.GetFileName(light):mode.SelectedIndex!=0||priority.SelectedIndex==4?"自动黑白封面 · 随原图尺寸适配":coverId==0?"自动封面 · 按原图配色与尺寸生成花纹":"自动封面 · "+coverNames[coverId];UpdateHelp();
        }
        void UpdateHelp()
        {
            bool color=mode.SelectedIndex==0;coverStyleRow.Visible=coverHelp.Visible=processingRow.Visible=processingHelp.Visible=color;strengthRow.Visible=color&&priority.SelectedIndex!=3&&priority.SelectedIndex!=4;
            effectiveRow.Visible=color&&priority.SelectedIndex==2;strength.Enabled=!manualEffective||priority.SelectedIndex!=2;if(!manualEffective&&previewProfile!=null&&previewProfile.SourceColor){updatingEffective=true;try{effectiveStrength.Value=(decimal)Math.Round(previewProfile.EffectiveStrength*100,1);}finally{updatingEffective=false;}}
            modeHelp.Text=color?"彩色叠显保留区域颜色；白底封面与黑底原图仍可能互透。":"黑白显图自动调整灰度明暗；封面只使用黑白效果。";
            sizeHelp.Text=bound.SelectedIndex==3?"保留原尺寸导出；预览仍使用缩略图。":string.Format("最长边限制为 {0} 像素，保持宽高比；较小图片不放大。",new[]{1200,2048,3200}[bound.SelectedIndex]);
            string explanation=new[]{"均衡色彩：折中保留两张图片的色彩，白底和黑底都可能互透。","原图优先：优先保留标准黑底的原图色彩，白底封面可能透出原图。","原色优化：按图自动收紧透色并柔化细碎颜色；自动封面用花瓣与流线遮掩色块，原图颜色会更淡，仍可能有残影。","轮廓优先：使用统一色调减轻轮廓残影，会牺牲原图多种颜色；可选择封面预设配色。","二维码优化：白底保留灰度封面，黑底增强二维码对比；需要本工具生成的二维码 PNG。导入扫描自动尝试两种背景，平台转码仍可能影响效果。"}[priority.SelectedIndex];
            string summaryTemplate=L.T("原色优化：保留不同区域的颜色并弱化细碎色彩；有效色彩强度 {0}%。封面仍可能透出部分色彩。");using(var measure=new Label{Font=processingHelp.Font,AutoSize=false,UseMnemonic=false,MaximumSize=new Size(Math.Max(100,Body.ClientSize.Width-Body.Padding.Horizontal-8),0)}){measure.Text=L.T(explanation);int needed=measure.GetPreferredSize(new Size(measure.MaximumSize.Width,0)).Height;if(priority.SelectedIndex==2){measure.Text=string.Format(summaryTemplate,"100.0");needed=Math.Max(needed,measure.GetPreferredSize(new Size(measure.MaximumSize.Width,0)).Height);}processingHelp.Height=needed+4;}
            processingHelp.Text=priority.SelectedIndex==2&&previewProfile!=null&&previewProfile.SourceColor?AutoSummary(previewProfile,true):L.T(explanation);
            coverHelp.Text=priority.SelectedIndex==4?"二维码优化使用灰度封面；二维码只在标准黑底显现，封面不再直接显示码。":coverId==4?"使用自选图片作为封面；实际配色由处理方式调整，以预览为准。":coverId==0?(priority.SelectedIndex==2?"原色优化自动生成柔和花瓣与流线，遮掩残余色块；不复制原图轮廓。":"自动封面按原图配色和尺寸生成无文字花纹。"):"使用所选花纹配色生成封面，并将原图调整为相应统一色调。";
        }
        void ArrangePreviews()
        {
            if(arrangingPreviews||previews==null||whiteCard==null||blackCard==null||IsDisposed||Disposing)return;
            arrangingPreviews=true;
            try
            {
                bool stack=previews.ClientSize.Width<650;
                if(stack!=stackedPreviews)
                {
                    previews.SuspendLayout();previews.Controls.Remove(whiteCard);previews.Controls.Remove(blackCard);previews.ColumnStyles.Clear();previews.RowStyles.Clear();
                    previews.ColumnCount=stack?1:2;previews.RowCount=stack?2:1;
                    previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,stack?100:50));if(!stack)previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
                    previews.RowStyles.Add(new RowStyle(SizeType.Absolute));if(stack)previews.RowStyles.Add(new RowStyle(SizeType.Absolute));
                    previews.Controls.Add(whiteCard,0,0);previews.Controls.Add(blackCard,stack?0:1,stack?1:0);stackedPreviews=stack;previews.ResumeLayout(true);
                }
                whiteCard.Margin=stack?new Padding(0,0,0,8):new Padding(0,0,6,0);blackCard.Margin=stack?new Padding(0):new Padding(6,0,0,0);
                int width=Math.Max(100,previews.ClientSize.Width/(stack?1:2));
                int wh=whiteCard.GetPreferredSize(new Size(width-whiteCard.Margin.Horizontal,0)).Height+whiteCard.Margin.Vertical;
                int bh=blackCard.GetPreferredSize(new Size(width-blackCard.Margin.Horizontal,0)).Height+blackCard.Margin.Vertical;
                previews.RowStyles[0].Height=stack?wh:Math.Max(wh,bh);if(stack)previews.RowStyles[1].Height=bh;
                int height=stack?wh+bh:Math.Max(wh,bh);if(previews.Height!=height)previews.Height=height;
            }
            finally{arrangingPreviews=false;}
        }
        static Bitmap Generate(Image light,Image dark,int bound,bool color,int strength,int processing,int palette,CancellationToken token,out BackgroundRevealAuto.Profile p,double manualStrength=-1){if(!color||processing>=2)return BackgroundRevealAuto.Compose(light,dark,bound,color&&processing!=4,strength,token,out p,palette,color&&processing==2,manualStrength);p=null;if(light!=null)return BackgroundReveal.Compose(light,dark,bound,color,strength,token,processing==1);using(var generated=BackgroundRevealAuto.CreateCover(dark,bound,palette,token))return BackgroundReveal.Compose(generated,dark,bound,color,strength,token,processing==1);}
        static QrRegion OptimizeQr(Bitmap result,QrRegion input,Image light,Image dark,int bound,BackgroundRevealAuto.Profile profile,CancellationToken token,bool strict){if(input==null)throw new ArgumentException("二维码优化需要本工具生成的二维码 PNG；请勿先清理元数据。");using(var cover=light==null?BackgroundRevealAuto.CreateCover(dark,bound,-1,token):Codec.Fit(light,result.Width,result.Height,Color.White))return QrService.Optimize(result,input,cover,profile,token,strict);}
        static string AutoSummary(BackgroundRevealAuto.Profile p,bool color){if(!color)return L.T("自动优化：已按图片调整黑白明暗与封面花纹。");if(p.SourceColor)return string.Format(L.T("原色优化：保留不同区域的颜色并弱化细碎色彩；有效色彩强度 {0}%。封面仍可能透出部分色彩。"),(p.EffectiveStrength*100).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture));if(p.Theme)return L.T("自动优化：采用封面配色的统一色调，抑制轮廓残影；未保留全部原色。");return string.Format(L.T("自动优化：色彩保留强度 {0}%；已调整封面明暗。"),(p.Strength*100).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture));}
        static TableLayoutPanel Card(){var c=Ui.Column(new Padding(16,14,16,12));c.BackColor=Color.White;c.Margin=new Padding(0,0,0,14);return c;}
        static ImageCanvas View(string empty){return new ImageCanvas{Dock=DockStyle.Top,Height=300,Checker=false,EmptyText=empty,Margin=new Padding(0)};}
        void PickLight(){using(var d=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",Title="选择白底图片"})if(L.Show(d,FindForm())==DialogResult.OK){light=d.FileName;coverId=4;RebuildCoverChoices();SchedulePreview();}else RebuildCoverChoices();}
        void PickDark(){using(var d=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",Title="选择黑底图片",Multiselect=true})if(L.Show(d,FindForm())==DialogResult.OK)Add(d.FileNames);}
        void Add(string[] paths){foreach(string path in paths)if(files.Count<200&&File.Exists(path)&&!files.Contains(path))files.Add(path);FillQueue(files.Count>0?0:-1);}
        void FillQueue(int selected){queue.BeginUpdate();try{queue.Items.Clear();queue.Items.AddRange(files.Select(Path.GetFileName).Cast<object>().ToArray());queue.SelectedIndex=selected;}finally{queue.EndUpdate();}SchedulePreview();}
        void StopPreview(){ticket++;debounce.Stop();if(previewCancel!=null)previewCancel.Cancel();}
        void SchedulePreview(){StopPreview();if(IsDisposed||Disposing)return;UpdateHelp();debounce.Start();}
        void Display(Bitmap w,Bitmap b){whiteView.Image=w;blackView.Image=b;if(whiteImage!=null)whiteImage.Dispose();if(blackImage!=null)blackImage.Dispose();whiteImage=w;blackImage=b;}
        string completedPreview;
        string PreviewKey(){string path=queue.SelectedIndex<0?null:files[queue.SelectedIndex];return light+"|"+path+"|"+(path!=null&&File.Exists(path)?File.GetLastWriteTimeUtc(path).Ticks.ToString():"")+"|"+(light!=null&&File.Exists(light)?File.GetLastWriteTimeUtc(light).Ticks.ToString():"")+"|"+mode.SelectedIndex+"|"+strength.Value+"|"+priority.SelectedIndex+"|"+coverId+"|"+(manualEffective?effectiveStrength.Value.ToString():"auto");}
        async Task Preview()
        {
            if(Busy)return;int version=ticket;int selected=queue.SelectedIndex;if(selected<0){Display(null,null);info.Text="导入黑底图片后自动预览；封面可自动生成或自行选择。";return;}
            string key=PreviewKey();string wp=light,bp=files[selected];bool color=mode.SelectedIndex==0;int sat=(int)strength.Value;double manual=manualEffective?(double)effectiveStrength.Value:-1;int processing=priority.SelectedIndex,palette=coverId==4?0:coverId;var cancel=new CancellationTokenSource();previewCancel=cancel;Bitmap[] views=null;BackgroundRevealAuto.Profile optimized=null;
            try
            {
                views=await Task.Run(()=>{Bitmap w=null,b=null;try{using(var a=wp==null?null:Codec.Load(wp))using(var z=Codec.Load(bp))using(var png=Generate(a,z,640,color,sat,processing,palette,cancel.Token,out optimized,manual)){if(color&&processing==4)OptimizeQr(png,QrService.Read(bp),a,z,640,optimized,cancel.Token,false);w=DualBlend.Render(png,true,cancel.Token);b=DualBlend.Render(png,false,cancel.Token);}return new[]{w,b};}catch{if(w!=null)w.Dispose();if(b!=null)b.Dispose();throw;}});
                if(version!=ticket||IsDisposed||Disposing)return;Display(views[0],views[1]);views=null;info.Text="预览使用实际透明合成结果；导出尺寸由下方选项决定。";previewProfile=optimized;completedPreview=key;UpdateHelp();
            }
            catch(OperationCanceledException){}catch(Exception ex){if(version==ticket&&!IsDisposed){Display(null,null);info.Text="处理失败："+ex.Message;}}
            finally{if(views!=null)foreach(var v in views)v.Dispose();if(previewCancel==cancel)previewCancel=null;cancel.Dispose();}
        }
        async Task Export(bool all,bool showResult=true)
        {
            if(files.Count==0){Status.Text="导入黑底图片后自动预览；封面可自动生成或自行选择。";return;}if(!OutputFolder.Ready())return;StopPreview();
            var selected=all?files.ToArray():queue.SelectedIndex<0?new string[0]:new[]{files[queue.SelectedIndex]};string wp=light,dest=OutputFolder.Destination;bool color=mode.SelectedIndex==0;int processing=priority.SelectedIndex,palette=coverId==4?0:coverId;double manual=manualEffective?(double)effectiveStrength.Value:-1;int sat=(int)strength.Value,max=new[]{1200,2048,3200,0}[bound.SelectedIndex];
            await Run(token=>Task.Run(()=>{int done=0;var errors=new List<string>();var autoReports=new List<string>();using(var white=wp==null?null:Codec.Load(wp))foreach(string path in selected){token.ThrowIfCancellationRequested();try{BackgroundRevealAuto.Profile optimized;using(var black=Codec.Load(path))using(var result=Generate(white,black,max,color,sat,processing,palette,token,out optimized,manual)){var region=color&&processing==4?OptimizeQr(result,QrService.Read(path),white,black,max,optimized,token,true):null;var data=optimized==null?BackgroundReveal.Encode(result,color,sat,token):BackgroundRevealAuto.Encode(result,color,optimized,token);if(optimized!=null)autoReports.Add(Path.GetFileName(path)+": "+AutoSummary(optimized,color));data=QrService.Tag(data,region);token.ThrowIfCancellationRequested();Codec.SaveAtomic(ImageTools.Unique(dest,path,color?"_color_reveal":"_gray_reveal",".png"),data);}done++;}catch(OperationCanceledException){throw;}catch(Exception ex){errors.Add(Path.GetFileName(path)+": "+ex.Message);}}return string.Format(L.T("背景显图导出完成：成功 {0} 张，失败 {1} 张。"),done,errors.Count)+(errors.Count>0?"\n"+string.Join("\n",errors):"")+(autoReports.Count>0?"\n"+string.Join("\n",autoReports):"");}),showResult);
        }
        async Task Extract(){using(var d=new OpenFileDialog{Title="选择透明 PNG",Filter="PNG|*.png",Multiselect=true}){if(L.Show(d,FindForm())!=DialogResult.OK||!OutputFolder.Ready())return;var paths=d.FileNames;StopPreview();await Run(token=>Task.Run(()=>{foreach(string p in paths){token.ThrowIfCancellationRequested();DualBlend.RestorePair(p,OutputFolder.Destination,token);}return L.T("已导出白底和黑底显示图；原文件字节无法恢复。");}));}}
        public override void OnLeavePage(){StopPreview();}
        public override void OnEnterPage(){base.OnEnterPage();ArrangePreviews();if(whiteImage==null||PreviewKey()!=completedPreview)SchedulePreview();}
        protected override void Dispose(bool disposing){if(disposing){StopPreview();debounce.Dispose();whiteView.Image=null;blackView.Image=null;if(whiteImage!=null)whiteImage.Dispose();if(blackImage!=null)blackImage.Dispose();}base.Dispose(disposing);}
        public override void Demo(){light=null;info.Text="导入黑底图片后自动预览；封面可自动生成或自行选择。";}
#if SELF_TEST
        internal async Task CheckBehavior(string dir)
        {
            Directory.CreateDirectory(dir);string a=Path.Combine(dir,"light.png"),b=Path.Combine(dir,"dark.png");using(var w=new Bitmap(64,48))using(var k=new Bitmap(64,48)){using(var g=Graphics.FromImage(w))g.Clear(Color.Orange);using(var g=Graphics.FromImage(k))g.Clear(Color.RoyalBlue);Codec.SaveAtomic(a,Codec.StaticPng(w));Codec.SaveAtomic(b,Codec.StaticPng(k));}
            light=a;coverId=4;RebuildCoverChoices();whiteInfo.Text=Path.GetFileName(a);Add(new[]{b});mode.SelectedIndex=0;await Preview();if(whiteImage==null||blackImage==null)throw new Exception("Background page did not generate previews");
            if(!effectiveRow.Visible)throw new Exception("Manual strength controls missing");
            effectiveStrength.Value=70;StopPreview();await Preview();if(!manualEffective||previewProfile==null||Math.Abs(previewProfile.Strength-.70)>.001)throw new Exception("Manual effective strength ignored");
            ((Button)effectiveRow.Controls[2]).PerformClick();StopPreview();await Preview();if(manualEffective)throw new Exception("Automatic strength was not restored");
            priority.SelectedIndex=1;UpdateHelp();if(effectiveRow.Visible)throw new Exception("Manual strength shown for other method");priority.SelectedIndex=2;StopPreview();await Preview();
            Scroller.AutoScrollPosition=new Point(0,Math.Max(0,strengthRow.Top-50));Application.DoEvents();int scrollBefore=Scroller.VerticalScroll.Value,heightBefore=Body.Height;decimal priorStrength=strength.Value;for(int step=0;step<4;step++){strength.Value=priorStrength+step+1;StopPreview();await Preview();Application.DoEvents();if(Body.Height!=heightBefore||Scroller.VerticalScroll.Value!=scrollBefore||!processingHelp.Visible)throw new Exception("Strength update changed scroll position or explanation layout");}strength.Value=priorStrength;StopPreview();await Preview();Scroller.AutoScrollPosition=Point.Empty;
            var form=(MainForm)FindForm();Size originalSize=form.ClientSize;bool originalLanguage=L.English;
            try{foreach(bool en in new[]{false,true}){L.Set(en?"en":"zh");foreach(var size in new[]{new Size(900,700),new Size(1500,950)}){form.ClientSize=size;Application.DoEvents();ArrangePreviews();form.ValidateLayout();if(en&&L.Untranslated(new[]{modeHelp.Text,sizeHelp.Text,processingHelp.Text,processingResult.Text,coverHelp.Text}).Length!=0)throw new Exception("Dynamic explanation is not translated");if(previews.RowCount!=(stackedPreviews?2:1))throw new Exception("Preview retained unused rows");int lastBottom=Math.Max(whiteCard.Bottom,blackCard.Bottom);if(previews.Height-lastBottom>12)throw new Exception("Blank space below preview cards");if(Body.GetRow(previews)>=Body.GetRow(strengthRow))throw new Exception("Previews are not first");using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(dir,"page-"+(en?"en":"zh")+"-"+size.Width+".png"));}Scroller.AutoScrollPosition=new Point(0,info.Bottom);Application.DoEvents();form.ValidateLayout();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(dir,"settings-"+(en?"en":"zh")+"-"+size.Width+".png"));}Scroller.AutoScrollPosition=Point.Empty;Application.DoEvents();}}}finally{form.ClientSize=originalSize;L.Set(originalLanguage?"en":"zh");}
            string root=OutputFolder.Folder;OutputFolder.TestSetFolder(dir);try{await Export(true,false);await Export(false,false);if(Directory.GetFiles(OutputFolder.Destination,"*_color_reveal*.png").Length!=2)throw new Exception("Background export/collision handling failed");}finally{OutputFolder.TestSetFolder(root);}
            light=null;coverId=0;RebuildCoverChoices();await Preview();if(whiteImage==null)throw new Exception("Automatic cover preview failed");string autoRoot=OutputFolder.Folder;OutputFolder.TestSetFolder(Path.Combine(dir,"automatic"));try{await Export(true,false);if(Directory.GetFiles(OutputFolder.Destination,"*_color_reveal*.png").Length!=1)throw new Exception("Automatic cover export failed");}finally{OutputFolder.TestSetFolder(autoRoot);}priority.SelectedIndex=3;for(int i=0;i<4;i++){coverStyle.TestChoice(i);await Preview();if(whiteImage==null||blackImage==null)throw new Exception("Cover style preview failed");}priority.SelectedIndex=2;if(coverStyle.Items.Count!=2||coverId!=0)throw new Exception("Non-tint methods show preset cover colors");
            foreach(int method in new[]{0,1,2,3,4}){priority.SelectedIndex=method;if(coverStyle.Items.Count!=(method==3?5:2))throw new Exception("Cover choices did not follow processing");if(method!=2&&!processingHelp.Text.Contains(L.T(new[]{"均衡色彩：折中保留两张图片的色彩，白底和黑底都可能互透。","原图优先：优先保留标准黑底的原图色彩，白底封面可能透出原图。","原色优化：按图自动收紧透色并柔化细碎颜色；自动封面用花瓣与流线遮掩色块，原图颜色会更淡，仍可能有残影。","轮廓优先：使用统一色调减轻轮廓残影，会牺牲原图多种颜色；可选择封面预设配色。","二维码优化：白底保留灰度封面，黑底增强二维码对比；需要本工具生成的二维码 PNG。导入扫描自动尝试两种背景，平台转码仍可能影响效果。"}[method])))throw new Exception("Processing explanation is stale");}
            bound.SelectedIndex=3;if(sizeHelp.Text!=L.T("保留原尺寸导出；预览仍使用缩略图。"))throw new Exception("Size explanation did not update");bound.SelectedIndex=0;light=a;coverId=4;RebuildCoverChoices();priority.SelectedIndex=2;if(light!=a||coverId!=4||coverStyle.SelectedIndex!=1)throw new Exception("Filtering changed a custom cover");
            using(var popup=new SoftCombo{VisibleRows=3}){popup.Items.AddRange(new object[]{"A","B"});Body.Controls.Add(popup);popup.SelectedIndex=0;popup.SelectedIndexChanged+=delegate{if(popup.PopupVisible)throw new Exception("Picker callback ran before menu closed");};popup.TestChoice(1);popup.VisibleRows=0;popup.TestChoice(0);Body.Controls.Remove(popup);}
            mode.SelectedIndex=1;if(modeHelp.Text!=L.T("黑白显图自动调整灰度明暗；封面只使用黑白效果。"))throw new Exception("Mode explanation did not update");await Preview();if(coverStyleRow.Visible||processingRow.Visible||strengthRow.Visible)throw new Exception("Grayscale shows color-only controls");if(blackImage.GetPixel(0,0).R!=blackImage.GetPixel(0,0).G)throw new Exception("Grayscale preview contains color");StopPreview();
            string qrPath=Path.Combine(dir,"qr-source.png"),qrUrl="https://www.pixiv.net/artworks/123456789";QrRegion qr;using(var art=SelfTest.Art(900,700,true))using(var image=QrService.Compose(art,qrUrl,1024,60,50,50,null,out qr))Codec.SaveAtomic(qrPath,QrService.Tag(Codec.StaticPng(image),qr));
            light=null;coverId=0;files.Clear();Add(new[]{qrPath});mode.SelectedIndex=0;priority.SelectedIndex=4;await Preview();if(whiteImage==null||QrService.Scan(whiteImage)!=null||QrService.Scan(blackImage)!=qrUrl)throw new Exception("QR optimization preview did not hide/reveal the code");
            string qrRoot=OutputFolder.Folder;OutputFolder.TestSetFolder(Path.Combine(dir,"qr-optimized"));try{await Export(true,false);string[] outputs=Directory.GetFiles(OutputFolder.Destination,"*.png");if(outputs.Length!=1)throw new Exception("QR optimization page export failed");using(var output=Codec.Load(outputs[0]))using(var cover=DualBlend.Render(output,true,CancellationToken.None)){if(QrService.Read(outputs[0])==null||QrService.Scan(output)!=qrUrl||QrService.Scan(cover)!=null)throw new Exception("QR optimization export lost linkage or exposed cover code");}}finally{OutputFolder.TestSetFolder(qrRoot);}
            files.Clear();Add(new[]{b});await Preview();if(whiteImage!=null||blackImage!=null)throw new Exception("QR optimization silently accepted an untagged image");priority.SelectedIndex=2;await Preview();if(whiteImage==null||blackImage==null)throw new Exception("Ordinary processing did not recover after QR error");StopPreview();
        }
#endif
    }
}
