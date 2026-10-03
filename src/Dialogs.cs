using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace QQImageSwitch
{
    class PreviewForm : Form
    {
        public PreviewForm(byte[] bytes)
        {
            Ui.Configure(this,"当前配对 · 实际生成效果",new Size(780,660));
            Bitmap cover=Codec.Decode(bytes),hidden=Codec.Decode(Codec.Restore(bytes));
            var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(18)};Controls.Add(scroll);
            var layout=Ui.Column(new Padding(0));scroll.Controls.Add(layout);
            var canvas=new ImageCanvas {Dock=DockStyle.Top,Height=430,MinimumSize=new Size(0,280),Image=cover,Cursor=Cursors.Default,Margin=new Padding(0,0,0,12)};
            Ui.Add(layout,Ui.Flow(Ui.Button("聊天封面（含编号）",delegate{canvas.Image=cover;canvas.Invalidate();}),
                Ui.Button("点击原图后显示",delegate{canvas.Image=hidden;canvas.Invalidate();})));
            Ui.Add(layout,canvas);
            var note=Ui.Text("从实际生成的 PNG 读取两张图。尺寸："+hidden.Width+" × "+hidden.Height+"，文件："+(bytes.Length/1048576.0).ToString("0.00")+" MB。\nQQ 的实际显示效果仍需发送测试。",9);Ui.Add(layout,note);
            EventHandler resize=delegate{layout.Width=Math.Max(120,scroll.ClientSize.Width-scroll.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth);note.MaximumSize=new Size(Math.Max(120,scroll.ClientSize.Width-60),0);};scroll.ClientSizeChanged+=resize;
            canvas.FollowViewport(scroll,.78,280,1000);Shown+=resize;resize(null,EventArgs.Empty);
            FormClosed+=delegate{canvas.Image=null;cover.Dispose();hidden.Dispose();};
        }
    }
    class ResultForm : Form
    {
        public ResultForm(string title,string message,string folder)
        {
            Ui.Configure(this,title,new Size(740,390));
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(20)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            layout.Controls.Add(new TextBox {Text=message,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,
                BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle,WordWrap=true,Margin=new Padding(0,0,0,14)},0,0);
            var buttons=Ui.Flow();
            if(folder!=null)buttons.Controls.Add(Ui.Button("打开输出文件夹",delegate{Process.Start("explorer.exe","\""+folder+"\"");}));
            var done=Ui.Button("完成",delegate{Close();},true);buttons.Controls.Add(done);layout.Controls.Add(buttons,0,1);AcceptButton=done;
        }
    }
    class SavedForm : Form
    {
        public SavedForm(string path)
        {
            Ui.Configure(this,"文件已保存",new Size(720,380));
            var host=new Panel {AutoScroll=true,Dock=DockStyle.Fill,Padding=new Padding(20)};Controls.Add(host);
            var content=Ui.Column(new Padding(0));host.Controls.Add(content);
            Ui.Add(content,Ui.Text("文件已保存",17,true));
            Ui.Add(content,new TextBox {Text=path,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Height=65,Dock=DockStyle.Top,Margin=new Padding(0,0,0,16)});
            var note=Ui.Text("在 QQ 图片选择器勾选「原图」发送。\n重新截图、复制位图或转成 JPG 可能丢失隐藏图。",10);Ui.Add(content,note);
            var copy=Ui.Button("复制文件到剪贴板",delegate{});
            copy.Click+=delegate{var files=new System.Collections.Specialized.StringCollection();files.Add(path);Clipboard.SetFileDropList(files);copy.Text="已复制文件";};
            var done=Ui.Button("完成",delegate{Close();},true);
            Ui.Add(content,Ui.Flow(Ui.Button("在文件夹中显示",delegate{Process.Start("explorer.exe","/select,\""+path+"\"");}),copy,done));AcceptButton=done;
            host.ClientSizeChanged+=delegate{note.MaximumSize=new Size(Math.Max(120,host.ClientSize.Width-60),0);};
        }
    }
}
