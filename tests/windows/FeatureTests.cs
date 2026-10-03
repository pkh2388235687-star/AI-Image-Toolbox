using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace QQImageSwitch
{
    static class FeatureTests
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        public static void Run(string folder)
        {
            folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);string run=Path.Combine(folder,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);var report=new List<string>();
            var token=CancellationToken.None;string blue=Path.Combine(run,"封面 蓝.jpg"),red=Path.Combine(run,"封面 红.jpg"),zip=Path.Combine(run,"中文文件.zip"),movie=Path.Combine(run,"视频.mp4");
            using(var b=new Bitmap(160,100)){using(var g=Graphics.FromImage(b))g.Clear(Color.Blue);b.Save(blue,System.Drawing.Imaging.ImageFormat.Jpeg);using(var g=Graphics.FromImage(b))g.Clear(Color.Red);b.Save(red,System.Drawing.Imaging.ImageFormat.Jpeg);}
            byte[] zipBytes={80,75,5,6,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};File.WriteAllBytes(zip,zipBytes);
            // Structurally valid tiny MP4, for name/streaming tests; real decodable videos are separately verified.
            byte[] movieBytes={0,0,0,16,102,116,121,112,105,115,111,109,0,0,0,0,0,0,0,8,109,111,111,118,0,0,0,8,109,100,97,116};File.WriteAllBytes(movie,movieBytes);
            byte[] before=File.ReadAllBytes(blue);var list=new[]{new FileBatchItem{Path=zip,CoverPath=red},new FileBatchItem{Path=movie}};
            string outputs=Path.Combine(run,"outputs");var text=new CoverTextOptions{Text="中文 cover",Position=3,X=25,Y=60};var r=FileBatch.Export(list,blue,outputs,true,text,token,null);
            Require(r.Errors.Count==0&&r.Files.Count==2,"Batch packing failed");Require(Path.GetFileName(r.Files[0])=="中文文件_a2i.jpg"&&Path.GetFileName(r.Files[1])=="视频_v2i.jpg","Filename markers missing");
            foreach(string file in r.Files){var info=FileDisguise.Check(file,token);string restored=FileDisguise.Restore(file,Path.Combine(run,"restored"),token);byte[] expected=info.Extension==".mp4"?movieBytes:zipBytes;Require(Path.GetFileName(restored)==info.Name&&File.ReadAllBytes(restored).SequenceEqual(expected),"Batch restore changed original name or bytes");}
            using(var b=Codec.Decode(FileDisguise.Preview(r.Files[0])))Require(b.GetPixel(20,20).R>200&&b.GetPixel(20,20).B<40,"Individual cover lost");
            using(var b=Codec.Decode(FileDisguise.Preview(r.Files[1])))Require(b.GetPixel(20,20).B>200&&b.GetPixel(20,20).R<40,"Shared cover lost");
            var again=FileBatch.Export(list,blue,outputs,true,text,token,null);Require(again.Files.Count==2&&again.Files.All(f=>Path.GetFileNameWithoutExtension(f).EndsWith("_2"))&&File.ReadAllBytes(blue).SequenceEqual(before),"Collision or original cover protection failed");
            var failures=FileBatch.Export(new[]{new FileBatchItem{Path=Path.Combine(run,"不存在.zip")},new FileBatchItem{Path=zip}},blue,outputs,false,new CoverTextOptions(),token,null);Require(failures.Errors.Count==1&&failures.Files.Count==1,"Batch did not continue after failed item");
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();r=FileBatch.Export(list,blue,outputs,true,text,cancel.Token,null);Require(r.Cancelled&&r.Files.Count==0,"Batch cancellation wrote outputs");}
            Require(Directory.GetFiles(outputs,"*.tmp").Length==0,"Batch left a temporary file");report.Add("PASS: v2i/a2i filenames, separate/shared covers, text/numbering, byte-identical batch restore, collision protection, failed item continuation, cancellation, original cover unchanged.");
            string longInput=Path.Combine(run,new string('长',80)+".zip"),longJpg=Path.Combine(run,"长文件名.jpg");File.WriteAllBytes(longInput,zipBytes);FileDisguise.Pack(blue,longInput,longJpg,token);string longRestored=FileDisguise.Restore(longJpg,Path.Combine(run,"r"),token);Require(Path.GetFileName(longRestored)==Path.GetFileName(longInput),"Long restored filename was shortened");
            // Dynamic labels translate; editable user text and raw prompt/JSON are never translated.
            L.Set("zh");using(var root=new Panel())using(var caption=Ui.Text("正面 tag"))using(var input=new SoftInput{Text="马赛克"})using(var raw=new TextBox{Text="正面 tag",ReadOnly=true,Tag="raw"}){root.Controls.Add(caption);root.Controls.Add(input);root.Controls.Add(raw);L.Bind(root);L.Set("en");Require(caption.Text=="Positive tags"&&input.Text=="马赛克"&&raw.Text=="正面 tag","Localization altered raw values");caption.Text="已导入 12 个文件，共 23 个。";Require(caption.Text=="Imported 12 files; 23 files in the queue.","Dynamic translation failed");L.Set("zh");Require(caption.Text=="已导入 12 个文件，共 23 个。","Chinese caption not restored");}
            L.Set("en");Require(L.T("种子 Seed  [#3 KSampler]")=="Seed  [#3 KSampler]","Parameter caption translation failed");Require(L.T("处理失败：保存位置剩余空间不足。")=="Processing failed: Not enough free space at the save location.","Nested error translation failed");L.Set("zh");
            string settings=Path.Combine(run,"preferences.txt");var prefs=new ToolPreferences(settings){Language="en"};prefs.Save();Require(new ToolPreferences(settings).Language=="en","Language not remembered");File.WriteAllText(settings+".options","language=bad");Require(new ToolPreferences(settings).Language=="zh","Invalid language not handled");report.Add("PASS: live dynamic labels and nested errors translate; prompt/JSON/editable text remain byte-for-byte unchanged; remembered language and invalid preference fallback.");
            using(var form=new Form{ClientSize=new Size(520,420)})using(var parent=new SoftPanel{Dock=DockStyle.Fill,BackColor=SoftTheme.Background})using(var card=new SoftTable{BackColor=Color.White,Padding=new Padding(16),Location=new Point(30,30),Size=new Size(170,120)})
            {
                form.Controls.Add(parent);parent.Controls.Add(card);form.Show();Application.DoEvents();card.Visible=false;using(var background=new Bitmap(parent.Width,parent.Height)){parent.DrawToBitmap(background,parent.ClientRectangle);card.Visible=true;Application.DoEvents();using(var frame=new Bitmap(parent.Width,parent.Height)){parent.DrawToBitmap(frame,parent.ClientRectangle);Require(frame.GetPixel(card.Left,card.Top).ToArgb()==background.GetPixel(card.Left,card.Top).ToArgb(),"Rounded corner used a mismatched local background");}
                    Rectangle invalidated=Rectangle.Empty;parent.Invalidated+=delegate(object sender,InvalidateEventArgs e){invalidated=Rectangle.Union(invalidated,e.InvalidRect);};var old=card.Bounds;card.Location=new Point(260,200);Application.DoEvents();Require(invalidated.Contains(old),"Previous card area was not invalidated");using(var frame=new Bitmap(parent.Width,parent.Height)){parent.DrawToBitmap(frame,parent.ClientRectangle);Require(frame.GetPixel(35,35).ToArgb()==background.GetPixel(35,35).ToArgb(),"Old corner residual after moving");}
                    card.Size=new Size(90,65);Application.DoEvents();using(var frame=new Bitmap(parent.Width,parent.Height)){parent.DrawToBitmap(frame,parent.ClientRectangle);Require(frame.GetPixel(415,305).ToArgb()==background.GetPixel(415,305).ToArgb(),"Old corner residual after shrinking");frame.Save(Path.Combine(folder,"rounded-corners.png"),System.Drawing.Imaging.ImageFormat.Png);}}
                form.Close();
            }
            report.Add("PASS: rounded corner pixels match parent gradient; previous bounds invalidated on movement; no residual after moving or shrinking.");File.WriteAllLines(Path.Combine(folder,"feature-tests.txt"),report);
        }
    }
}
