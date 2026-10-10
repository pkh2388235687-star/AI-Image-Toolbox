using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQImageSwitch
{
    public sealed class QQReceipt
    {
        public bool Passed,UserConfirmedActualQQ;
        public string TimeUtc,SenderVersion,ReceiverVersion,SendMode,SizeClass,Extension,OriginalName,PayloadSha256,SentJpgSha256,ReceivedJpgSha256,ExecutableSha256;
        public long PayloadLength,SentLength,ReceivedLength;
    }
    public static class QQRoundTrip
    {
        public static string Hash(string path,CancellationToken token)
        {using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileDisguise.BufferSize))using(var h=SHA256.Create()){byte[] b=new byte[FileDisguise.BufferSize];int n;while((n=s.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();h.TransformBlock(b,0,n,null,0);}token.ThrowIfCancellationRequested();h.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(h.Hash).Replace("-","").ToLowerInvariant();}}
        public static QQReceipt Verify(string sent,string received,string sender,string receiver,string sizeClass,bool confirmed,CancellationToken token,IProgress<FileProgress> progress=null)
        {
            var a=PackagingPrototype.Check(sent,token);var b=PackagingPrototype.Check(received,token);
            if(a.Sha256!=b.Sha256||a.Length!=b.Length||a.Name!=b.Name||a.Extension!=b.Extension)throw new InvalidDataException("接收文件与发送样本不一致，不能标记通过。");
            return new QQReceipt {Passed=true,UserConfirmedActualQQ=confirmed,TimeUtc=DateTime.UtcNow.ToString("o"),SenderVersion=sender,ReceiverVersion=receiver,SendMode="QQ 图片选择器勾选原图；接收方查看并保存原图",SizeClass=sizeClass,Extension=a.Extension,OriginalName=a.Name,PayloadLength=a.Length,PayloadSha256=a.Sha256,SentLength=new FileInfo(sent).Length,ReceivedLength=new FileInfo(received).Length,SentJpgSha256=Hash(sent,token),ReceivedJpgSha256=Hash(received,token),ExecutableSha256=Hash(typeof(QQRoundTrip).Assembly.Location,token)};
        }
    }
    class QQRoundTripForm : Form
    {
        readonly SoftInput sent,received,sender,receiver;readonly SoftCombo size;readonly SoftCheck actual;readonly Label status;readonly Button run,cancel;CancellationTokenSource cancellation;
        public QQRoundTripForm(string folder)
        {
            Ui.Configure(this,"校验 QQ 原图",new Size(780,650));var scroller=new SoftPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(20)};Controls.Add(scroller);var body=Ui.Column(new Padding(0));scroller.Controls.Add(body);
            Ui.Add(body,Ui.Text("校验发送与接收文件",18,true));Ui.Add(body,Ui.Text("先发小样本，收到的原图通过后再测试较大文件。这里只记录你实际完成的往返测试，不会自动发送消息。",9));
            sent=Input(body,"发送前的文件伪装图片样本",true);received=Input(body,"接收方保存的文件伪装原图",true);sender=Input(body,"发送端 QQ 客户端及版本（例如 Windows QQ 9.x）",false);receiver=Input(body,"接收端 QQ 客户端及版本",false);
            size=new SoftCombo{Width=170};size.Items.AddRange(new object[]{"小样本","较大样本"});size.SelectedIndex=0;Ui.Add(body,Ui.Flow(Ui.Text("本次测试"),size));
            actual=new SoftCheck{Text="我已实际通过 QQ 勾选原图发送，接收方查看并保存原图",AutoSize=true,Margin=new Padding(0,8,0,12)};Ui.Add(body,actual);
            status=Ui.Text("通过后保存校验报告，MP4、ZIP、RAR、7Z 分别测试。纯拼接没有原文件校验记录，请比较完整图片及提取文件的哈希。",9);Ui.Add(body,status);
            run=Ui.Button("开始校验",async delegate
            {
                if(cancellation!=null)return;if(!actual.Checked||string.IsNullOrWhiteSpace(sender.Text)||string.IsNullOrWhiteSpace(receiver.Text)){status.Text="请填写双方客户端版本，并确认实际 QQ 原图往返。";return;}
                string a=sent.Text,b=received.Text,sv=sender.Text,rv=receiver.Text,variant=size.SelectedIndex==0?"small":"large";cancellation=new CancellationTokenSource();run.Enabled=false;cancel.Visible=true;
                try{var p=new Progress<FileProgress>(v=>{if(!IsDisposed)status.Text=v.Stage+" · "+v.Percent+"%";});var token=cancellation.Token;var receipt=await Task.Run(()=>QQRoundTrip.Verify(a,b,sv,rv,variant,true,token,p),token);
                    string output=ImageTools.Unique(folder,"QQ校验_"+receipt.Extension.TrimStart('.')+"_"+variant+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"),"",".json");Codec.SaveAtomic(output,System.Text.Encoding.UTF8.GetBytes(Inspection.Json().Serialize(receipt)));status.Text="通过：原文件 SHA-256 一致。报告已保存。";using(var result=new ResultForm("校验通过",output+"\n只有这组客户端与发送方式已验证；请继续测试其他类型与较大样本。",folder))L.Show(result,this);}
                catch(OperationCanceledException){status.Text="校验已取消。";}catch(Exception ex){status.Text="未通过："+ex.Message;}finally{cancellation.Dispose();cancellation=null;run.Enabled=true;cancel.Visible=false;}
            },true);cancel=Ui.Button("取消校验",delegate{if(cancellation!=null)cancellation.Cancel();});cancel.Visible=false;Ui.Add(body,Ui.Flow(run,cancel));
            scroller.SizeChanged+=delegate{body.Width=Math.Max(200,scroller.ClientSize.Width-50);foreach(Control c in body.Controls)if(c is Label)c.MaximumSize=new Size(Math.Max(150,body.Width-10),0);};
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(cancellation!=null){cancellation.Cancel();e.Cancel=true;status.Text="正在停止，请稍后关闭。";}};
        }
        SoftInput Input(TableLayoutPanel body,string title,bool file)
        {
            Ui.Add(body,Ui.Text(title,9));var input=new SoftInput{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12)};
            if(!file)Ui.Add(body,input);else{var row=new TableLayoutPanel{ColumnCount=2,AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0)};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));row.Controls.Add(input,0,0);row.Controls.Add(Ui.Button("选择…",delegate{using(var d=new OpenFileDialog{Filter="文件伪装图片|*.jpg;*.jpeg;*.png"})if(L.Show(d,this)==DialogResult.OK)input.Text=d.FileName;}),1,0);Ui.Add(body,row);}return input;
        }
    }
}
