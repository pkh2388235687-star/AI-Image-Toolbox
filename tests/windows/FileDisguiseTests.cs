using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace QQImageSwitch
{
    static class FileDisguiseTests
    {
        sealed class Notify : IProgress<FileProgress>{public Action<FileProgress> Action;public void Report(FileProgress p){Action(p);}}
        static void Reject(Action action,string name,List<string> report){try{action();}catch(Exception e){if(e is InvalidDataException||e is IOException||e is ArgumentException||e is OperationCanceledException){report.Add("PASS: "+name+" ("+e.GetType().Name+")");return;}throw;}throw new Exception("Expected rejection: "+name);}
        public static void Run(string folder)
        {
            string reportRoot=folder;folder=Path.Combine(folder,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var report=new List<string>();var token=CancellationToken.None;string cover=Path.Combine(folder,"原封面.jpg"),input=Path.Combine(folder,"中文原文件.zip"),jpg=Path.Combine(folder,"中文封装.jpg");
            using(var b=new Bitmap(80,60)){using(var g=Graphics.FromImage(b))g.Clear(Color.LightPink);b.Save(cover,System.Drawing.Imaging.ImageFormat.Jpeg);}
            byte[] empty={80,75,5,6,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};File.WriteAllBytes(input,empty);var before=File.ReadAllBytes(cover);
            var info=FileDisguise.Pack(cover,input,jpg,token);if(info.Length!=22||info.CoverConverted||!before.SequenceEqual(File.ReadAllBytes(cover)))throw new Exception("Input mutated");
            using(var image=Codec.Decode(FileDisguise.Preview(jpg)))if(image.Width!=80||image.Height!=60)throw new Exception("Cover dimensions changed");
            FileDisguise.Check(jpg,token);string restored=FileDisguise.Restore(jpg,folder,token),second=FileDisguise.Restore(jpg,folder,token);if(restored==second||!File.ReadAllBytes(restored).SequenceEqual(empty))throw new Exception("Restore identity/collision failed");report.Add("PASS: Chinese paths, true JPEG decode, unmodified cover and input, exact restore, unique outputs");
            var receipt=QQRoundTrip.Verify(jpg,jpg,"local test","local test","small",false,token);if(!receipt.Passed||receipt.UserConfirmedActualQQ)throw new Exception("Local copy incorrectly marked as actual QQ roundtrip");report.Add("PASS: local comparison is never marked as actual QQ transmission");
            Reject(()=>FileDisguise.Pack(cover,input,jpg,token),"existing output protected",report);
            string bad=Path.Combine(folder,"损坏.jpg");byte[] original=File.ReadAllBytes(jpg),alter=(byte[])original.Clone();alter[alter.Length-1]^=1;File.WriteAllBytes(bad,alter);Reject(()=>FileDisguise.Check(bad,token),"tampered payload",report);Reject(()=>FileDisguise.Restore(bad,folder,token),"tampered restore leaves no final output",report);
            File.WriteAllBytes(bad,original.Take(original.Length-1).ToArray());Reject(()=>FileDisguise.Check(bad,token),"truncated file",report);
            alter=(byte[])original.Clone();Array.Copy(BitConverter.GetBytes(long.MaxValue),0,alter,26,8);File.WriteAllBytes(bad,alter);Reject(()=>FileDisguise.Check(bad,token),"invalid offset",report);
            alter=(byte[])original.Clone();byte[] traversal=System.Text.Encoding.UTF8.GetBytes("../bad.zip"+new string('a',BitConverter.ToUInt16(alter,16)-10));Array.Copy(traversal,0,alter,66,traversal.Length);File.WriteAllBytes(bad,alter);Reject(()=>FileDisguise.Restore(bad,folder,token),"path traversal name",report);
            alter=(byte[])original.Clone();alter[6]=(byte)'?';File.WriteAllBytes(bad,alter);Reject(()=>FileDisguise.Check(bad,token),"metadata removed",report);
            var space=FileDisguise.AvailableSpace;try{FileDisguise.AvailableSpace=d=>0;Reject(()=>FileDisguise.Pack(cover,input,Path.Combine(folder,"不足.jpg"),token),"disk space preflight",report);}finally{FileDisguise.AvailableSpace=space;}
            string invalid=Path.Combine(folder,"错误.mp4");File.WriteAllBytes(invalid,empty);Reject(()=>FileDisguise.Pack(cover,invalid,Path.Combine(folder,"错误.jpg"),token),"wrong file signature",report);
            string denied=Path.Combine(folder,"受限.zip");File.WriteAllBytes(denied,empty);using(var locked=new FileStream(denied,FileMode.Open,FileAccess.Read,FileShare.None))Reject(()=>FileDisguise.Pack(cover,denied,Path.Combine(folder,"受限.jpg"),token),"read-restricted file",report);
            string split=Path.Combine(folder,"archive.part1.rar");File.WriteAllBytes(split,empty);Reject(()=>FileDisguise.Pack(cover,split,Path.Combine(folder,"分卷RAR.jpg"),token),"multipart RAR name",report);
            empty[4]=1;File.WriteAllBytes(input,empty);Reject(()=>FileDisguise.Pack(cover,input,Path.Combine(folder,"分卷.jpg"),token),"multipart ZIP",report);empty[4]=0;File.WriteAllBytes(input,empty);
            using(var c=new CancellationTokenSource()){c.Cancel();Reject(()=>FileDisguise.Pack(cover,input,Path.Combine(folder,"取消.jpg"),c.Token),"cancel before task",report);}
            // Large sparse MP4 structure exercises 64-bit seeks without allocating large buffers.
            string large=Path.Combine(folder,"长度超过2GB.mp4");using(var s=new FileStream(large,FileMode.Create,FileAccess.ReadWrite)){byte[] h={0,0,0,16,102,116,121,112,105,115,111,109,0,0,0,0,0,0,0,8,109,111,111,118,0,0,0,1,109,100,97,116,0,0,0,0,128,0,0,32};s.Write(h,0,h.Length);s.SetLength(24L+0x80000020L+8);s.Position=s.Length-8;s.Write(new byte[]{0,0,0,8,102,114,101,101},0,8);s.Position=0;FileDisguise.Validate(s,1,token);}
            using(var c=new CancellationTokenSource()){var p=new Notify{Action=x=>{if(x.Completed>=FileDisguise.BufferSize)c.Cancel();}};Reject(()=>FileDisguise.Pack(cover,large,Path.Combine(folder,"中途取消.jpg"),c.Token,p),"cancel during streaming and cleanup",report);}
            FileDisguise.Range(0x80000020L,0x80000020L,0x100000040L);Reject(()=>FileDisguise.Range(long.MaxValue-1,4,long.MaxValue),"overflow-safe range",report);report.Add("PASS: actual file length beyond 2 GiB, extended MP4 box, 64-bit length/offset range; payload copy cancelled after first MiB (not a full 2 GiB roundtrip)");
            if(Directory.GetFiles(folder,"*.tmp").Length>0||File.Exists(Path.Combine(folder,"中途取消.jpg")))throw new Exception("Temporary file left after failure");report.Add("PASS: cancellation/failure cleanup, no existing output overwritten");File.WriteAllLines(Path.Combine(reportRoot,"file-core-tests.txt"),report);File.Delete(large);
        }
    }
}
