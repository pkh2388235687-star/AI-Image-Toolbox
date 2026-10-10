using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace QQImageSwitch
{
    public sealed class HiddenFileInfo
    {
        public string Name,Extension,Sha256,StoredSha256;
        public long Length,Offset;
        public bool CoverConverted,ZipCompatible;
    }
    public sealed class FileProgress
    {
        public string Stage;public long Completed,Total;
        public int Percent {get{return Total<=0?0:(int)Math.Min(100,Completed/(double)Total*100);}}
    }
    public static class FileDisguise
    {
        public const int BufferSize=1024*1024,CoverLimit=512*1024;
        static readonly byte[] Magic=Encoding.ASCII.GetBytes("AIPTFILE");
        static readonly string[] Extensions={"", ".mp4", ".zip", ".rar", ".7z"};
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        static extern bool GetDiskFreeSpaceEx(string folder,out ulong available,out ulong total,out ulong free);
        internal static Func<string,long> AvailableSpace=FreeSpace;
        static long FreeSpace(string folder){ulong a,b,c;if(!GetDiskFreeSpaceEx(folder,out a,out b,out c))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法检查保存位置的剩余空间。");return a>long.MaxValue?long.MaxValue:(long)a;}
        static void Space(string folder,long size){if(size<0||AvailableSpace(folder)<size)throw new IOException("保存位置剩余空间不足。");}
        static FileStream Open(string path){return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,BufferSize,FileOptions.SequentialScan);}
        static byte[] Bytes(Stream s,int count){var b=new byte[count];int p=0,n;while(p<count&&(n=s.Read(b,p,count-p))>0)p+=n;if(p!=count)throw new InvalidDataException("文件已截断或数据不完整。");return b;}
        static ushort U16(byte[] b,int p){return BitConverter.ToUInt16(b,p);}
        static uint U32(byte[] b,int p){return BitConverter.ToUInt32(b,p);}
        static ulong U64(byte[] b,int p){return BitConverter.ToUInt64(b,p);}
        static long Long(ulong n){if(n>long.MaxValue)throw new InvalidDataException("文件长度或偏移超出范围。");return (long)n;}
        internal static void Range(long offset,long size,long total){if(offset<0||size<0||offset>total||size>total-offset)throw new InvalidDataException("文件长度或偏移无效，可能已被压缩或截断。");}
        static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
        internal static uint Crc(byte[] b,int p,int count){uint c=0xffffffff;for(int i=p;i<p+count;i++){c^=b[i];for(int n=0;n<8;n++)c=(c&1)!=0?0xedb88320^(c>>1):c>>1;}return c^0xffffffff;}
        static bool Starts(byte[] b,params byte[] prefix){return b.Length>=prefix.Length&&prefix.Select((v,i)=>b[i]==v).All(v=>v);}
        internal static void SafeName(string name)
        {
            if(string.IsNullOrWhiteSpace(name)||name!=Path.GetFileName(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.Any(char.IsControl)||name.EndsWith(".")||name.EndsWith(" ")||Utf8.GetByteCount(name)>1024)throw new InvalidDataException("原文件名无效。");
            string stem=Path.GetFileNameWithoutExtension(name).Split('.')[0].ToUpperInvariant();
            if(new[]{"CON","PRN","AUX","NUL","CLOCK$"}.Contains(stem)||System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(COM|LPT)[1-9]$"))throw new InvalidDataException("原文件名不能使用系统设备名称。");
        }
        // Parse the image prefix only, never pass the entire large disguised file to an image decoder.
        internal static int Jpeg(byte[] b,out byte[] metadata)
        {
            metadata=null;if(!Starts(b,255,216))throw new InvalidDataException("不是有效的 JPG 文件。");int p=2;bool scan=false,frame=false;
            while(p<b.Length)
            {
                if(scan){while(p<b.Length&&b[p]!=255)p++;if(p>=b.Length)break;p++;while(p<b.Length&&b[p]==255)p++;if(p>=b.Length)break;int code=b[p++];if(code==0||(code>=208&&code<=215))continue;p-=2;scan=false;}
                if(p+2>b.Length||b[p++]!=255)throw new InvalidDataException("JPG 标记损坏。");while(p<b.Length&&b[p]==255)p++;if(p>=b.Length)break;int marker=b[p++];
                if(marker==217){if(!frame)throw new InvalidDataException("JPG 没有图像数据。");return p;}
                if(marker==216||marker==0)throw new InvalidDataException("JPG 标记无效。");if(marker==1||(marker>=208&&marker<=215))continue;
                if(p+2>b.Length)break;int size=b[p]*256+b[p+1];if(size<2||size>b.Length-p)throw new InvalidDataException("JPG 封面损坏或过大。");
                if(marker==239&&size>=10&&Magic.Select((v,i)=>b[p+2+i]==v).All(v=>v)){if(metadata!=null)throw new InvalidDataException("发现重复的文件封装记录。");metadata=b.Skip(p+2).Take(size-2).ToArray();}
                if(marker>=192&&marker<=207&&marker!=196&&marker!=200&&marker!=204)frame=true;
                p+=size;if(marker==218)scan=true;
            }
            throw new InvalidDataException("JPG 封面不完整，或隐藏信息已被图片处理删除。");
        }
        internal static byte[] Cover(string path,out bool converted)
        {
            converted=false;
            using(var image=Codec.Load(path))
            {
                if(new FileInfo(path).Length<=CoverLimit)
                {
                    var b=File.ReadAllBytes(path);if(Starts(b,255,216)){byte[] old;int end=Jpeg(b,out old);if(old==null)return b.Take(end).ToArray();}
                }
                converted=true;int width=image.Width,height=image.Height;double scale=Math.Min(1,1600.0/Math.Max(width,height));width=Math.Max(1,(int)(width*scale));height=Math.Max(1,(int)(height*scale));
                var encoder=ImageCodecInfo.GetImageEncoders().First(c=>c.MimeType=="image/jpeg");
                for(int attempt=0;attempt<30;attempt++)
                {
                    using(var bitmap=Codec.Fit(image,width,height,Color.White))using(var ms=new MemoryStream())using(var settings=new EncoderParameters(1))
                    {settings.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,95L);bitmap.Save(ms,encoder,settings);if(ms.Length<=CoverLimit)return ms.ToArray();}
                    if(width==1&&height==1)break;width=Math.Max(1,(int)(width*.8));height=Math.Max(1,(int)(height*.8));
                }
                throw new InvalidDataException("封面无法转换到 512 KiB 以内，请另选一张图片。");
            }
        }
        public static byte[] EncodeCover(Image image)
        {
            int width=image.Width,height=image.Height;double scale=Math.Min(1,1600.0/Math.Max(width,height));width=Math.Max(1,(int)(width*scale));height=Math.Max(1,(int)(height*scale));var encoder=ImageCodecInfo.GetImageEncoders().First(c=>c.MimeType=="image/jpeg");
            for(int i=0;i<30;i++){using(var b=Codec.Fit(image,width,height,Color.White))using(var ms=new MemoryStream())using(var settings=new EncoderParameters(1)){settings.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,95L);b.Save(ms,encoder,settings);if(ms.Length<=CoverLimit)return ms.ToArray();}width=Math.Max(1,(int)(width*.8));height=Math.Max(1,(int)(height*.8));}throw new InvalidDataException("封面无法转换到 512 KiB 以内，请另选图片。");
        }
        static byte Kind(string extension){int i=Array.IndexOf(Extensions,extension.ToLowerInvariant());if(i<1)throw new InvalidDataException("仅支持 MP4、ZIP、RAR、7Z；分卷文件请先合并成单文件。");return (byte)i;}
        static void SingleFile(string path){if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),@"\.part\d+\.rar$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new InvalidDataException("不支持分卷 RAR，请先合并成单文件。");}
        static byte[] Record(HiddenFileInfo info,byte kind,byte[] sha)
        {
            byte[] name=Utf8.GetBytes(info.Name);using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms))
            {w.Write(Magic);w.Write((byte)(info.ZipCompatible?2:1));w.Write(kind);w.Write((ushort)name.Length);w.Write(info.Length);w.Write(info.Offset);w.Write(sha);w.Write(name);if(info.ZipCompatible){w.Write((byte)1);w.Write(new byte[32]);}return ms.ToArray();}
        }
        static HiddenFileInfo ParseRecord(byte[] m)
        {
            if(m==null)throw new InvalidDataException("没有找到文件伪装记录。请保存 QQ 原图；缩略图或压缩图无法还原。");
            if(m.Length<60||(m[8]!=1&&m[8]!=2)||m[9]<1||m[9]>=Extensions.Length||U16(m,10)==0||U16(m,10)>1024||m.Length!=60+U16(m,10)+(m[8]==2?33:0))throw new InvalidDataException("文件封装记录损坏或格式不支持。");
            int nameSize=U16(m,10);var info=new HiddenFileInfo {Name=Utf8.GetString(m,60,nameSize),Extension=Extensions[m[9]],Length=BitConverter.ToInt64(m,12),Offset=BitConverter.ToInt64(m,20),Sha256=Hex(m.Skip(28).Take(32).ToArray()),ZipCompatible=m[8]==2};SafeName(info.Name);
            if(info.ZipCompatible){if(m[9]!=2||m[60+nameSize]!=1)throw new InvalidDataException("ZIP 兼容记录无效。");info.StoredSha256=Hex(m.Skip(61+nameSize).Take(32).ToArray());}else info.StoredSha256=info.Sha256;
            if(!Path.GetExtension(info.Name).Equals(info.Extension,StringComparison.OrdinalIgnoreCase)||info.Length<=0)throw new InvalidDataException("封装文件名、类型或长度无效。");return info;
        }
        static HiddenFileInfo Header(Stream s,out byte[] cover)
        {
            s.Position=0;var prefix=Bytes(s,(int)Math.Min(s.Length,CoverLimit+2048L));byte[] metadata;int end=Jpeg(prefix,out metadata);var info=ParseRecord(metadata);
            Range(info.Offset,info.Length,s.Length);if(info.Offset!=end||info.Length!=s.Length-info.Offset)throw new InvalidDataException("文件数据不完整，或传输改变了封面结构。请重新保存原图。");cover=prefix.Take(end).ToArray();return info;
        }
        public static byte[] Preview(string path){using(var s=Open(path)){byte[] cover;Header(s,out cover);return cover;}}
        public static HiddenFileInfo Inspect(string path){using(var s=Open(path)){byte[] cover;return Header(s,out cover);}}
        public static void ValidateFile(string path,CancellationToken token){SingleFile(path);using(var s=Open(path))Validate(s,Kind(Path.GetExtension(path)),token);}
        internal static void ValidateFileAs(string path,string extension,CancellationToken token){using(var s=Open(path))Validate(s,Kind(extension),token);}
        static byte[] CopyHash(Stream input,Stream output,long count,CancellationToken token,IProgress<FileProgress> progress,string stage)
        {
            byte[] buffer=new byte[BufferSize];long done=0,lastTicks=0;using(var hash=SHA256.Create())
            {
                while(done<count){token.ThrowIfCancellationRequested();int n=input.Read(buffer,0,(int)Math.Min(buffer.Length,count-done));if(n==0)throw new InvalidDataException("读取文件时发现数据已截断。");if(output!=null)output.Write(buffer,0,n);hash.TransformBlock(buffer,0,n,null,0);done+=n;
                    long ticks=DateTime.UtcNow.Ticks;if(progress!=null&&(done==count||ticks-lastTicks>=TimeSpan.TicksPerMillisecond*100)){progress.Report(new FileProgress{Stage=stage,Completed=done,Total=count});lastTicks=ticks;}}
                token.ThrowIfCancellationRequested();hash.TransformFinalBlock(new byte[0],0,0);return hash.Hash;
            }
        }
        public static HiddenFileInfo Check(string path,CancellationToken token,IProgress<FileProgress> progress=null)
        {
            using(var s=Open(path)){byte[] cover;var info=Header(s,out cover);s.Position=info.Offset;if(Hex(CopyHash(s,null,info.Length,token,progress,"校验"))!=info.StoredSha256)throw new InvalidDataException("SHA-256 校验失败：隐藏文件已损坏。请重新发送并保存原图。");return info;}
        }
        static void Destination(string path,long size)
        {
            if(File.Exists(path)||Directory.Exists(path))throw new IOException("输出文件已存在，请选择其他名称。");string dir=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(dir);Space(dir,size);
        }
        internal static string RestoredName(string folder,string name)
        {
            Directory.CreateDirectory(folder);string path=Path.Combine(folder,name),stem=Path.GetFileNameWithoutExtension(name),extension=Path.GetExtension(name);int n=2;
            while(File.Exists(path)||Directory.Exists(path)){string suffix="_"+(n++),shortStem=stem;int limit=255-extension.Length-suffix.Length;if(shortStem.Length>limit){shortStem=shortStem.Substring(0,limit);if(char.IsHighSurrogate(shortStem[shortStem.Length-1]))shortStem=shortStem.Substring(0,shortStem.Length-1);}path=Path.Combine(folder,shortStem+suffix+extension);}return path;
        }
        public static HiddenFileInfo Pack(string coverPath,string inputPath,string outputPath,CancellationToken token,IProgress<FileProgress> progress=null)
        {
            token.ThrowIfCancellationRequested();bool converted;var cover=Cover(coverPath,out converted);return Pack(cover,inputPath,outputPath,token,progress,converted);
        }
        public static HiddenFileInfo Pack(byte[] cover,string inputPath,string outputPath,CancellationToken token,IProgress<FileProgress> progress=null,bool converted=false)
        {
            token.ThrowIfCancellationRequested();if(!Path.GetExtension(outputPath).Equals(".jpg",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("封装输出必须为 .jpg。");
            byte[] metadata;if(cover==null||cover.Length>CoverLimit||Jpeg(cover,out metadata)!=cover.Length||metadata!=null)throw new InvalidDataException("封面必须是 512 KiB 内完整 JPG。");SingleFile(inputPath);byte kind=Kind(Path.GetExtension(inputPath));string name=Path.GetFileName(inputPath);SafeName(name);
            using(var source=Open(inputPath))
            {
                Validate(source,kind,token);var info=new HiddenFileInfo {Name=name,Extension=Extensions[kind],Length=source.Length,CoverConverted=converted,ZipCompatible=kind==2};
                var record=Record(info,kind,new byte[32]);info.Offset=checked(cover.LongLength+record.Length+4);record=Record(info,kind,new byte[32]);Destination(outputPath,checked(info.Offset+info.Length));string temp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath)),".aipt-"+Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,BufferSize))
                    {
                        target.Write(cover,0,2);target.WriteByte(255);target.WriteByte(239);int size=record.Length+2;target.WriteByte((byte)(size>>8));target.WriteByte((byte)size);target.Write(record,0,record.Length);target.Write(cover,2,cover.Length-2);
                        source.Position=0;var hash=CopyHash(source,target,source.Length,token,progress,"封装");info.Sha256=Hex(hash);target.Position=34;target.Write(hash,0,hash.Length);target.Flush(true);
                        if(info.ZipCompatible)
                        {
                            ZipOffsets(target,info.Offset,info.Length,info.Offset,token);target.Flush(true);target.Position=info.Offset;var stored=CopyHash(target,null,info.Length,token,progress,"校验 ZIP 兼容目录");info.StoredSha256=Hex(stored);
                            ZipOffsets(target,info.Offset,info.Length,-info.Offset,token);target.Position=info.Offset;if(Hex(CopyHash(target,null,info.Length,token,progress,"复核原 ZIP"))!=info.Sha256)throw new IOException("ZIP 目录还原后与原文件不一致。");ZipOffsets(target,info.Offset,info.Length,info.Offset,token);
                            target.Position=67+Utf8.GetByteCount(info.Name);target.Write(stored,0,stored.Length);target.Flush(true);
                        }
                        else{target.Position=info.Offset;if(Hex(CopyHash(target,null,info.Length,token,progress,"复核原数据"))!=info.Sha256)throw new IOException("写入后的文件校验失败。");info.StoredSha256=info.Sha256;}
                    }
                    token.ThrowIfCancellationRequested();File.Move(temp,outputPath);return info;
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
        public static string Restore(string path,string folder,CancellationToken token,IProgress<FileProgress> progress=null)
        {
            using(var source=Open(path))
            {
                byte[] cover;var info=Header(source,out cover);string target=RestoredName(folder,info.Name);Destination(target,info.Length);string temp=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),".aipt-"+Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    source.Position=info.Offset;using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,BufferSize))
                    {if(Hex(CopyHash(source,output,info.Length,token,progress,"还原"))!=info.StoredSha256)throw new InvalidDataException("SHA-256 校验失败：原文件未保存，临时文件将清理。");if(info.ZipCompatible){ZipOffsets(output,0,info.Length,-info.Offset,token);output.Position=0;if(Hex(CopyHash(output,null,info.Length,token,progress,"复核原 ZIP"))!=info.Sha256)throw new InvalidDataException("ZIP 原始文件哈希不匹配，临时文件将清理。");}output.Flush(true);}
                    using(var restored=Open(temp))Validate(restored,Kind(info.Extension),token);
                    token.ThrowIfCancellationRequested();File.Move(temp,target);return target;
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
        internal static void Validate(Stream s,byte kind,CancellationToken token)
        {
            if(s.Length<8)throw new InvalidDataException("输入文件过短或已损坏。");s.Position=0;
            if(kind==1)Mp4(s,token);else if(kind==2)Zip(s,token);else if(kind==3)Rar(s,token);else SevenZip(s,token);s.Position=0;
        }
        static void Mp4(Stream s,CancellationToken token)
        {
            bool ftyp=false,moov=false,mdat=false;int count=0;
            while(s.Position<s.Length)
            {
                token.ThrowIfCancellationRequested();if(++count>100000)throw new InvalidDataException("MP4 数据块过多。");long start=s.Position;var b=Bytes(s,8);long size=Codec.Read32(b,0);string type=Encoding.ASCII.GetString(b,4,4);int head=8;
                if(size==1){b=Bytes(s,8);size=Long(((ulong)Codec.Read32(b,0)<<32)|Codec.Read32(b,4));head=16;}else if(size==0)size=s.Length-start;
                if(size<head)throw new InvalidDataException("MP4 数据块长度无效。");Range(start,size,s.Length);
                if(type=="ftyp"){if(size<head+8)throw new InvalidDataException("MP4 ftyp 无效。");ftyp=true;}if(type=="moov")moov=true;if(type=="mdat")mdat=true;s.Position=start+size;
            }
            if(!ftyp||!moov||!mdat)throw new InvalidDataException("MP4 缺少必要数据块，可能不完整；不支持仅含分片的 M4S。");
        }
        // Only ZIP directory offsets change. Entry bytes, passwords and compression stay intact.
        // Undo these additions before committing a restored ZIP and require its original hash.
        internal static void ZipOffsets(Stream s,long start,long length,long delta,CancellationToken token)
        {
            long shift=delta<0?-delta:0;int tail=(int)Math.Min(65557,length);s.Position=start+length-tail;var b=Bytes(s,tail);int end=-1;
            for(int i=b.Length-22;i>=0;i--)if(U32(b,i)==0x06054b50&&i+22+U16(b,i+20)==b.Length){end=i;break;}
            if(end<0)throw new InvalidDataException("ZIP 结尾无效。");long e=start+length-tail+end,entries=U16(b,end+10),cd=U32(b,end+16),cdSize=U32(b,end+12);
            if(entries==65535||cd==uint.MaxValue||cdSize==uint.MaxValue)
            {
                s.Position=e-20;var locator=Bytes(s,20);if(U32(locator,0)!=0x07064b50)throw new InvalidDataException("ZIP64 定位记录缺失。");long z=Long(U64(locator,8))-shift;Range(z,56,length);s.Position=start+z;var h=Bytes(s,56);if(U32(h,0)!=0x06064b50)throw new InvalidDataException("ZIP64 目录损坏。");entries=Long(U64(h,32));cd=Long(U64(h,48));cdSize=Long(U64(h,40));PatchOffset(s,e-12,Long(U64(locator,8)),delta,true);PatchOffset(s,start+z+48,cd,delta,true);
            }
            if(U32(b,end+16)!=uint.MaxValue)PatchOffset(s,e+16,U32(b,end+16),delta,false);
            cd-=shift;Range(cd,cdSize,length);long at=start+cd,stop=at+cdSize;
            if(entries>1000000)throw new InvalidDataException("ZIP 条目过多。");
            for(long i=0;i<entries;i++)
            {
                token.ThrowIfCancellationRequested();Range(at-start,46,length);s.Position=at;var h=Bytes(s,46);if(U32(h,0)!=0x02014b50)throw new InvalidDataException("ZIP 目录项损坏。");int name=U16(h,28),extraSize=U16(h,30),comment=U16(h,32);long local=U32(h,42);
                if(local!=uint.MaxValue)PatchOffset(s,at+42,local,delta,false);
                else
                {
                    s.Position=at+46+name;var extra=Bytes(s,extraSize);bool found=false;
                    for(int p=0;p+4<=extra.Length;){int tag=U16(extra,p),n=U16(extra,p+2);p+=4;if(n>extra.Length-p)throw new InvalidDataException("ZIP64 扩展损坏。");if(tag==1){int q=p;if(U32(h,24)==uint.MaxValue)q+=8;if(U32(h,20)==uint.MaxValue)q+=8;if(q+8>p+n)throw new InvalidDataException("ZIP64 偏移缺失。");PatchOffset(s,at+46+name+q,Long(U64(extra,q)),delta,true);found=true;break;}p+=n;}if(!found)throw new InvalidDataException("ZIP64 偏移缺失。");
                }
                at+=46L+name+extraSize+comment;if(at>stop)throw new InvalidDataException("ZIP 目录超出范围。");
            }
            if(at!=stop)throw new InvalidDataException("ZIP 目录长度不匹配。");
        }
        static void PatchOffset(Stream s,long at,long value,long delta,bool wide)
        {
            long next=checked(value+delta);if(next<0||(!wide&&next>=uint.MaxValue))throw new InvalidDataException("ZIP 兼容偏移超出格式范围，请先用 ZIP64 重新保存该压缩包。");s.Position=at;byte[] b=wide?BitConverter.GetBytes((ulong)next):BitConverter.GetBytes((uint)next);s.Write(b,0,b.Length);
        }
        static void Zip(Stream s,CancellationToken token)
        {
            int tail=(int)Math.Min(65557,s.Length);s.Position=s.Length-tail;var b=Bytes(s,tail);int end=-1;
            for(int i=b.Length-22;i>=0;i--)if(U32(b,i)==0x06054b50&&i+22+U16(b,i+20)==b.Length){end=i;break;}
            if(end<0)throw new InvalidDataException("ZIP 目录结尾损坏，或存在不支持的尾部数据。");
            if(U16(b,end+4)!=0||U16(b,end+6)!=0||U16(b,end+8)!=U16(b,end+10))throw new InvalidDataException("不支持分卷 ZIP，请先合并。");
            long entries=U16(b,end+10),size=U32(b,end+12),offset=U32(b,end+16),directoryEnd=s.Length-tail+end;
            if(entries==65535||size==uint.MaxValue||offset==uint.MaxValue)
            {
                Range(directoryEnd-20,20,s.Length);s.Position=directoryEnd-20;var locator=Bytes(s,20);if(U32(locator,0)!=0x07064b50||U32(locator,4)!=0||U32(locator,16)!=1)throw new InvalidDataException("ZIP64 分卷或目录无效。");
                long z=Long(U64(locator,8));Range(z,56,s.Length);s.Position=z;var header=Bytes(s,56);if(U32(header,0)!=0x06064b50||U32(header,16)!=0||U32(header,20)!=0||U64(header,24)!=U64(header,32))throw new InvalidDataException("ZIP64 目录无效。");entries=Long(U64(header,32));size=Long(U64(header,40));offset=Long(U64(header,48));directoryEnd=z;
            }
            if(entries>1000000)throw new InvalidDataException("ZIP 条目超过 100 万，请拆分归档。");Range(offset,size,s.Length);if(offset+size!=directoryEnd)throw new InvalidDataException("ZIP 目录偏移无效；请使用没有前置模块的原始 ZIP。");s.Position=offset;
            for(long i=0;i<entries;i++)
            {
                token.ThrowIfCancellationRequested();var h=Bytes(s,46);if(U32(h,0)!=0x02014b50||U16(h,34)!=0)throw new InvalidDataException("ZIP 条目或分卷标志无效。");
                int nameSize=U16(h,28),extraSize=U16(h,30),commentSize=U16(h,32);long packed=U32(h,20),local=U32(h,42);Bytes(s,nameSize);var extra=Bytes(s,extraSize);Bytes(s,commentSize);
                if(local==uint.MaxValue||packed==uint.MaxValue||U32(h,24)==uint.MaxValue)
                {
                    bool found=false;for(int p=0;p+4<=extra.Length;){int tag=U16(extra,p),length=U16(extra,p+2);p+=4;if(length>extra.Length-p)throw new InvalidDataException("ZIP64 扩展损坏。");if(tag==1){int q=p;if(U32(h,24)==uint.MaxValue){if(q+8>p+length)throw new InvalidDataException("ZIP64 大小缺失。");Long(U64(extra,q));q+=8;}if(packed==uint.MaxValue){if(q+8>p+length)throw new InvalidDataException("ZIP64 大小缺失。");packed=Long(U64(extra,q));q+=8;}if(local==uint.MaxValue){if(q+8>p+length)throw new InvalidDataException("ZIP64 偏移缺失。");local=Long(U64(extra,q));}found=true;break;}p+=length;}if(!found)throw new InvalidDataException("ZIP64 扩展缺失。");
                }
                long next=s.Position;Range(local,30,offset);s.Position=local;var lh=Bytes(s,30);if(U32(lh,0)!=0x04034b50)throw new InvalidDataException("ZIP 本地文件头无效。");Range(checked(local+30+U16(lh,26)+U16(lh,28)),packed,offset);s.Position=next;
            }
            if(s.Position!=directoryEnd)throw new InvalidDataException("ZIP 目录长度不匹配。");
        }
        static ulong Vint(byte[] b,ref int p)
        {
            ulong value=0;for(int i=0;i<10;i++){if(p>=b.Length)throw new InvalidDataException("RAR 整数字段不完整。");byte x=b[p++];if(i==9&&(x&254)!=0)throw new InvalidDataException("RAR 整数字段溢出。");value|=(ulong)(x&127)<<(i*7);if((x&128)==0)return value;}throw new InvalidDataException("RAR 整数字段无效。");
        }
        static void Rar(Stream s,CancellationToken token)
        {
            var signature=Bytes(s,7);if(!Starts(signature,82,97,114,33,26,7))throw new InvalidDataException("RAR 文件头无效；不支持 SFX 或分卷文件。");bool five=signature[6]==1;if(five){if(s.ReadByte()!=0)throw new InvalidDataException("RAR5 文件头无效。");}else if(signature[6]!=0)throw new InvalidDataException("RAR 格式不支持。");bool main=false,ended=false;int blocks=0;
            while(s.Position<s.Length)
            {
                token.ThrowIfCancellationRequested();if(++blocks>1000000)throw new InvalidDataException("RAR 数据块过多。");long dataSize=0;
                if(!five)
                {
                    var first=Bytes(s,7);int size=U16(first,5),flags=U16(first,3),type=first[2];if(size<7)throw new InvalidDataException("RAR4 文件头损坏。");var head=first.Concat(Bytes(s,size-7)).ToArray();if((Crc(head,2,head.Length-2)&65535)!=U16(head,0))throw new InvalidDataException("RAR4 文件头校验失败。");
                    if(type==0x73){if((flags&1)!=0)throw new InvalidDataException("不支持分卷 RAR。");main=true;if((flags&128)!=0)return;}
                    if((flags&0x8000)!=0){if(size<11)throw new InvalidDataException("RAR4 数据长度缺失。");dataSize=U32(head,7);}
                    if(type==0x74){if((flags&3)!=0)throw new InvalidDataException("不支持分卷 RAR。");if((flags&0x100)!=0){if(size<40)throw new InvalidDataException("RAR4 大文件头损坏。");dataSize=Long(((ulong)U32(head,32)<<32)|U32(head,7));}}
                    if(type==0x7b){if((flags&1)!=0)throw new InvalidDataException("不支持分卷 RAR。");ended=true;}
                }
                else
                {
                    var checksum=Bytes(s,4);var lengthBytes=new List<byte>();int next;do{next=s.ReadByte();if(next<0||lengthBytes.Count>=3)throw new InvalidDataException("RAR5 文件头大小无效。");lengthBytes.Add((byte)next);}while((next&128)!=0);
                    int p=0;long size=Long(Vint(lengthBytes.ToArray(),ref p));if(size<2||size>2*1024*1024)throw new InvalidDataException("RAR5 文件头过大或损坏。");var body=Bytes(s,(int)size);var all=lengthBytes.Concat(body).ToArray();if(Crc(all,0,all.Length)!=U32(checksum,0))throw new InvalidDataException("RAR5 文件头校验失败。");p=0;ulong type=Vint(body,ref p),flags=Vint(body,ref p);if((flags&1)!=0)Vint(body,ref p);if((flags&2)!=0)dataSize=Long(Vint(body,ref p));if((flags&24)!=0)throw new InvalidDataException("不支持分卷 RAR。");
                    if(type==1){ulong archiveFlags=Vint(body,ref p);if((archiveFlags&1)!=0)throw new InvalidDataException("不支持分卷 RAR。");main=true;}if(type==4)return;if(type==5){if((Vint(body,ref p)&1)!=0)throw new InvalidDataException("不支持分卷 RAR。");ended=true;}
                }
                Range(s.Position,dataSize,s.Length);s.Position+=dataSize;if(ended)break;
            }
            if(!main||(!ended&&five))throw new InvalidDataException("RAR 缺少必要文件头或归档结尾。");
        }
        static void SevenZip(Stream s,CancellationToken token)
        {
            var h=Bytes(s,32);if(!Starts(h,55,122,188,175,39,28)||Crc(h,12,20)!=U32(h,8))throw new InvalidDataException("7Z 文件头或 CRC 校验失败。");long offset=Long(U64(h,12)),size=Long(U64(h,20));if(offset>long.MaxValue-32)throw new InvalidDataException("7Z 偏移溢出。");Range(offset+32,size,s.Length);s.Position=offset+32;
            byte[] buffer=new byte[BufferSize];long done=0;uint crc=0xffffffff;while(done<size){token.ThrowIfCancellationRequested();int n=s.Read(buffer,0,(int)Math.Min(buffer.Length,size-done));if(n==0)throw new InvalidDataException("7Z 数据截断。");for(int i=0;i<n;i++){crc^=buffer[i];for(int k=0;k<8;k++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}done+=n;}if((crc^0xffffffff)!=U32(h,28))throw new InvalidDataException("7Z 目录 CRC 校验失败。");
        }
    }
}
