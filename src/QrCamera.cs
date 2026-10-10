using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQImageSwitch
{
    public sealed class QrCamera : Form
    {
        readonly PictureBox preview=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Black};
        readonly Label status=new Label{Dock=DockStyle.Fill,AutoSize=false,UseMnemonic=false,Padding=new Padding(8)};
        readonly SoftCombo devices=new SoftCombo{Dock=DockStyle.Top,VisibleRows=3,Tag="raw"};
        readonly Timer timer=new Timer{Interval=160};
        CameraCapture capture;bool closed,decoding,starting;int ticket;
        DateTime lastFrame;
        public string Result;
        public QrCamera()
        {
            Text=L.T("相机扫描");Size=new Size(740,630);MinimumSize=new Size(460,400);StartPosition=FormStartPosition.CenterParent;
            var bottom=new Panel{Dock=DockStyle.Bottom,Height=150};
            var buttons=Ui.Flow(Ui.Button("重新连接",async delegate{await Start();}),Ui.Button("相机权限设置",delegate{try{Process.Start("ms-settings:privacy-webcam");}catch(Exception ex){status.Text=L.T("无法打开相机设置：")+ex.Message;}}),Ui.Button("关闭相机",delegate{Close();}));
            buttons.Dock=DockStyle.Bottom;bottom.Controls.Add(status);bottom.Controls.Add(buttons);
            Controls.Add(preview);Controls.Add(bottom);Controls.Add(devices);
            devices.SelectedIndexChanged+=async delegate{await Start();};
            timer.Tick+=async delegate{await Poll();};
            Shown+=async delegate
            {
                status.Text=L.T("正在查找相机…");
                try
                {
                    var list=await Task.Run(()=>CameraCapture.Devices());if(closed)return;
                    devices.Items.AddRange(list);if(list.Length==0)status.Text=L.T("未检测到摄像头。请连接相机并检查 Windows 相机权限，或导入图片扫描。");else devices.SelectedIndex=0;
                }
                catch(Exception ex){if(!closed)status.Text=Failure(ex);}
            };
        }
        async Task Start()
        {
            int current=++ticket;timer.Stop();devices.Enabled=false;starting=true;
            try
            {
                var old=capture;capture=null;
                if(old!=null){old.Dispose();await old.Completion;}
                if(closed||current!=ticket)return;
                var image=preview.Image;preview.Image=null;if(image!=null)image.Dispose();
                if(devices.SelectedIndex<0)return;
                status.Text=L.T("正在连接相机…");
                capture=new CameraCapture((CameraCapture.Device)devices.Items[devices.SelectedIndex]);
                lastFrame=DateTime.UtcNow;timer.Start();
            }
            catch(Exception ex){if(!closed&&current==ticket)status.Text=Failure(ex);}
            finally{if(!closed&&current==ticket){devices.Enabled=true;starting=false;}}
        }
        async Task Poll()
        {
            if(closed||starting||capture==null||decoding)return;
            var session=capture;int current=ticket;var error=session.Error;
            if(error!=null){timer.Stop();status.Text=Failure(error);session.Dispose();return;}
            var image=session.TakeFrame();
            if(image==null)
            {
                if((DateTime.UtcNow-lastFrame).TotalSeconds>10)status.Text=L.T("相机未返回画面。请检查隐私挡板、USB 连接或权限，尝试重新连接或更换相机。");
                session.Request();return;
            }
            lastFrame=DateTime.UtcNow;decoding=true;
            try
            {
                var old=preview.Image;preview.Image=image;if(old!=null)old.Dispose();image=null;
                status.Text=L.T("将二维码对准相机；识别后返回结果，不自动打开网站。");
                using(var scan=(Bitmap)preview.Image.Clone())
                {
                    string found=await Task.Run(()=>QrService.Scan(scan));
                    if(!closed&&current==ticket&&found!=null){Result=found;DialogResult=DialogResult.OK;Close();}
                }
            }
            catch(Exception ex){if(!closed&&current==ticket)status.Text=L.T("画面识别失败，请重试：")+ex.Message;}
            finally{if(image!=null)image.Dispose();decoding=false;if(!closed&&current==ticket)session.Request();}
        }
        internal static string Failure(Exception error)
        {
            string reason;int code=error.HResult;
            if(error is DllNotFoundException||error is EntryPointNotFoundException)reason=L.T("系统缺少媒体组件。Windows N 版请安装 Media Feature Pack，再重新连接相机。");
            else if(code==unchecked((int)0x80070005))reason=L.T("相机访问被拒绝。请允许相机与桌面应用访问相机，然后重新连接。");
            else if(code==unchecked((int)0x800700aa))reason=L.T("相机设备暂不可用。可能被占用、断开或被驱动锁定；请重新连接。");
            else if(code==unchecked((int)0xc00d36b4)||code==unchecked((int)0xc00d5212))reason=L.T("相机输出格式或解码器不受支持。请更新相机驱动或选择其他相机。");
            else reason=L.T("相机启动或取帧失败。请检查权限、连接和驱动，或更换相机、导入图片扫描。");
            return reason+Environment.NewLine+L.T("错误代码：")+"0x"+code.ToString("X8");
        }
        void Stop(){closed=true;ticket++;timer.Stop();var session=capture;capture=null;if(session!=null)session.Dispose();}
        protected override void OnFormClosed(FormClosedEventArgs e){Stop();base.OnFormClosed(e);}
        protected override void Dispose(bool disposing){if(disposing){Stop();timer.Dispose();if(preview.Image!=null){preview.Image.Dispose();preview.Image=null;}}base.Dispose(disposing);}
    }
}

