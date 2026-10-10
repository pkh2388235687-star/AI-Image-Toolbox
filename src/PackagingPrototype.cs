using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace QQImageSwitch
{
    // Independent compatibility-test implementation. No QQ delivery claim.
    public static class PackagingPrototype
    {
        static readonly byte[] PngSignature={137,80,78,71,13,10,26,10};
        static readonly byte[] End={0,0,0,0,73,69,78,68,174,66,96,130};
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        static readonly uint[] CrcTable=Enumerable.Range(0,256).Select(i=>{uint v=(uint)i;for(int j=0;j<8;j++)v=(v&1)!=0?0xedb88320^(v>>1):v>>1;return v;}).ToArray();
        public sealed class Record
        {
            public string format="AIPTPNG",name,kind,sha256,stored_sha256;
            public int version=1;public long length;public int? zip_comment;
            public string original_name,original_sha256;public long original_length;
        }
        sealed class Scan {public Record Record;public long Offset;public byte[] Cover;}
        internal static byte[] Read(Stream s,int count){var b=new byte[count];int p=0,n;while(p<count&&(n=s.Read(b,p,count-p))>0)p+=n;if(p!=count)throw new InvalidDataException("文件已截断 / Truncated file");return b;}
        internal static uint Be(byte[] b,int at){return ((uint)b[at]<<24)|((uint)b[at+1]<<16)|((uint)b[at+2]<<8)|b[at+3];}
        internal static byte[] Big(uint n){return new[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};}
        internal static uint Crc(uint crc,byte[] data,int count){for(int p=0;p<count;p++)crc=CrcTable[(crc^data[p])&255]^(crc>>8);return crc;}
        static void Chunk(Stream s,string name,byte[] data){byte[] type=Encoding.ASCII.GetBytes(name);s.Write(Big((uint)data.Length),0,4);s.Write(type,0,4);s.Write(data,0,data.Length);uint crc=Crc(0xffffffff,type,4);s.Write(Big(Crc(crc,data,data.Length)^0xffffffff),0,4);}
        static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
        internal static string Hash(Stream s,long length,CancellationToken token,IProgress<FileProgress> progress,string stage,Stream target=null){using(var h=SHA256.Create()){var buffer=new byte[FileDisguise.BufferSize];long done=0,ticks=0;while(done<length){token.ThrowIfCancellationRequested();int n=s.Read(buffer,0,(int)Math.Min(buffer.Length,length-done));if(n==0)throw new InvalidDataException("文件已截断 / Truncated file");if(target!=null)target.Write(buffer,0,n);h.TransformBlock(buffer,0,n,null,0);done+=n;if(progress!=null&&(done==length||DateTime.UtcNow.Ticks-ticks>TimeSpan.TicksPerMillisecond*120)){progress.Report(new FileProgress{Stage=stage,Completed=done,Total=length});ticks=DateTime.UtcNow.Ticks;}}h.TransformFinalBlock(new byte[0],0,0);return Hex(h.Hash);}}
        static void Validate(Record r){if(r==null||r.format!="AIPTPNG"||(r.version!=1&&r.version!=2)||r.length<=0||r.length>int.MaxValue||!new[]{"zip","rar","7z","mp4"}.Contains(r.kind))throw new InvalidDataException("PNG 文件记录无效 / Invalid PNG record");FileDisguise.SafeName(r.name);if(Path.GetExtension(r.name).ToLowerInvariant()!="."+r.kind||!System.Text.RegularExpressions.Regex.IsMatch(r.sha256??"","^[a-f0-9]{64}$")||!System.Text.RegularExpressions.Regex.IsMatch(r.stored_sha256??"","^[a-f0-9]{64}$"))throw new InvalidDataException("文件记录不完整 / Invalid archive metadata");if(r.kind=="zip"?(!r.zip_comment.HasValue||r.zip_comment<0||r.zip_comment>65519):r.zip_comment.HasValue)throw new InvalidDataException("ZIP 注释过长或记录无效 / ZIP comment too large or invalid");if(r.version==2){FileDisguise.SafeName(r.original_name);if(r.kind!="zip"||r.original_length<=0||r.original_length>int.MaxValue||!new[]{".mp4",".zip",".rar",".7z"}.Contains(Path.GetExtension(r.original_name).ToLowerInvariant())||!System.Text.RegularExpressions.Regex.IsMatch(r.original_sha256??"","^[a-f0-9]{64}$"))throw new InvalidDataException("原文件记录无效 / Invalid original file record");}}
        static Scan ReadPng(string path,CancellationToken token,bool plain=false,IProgress<FileProgress> progress=null)
        {
            using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileDisguise.BufferSize))using(var cover=new MemoryStream())
            {
                if(!Read(s,8).SequenceEqual(PngSignature))throw new InvalidDataException("不是 PNG / Not a PNG");cover.Write(PngSignature,0,8);var result=new Scan();bool idat=false,payload=false;int chunks=0;long lastTicks=0;string stored=null;
                while(true)
                {
                    token.ThrowIfCancellationRequested();var head=Read(s,8);uint size=Be(head,0);string type=Encoding.ASCII.GetString(head,4,4);if(++chunks>100000||size>int.MaxValue||size>s.Length-s.Position-4||!System.Text.RegularExpressions.Regex.IsMatch(type,"^[A-Za-z]{2}[A-Z][A-Za-z]$"))throw new InvalidDataException("PNG 数据块无效 / Invalid PNG chunk");
                    if(chunks==1&&(type!="IHDR"||size!=13)||type=="IHDR"&&chunks!=1||new[]{"acTL","fcTL","fdAT"}.Contains(type))throw new InvalidDataException("PNG 封面无效；不支持动画 / Invalid or animated PNG cover");
                    if(type=="IDAT"){if(payload)throw new InvalidDataException("PNG 块顺序无效 / Invalid PNG order");idat=true;}
                    if(type=="aiMd"&&(result.Record!=null||size>2048||!idat||payload))throw new InvalidDataException("PNG 记录重复或无效 / Duplicate PNG record");
                    if(type=="aiPd"){if(result.Record==null||payload||!idat)throw new InvalidDataException("PNG 文件块无效 / Invalid PNG payload");payload=true;result.Offset=s.Position;if(size!=result.Record.length)throw new InvalidDataException("文件长度不匹配 / Payload length mismatch");}
                    else if(payload&&type!="IEND")throw new InvalidDataException("文件块后存在多余数据 / Extra data after payload");
                    bool keep=type!="aiMd"&&type!="aiPd";if(keep&&cover.Length+size+12>FileDisguise.CoverLimit)throw new InvalidDataException("PNG 封面超过 512 KiB / PNG cover over 512 KiB");
                    uint crc=Crc(0xffffffff,head.Skip(4).ToArray(),4);long remaining=size;var buffer=new byte[FileDisguise.BufferSize];using(var bytes=new MemoryStream())using(var sha=SHA256.Create())
                    {
                        while(remaining>0){token.ThrowIfCancellationRequested();int n=s.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(n<=0)throw new InvalidDataException("文件已截断 / Truncated file");crc=Crc(crc,buffer,n);if(type=="aiPd")sha.TransformBlock(buffer,0,n,null,0);else bytes.Write(buffer,0,n);remaining-=n;if(progress!=null&&(remaining==0||DateTime.UtcNow.Ticks-lastTicks>TimeSpan.TicksPerMillisecond*120)){progress.Report(new FileProgress{Stage="检查 PNG / Check PNG",Completed=s.Position,Total=s.Length});lastTicks=DateTime.UtcNow.Ticks;}}
                        var check=Read(s,4);if(Be(check,0)!=(crc^0xffffffff))throw new InvalidDataException("PNG CRC 校验失败 / PNG CRC mismatch");if(type=="aiPd"){sha.TransformFinalBlock(new byte[0],0,0);stored=Hex(sha.Hash);}else if(type=="aiMd"){result.Record=Inspection.Json().Deserialize<Record>(Utf8.GetString(bytes.ToArray()));Validate(result.Record);}else if(keep){cover.Write(head,0,head.Length);var data=bytes.ToArray();cover.Write(data,0,data.Length);cover.Write(check,0,4);}
                    }
                    if(type=="IEND"){if(size!=0||!idat||s.Position!=s.Length)throw new InvalidDataException("PNG 结束块或文件尾无效 / Invalid PNG ending");break;}
                }
                if(!plain&&(result.Record==null||!payload))throw new InvalidDataException("没有 PNG 文件记录；图片可能被重新编码 / No PNG record; image may have been re-encoded");if(result.Record!=null&&stored!=result.Record.stored_sha256)throw new InvalidDataException("SHA-256 校验失败 / SHA-256 mismatch");result.Cover=cover.ToArray();return result;
            }
        }
        internal static byte[] PngCover(byte[] jpeg)
        {
            using(var image=Codec.Decode(jpeg))
            {
                int w=image.Width,h=image.Height;for(int i=0;i<32;i++){using(var b=Codec.Fit(image,w,h,Color.White)){byte[] data=Codec.StaticPng(b);if(data.Length<=FileDisguise.CoverLimit)return data;}w=Math.Max(1,(int)(w*.8));h=Math.Max(1,(int)(h*.8));}throw new InvalidDataException("PNG 封面过大 / PNG cover too large");
            }
        }
        internal static long ZipEnd(Stream s,long start,long length,int expectedExtra)
        {
            int tail=(int)Math.Min(65557,length);s.Position=start+length-tail;var b=Read(s,tail);for(int p=b.Length-22;p>=0;p--)if(BitConverter.ToUInt32(b,p)==0x06054b50&&p+22+BitConverter.ToUInt16(b,p+20)==b.Length+expectedExtra)return start+length-tail+p;throw new InvalidDataException("ZIP 结束记录无效 / Invalid ZIP end record");
        }
        internal static void Space(string path,long length){string folder=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(folder);if(FileDisguise.AvailableSpace(folder)<length+FileDisguise.CoverLimit+4096)throw new IOException("保存位置剩余空间不足 / Insufficient disk space");if(File.Exists(path)||Directory.Exists(path))throw new IOException("输出已存在 / Output already exists");}
        internal static string Temp(string output){return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),".aipt-"+Guid.NewGuid().ToString("N")+".tmp");}
        public static void Pack(byte[] jpeg,string source,string output,int mode,CancellationToken token,IProgress<FileProgress> progress=null)
        {
            if(mode==1){FileDisguise.Pack(jpeg,source,output,token,progress,true);return;}token.ThrowIfCancellationRequested();FileDisguise.ValidateFile(source,token);
            if(mode==0){if(Path.GetExtension(output).ToLowerInvariant()!=".jpg")throw new ArgumentException("拼接输出必须为 JPG / Concatenation output must be JPG");CopyPack(jpeg,source,output,token,progress);return;}
            if((mode!=2&&mode!=3)||Path.GetExtension(output).ToLowerInvariant()!=".png")throw new ArgumentException("请选择有效方式及后缀 / Select a valid mode and extension");long originalLength=new FileInfo(source).Length;if(originalLength>int.MaxValue-4096L)throw new InvalidDataException("PNG 包裹最多约 2 GiB；请使用 JPG / PNG wrapper limit approximately 2 GiB; use JPG");Space(output,checked(originalLength*2));string wrapDir=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),".aipt-wrap-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(wrapDir);string wrapped=Path.Combine(wrapDir,Path.GetFileName(source)+".zip");string originalHash;
            try{using(var input=File.OpenRead(source))using(var file=new FileStream(wrapped,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                {using(var zip=new ZipArchive(file,ZipArchiveMode.Create,true)){var entry=zip.CreateEntry(Path.GetFileName(source),CompressionLevel.NoCompression);using(var entryStream=entry.Open())originalHash=Hash(input,originalLength,token,progress,"包裹原文件 / Wrap original",entryStream);}file.Flush(true);}if(mode==3)IdatPackaging.Pack(jpeg,wrapped,output,Path.GetFileName(source),originalLength,originalHash,token,progress);else PackPng(jpeg,wrapped,output,token,progress,Path.GetFileName(source),originalLength,originalHash);}
            finally{if(File.Exists(wrapped))File.Delete(wrapped);if(Directory.Exists(wrapDir))Directory.Delete(wrapDir);}
        }
        static void PackPng(byte[] jpeg,string source,string output,CancellationToken token,IProgress<FileProgress> progress,string originalName,long originalLength,string originalHash)
        {
            var cover=PngCover(jpeg);long length=new FileInfo(source).Length;if(length>int.MaxValue)throw new InvalidDataException("PNG 单块最多 2 GiB 减 1 字节 / PNG payload limit: 2 GiB minus one byte");
            Space(output,length);string temp=Temp(output);try
            {
                using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read,FileDisguise.BufferSize))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,FileDisguise.BufferSize))
                {
                    var record=new Record{version=2,original_name=originalName,original_length=originalLength,original_sha256=originalHash,name=Path.GetFileName(source),kind=Path.GetExtension(source).Substring(1).ToLowerInvariant(),length=length,sha256=Hash(input,length,token,progress,"校验原文件 / Hash original"),stored_sha256=new string('0',64)};
                    if(record.kind=="zip"){long end=ZipEnd(input,0,length,0);input.Position=end+20;record.zip_comment=BitConverter.ToUInt16(Read(input,2),0);}Validate(record);target.Write(cover,0,cover.Length-12);long recordAt=target.Position;var encoded=Utf8.GetBytes(Inspection.Json().Serialize(record));if(encoded.Length>2048)throw new InvalidDataException("记录过大 / Oversized metadata");Chunk(target,"aiMd",encoded);target.Write(Big((uint)length),0,4);var type=Encoding.ASCII.GetBytes("aiPd");target.Write(type,0,4);long offset=target.Position;input.Position=0;if(Hash(input,length,token,progress,"封装 / Package",target)!=record.sha256)throw new IOException("原文件发生变化 / Input changed");
                    if(record.kind=="zip"){FileDisguise.ZipOffsets(target,offset,length,offset,token);long end=ZipEnd(target,offset,length,0);target.Position=end+20;var comment=BitConverter.GetBytes((ushort)(record.zip_comment.Value+16));target.Write(comment,0,2);}
                    target.Position=offset;record.stored_sha256=Hash(target,length,token,progress,"复核数据 / Verify data");target.Position=offset;uint crc=Crc(0xffffffff,type,4);var buffer=new byte[FileDisguise.BufferSize];long left=length;while(left>0){token.ThrowIfCancellationRequested();int n=target.Read(buffer,0,(int)Math.Min(left,buffer.Length));if(n==0)throw new EndOfStreamException();crc=Crc(crc,buffer,n);left-=n;}target.Write(Big(crc^0xffffffff),0,4);target.Write(End,0,End.Length);target.Position=recordAt;var final=Utf8.GetBytes(Inspection.Json().Serialize(record));if(final.Length!=encoded.Length)throw new IOException("记录长度发生变化 / Metadata length changed");Chunk(target,"aiMd",final);target.Flush(true);
                }
                ReadPng(temp,token,false,progress);token.ThrowIfCancellationRequested();File.Move(temp,output);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        static void CopyPack(byte[] cover,string source,string output,CancellationToken token,IProgress<FileProgress> progress)
        {
            long length=new FileInfo(source).Length;Space(output,checked(length+cover.Length));string temp=Temp(output);try{using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read,FileDisguise.BufferSize))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,FileDisguise.BufferSize)){target.Write(cover,0,cover.Length);string hash=Hash(input,length,token,progress,"拼接 / Concatenate",target);target.Flush(true);target.Position=cover.Length;if(Hash(target,length,token,progress,"复核拼接 / Verify concatenation")!=hash)throw new IOException("拼接校验失败 / Concatenation verification failed");}token.ThrowIfCancellationRequested();File.Move(temp,output);}finally{if(File.Exists(temp))File.Delete(temp);}}
        static bool IsPng(string path){using(var s=File.OpenRead(path)){return s.Length>=8&&Read(s,8).SequenceEqual(PngSignature);}}
        public static string Restore(string path,string folder,CancellationToken token,IProgress<FileProgress> progress=null)
        {
            if(!IsPng(path)){byte[] meta;long offset;using(var s=File.OpenRead(path)){var b=Read(s,(int)Math.Min(s.Length,FileDisguise.CoverLimit+2048L));offset=FileDisguise.Jpeg(b,out meta);}if(meta!=null)return FileDisguise.Restore(path,folder,token,progress);return RestoreCopy(path,folder,offset,token,progress);}
            if(IdatPackaging.HasRecord(path))return IdatPackaging.Restore(path,folder,token,progress);
            var scan=ReadPng(path,token,false,progress);string output=FileDisguise.RestoredName(folder,scan.Record.version==2?scan.Record.original_name:scan.Record.name);Space(output,scan.Record.length+scan.Record.original_length);string temp=Temp(output),originalTemp=Temp(output);try
            {
                using(var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileDisguise.BufferSize))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,FileDisguise.BufferSize))
                {source.Position=scan.Offset;if(Hash(source,scan.Record.length,token,progress,"还原 / Restore",target)!=scan.Record.stored_sha256)throw new InvalidDataException("文件发生变化 / Input changed");if(scan.Record.kind=="zip"){long end=ZipEnd(target,0,scan.Record.length,16);target.Position=end+20;var comment=BitConverter.GetBytes((ushort)scan.Record.zip_comment.Value);target.Write(comment,0,2);FileDisguise.ZipOffsets(target,0,scan.Record.length,-scan.Offset,token);}target.Position=0;if(Hash(target,scan.Record.length,token,progress,"核验原文件 / Verify original")!=scan.Record.sha256)throw new InvalidDataException("原文件 SHA-256 不匹配 / Original SHA-256 mismatch");target.Flush(true);}
                FileDisguise.ValidateFileAs(temp,"."+scan.Record.kind,token);if(scan.Record.version==2){using(var z=new ZipArchive(File.OpenRead(temp),ZipArchiveMode.Read)){
                    if(z.Entries.Count!=1||z.Entries[0].FullName!=scan.Record.original_name||z.Entries[0].Length!=scan.Record.original_length)throw new InvalidDataException("包裹文件内容不匹配 / Wrapper entry mismatch");using(var entry=z.Entries[0].Open())using(var original=new FileStream(originalTemp,FileMode.CreateNew,FileAccess.Write)){if(Hash(entry,scan.Record.original_length,token,progress,"提取原文件 / Extract original",original)!=scan.Record.original_sha256||entry.ReadByte()!=-1)throw new InvalidDataException("原文件校验失败 / Original checksum mismatch");original.Flush(true);}}
                    FileDisguise.ValidateFileAs(originalTemp,Path.GetExtension(scan.Record.original_name),token);token.ThrowIfCancellationRequested();File.Move(originalTemp,output);
                }else{token.ThrowIfCancellationRequested();File.Move(temp,output);}return output;
            }
            finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(originalTemp))File.Delete(originalTemp);}
        }
        static string RestoreCopy(string path,string folder,long offset,CancellationToken token,IProgress<FileProgress> progress)
        {
            string ext;using(var s=File.OpenRead(path)){s.Position=offset;var b=Read(s,(int)Math.Min(64,s.Length-offset));if(b.Length>=4&&b[0]==80&&b[1]==75)ext=".zip";else if(b.Length>=7&&Encoding.ASCII.GetString(b,0,4)=="Rar!")ext=".rar";else if(b.Length>=6&&b.Take(6).SequenceEqual(new byte[]{55,122,188,175,39,28}))ext=".7z";else if(b.Length>=8&&Encoding.ASCII.GetString(b,4,4)=="ftyp")ext=".mp4";else throw new InvalidDataException("无法识别拼接文件；请使用本工具封装 / Unrecognized concatenated file");}
            string name=Path.GetFileNameWithoutExtension(path);if(name.Length>150)name=name.Substring(0,150);string output=FileDisguise.RestoredName(folder,name+"_restored"+ext);long length=new FileInfo(path).Length-offset;Space(output,length);string temp=Temp(output);try{using(var source=File.OpenRead(path))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.Write)){source.Position=offset;Hash(source,length,token,progress,"提取拼接数据 / Extract concatenated data",target);target.Flush(true);}FileDisguise.ValidateFileAs(temp,ext,token);token.ThrowIfCancellationRequested();File.Move(temp,output);return output;}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static HiddenFileInfo Check(string path,CancellationToken token)
        {if(IsPng(path)){if(IdatPackaging.HasRecord(path))return IdatPackaging.Check(path,token);var s=ReadPng(path,token);bool wrap=s.Record.version==2;return new HiddenFileInfo{Name=wrap?s.Record.original_name:s.Record.name,Extension=wrap?Path.GetExtension(s.Record.original_name):"."+s.Record.kind,Length=wrap?s.Record.original_length:s.Record.length,Offset=s.Offset,Sha256=wrap?s.Record.original_sha256:s.Record.sha256,StoredSha256=s.Record.stored_sha256};}return FileDisguise.Check(path,token);}
        public static void Command(string[] args)
        {
            if(args[0]=="--prototype-pack"){byte[] cover=FileBatch.Cover(args[2],1,false,new CoverTextOptions());Pack(cover,args[3],args[4],int.Parse(args[1]),CancellationToken.None);Console.WriteLine("PASS: "+args[4]);}
            else if(args[0]=="--prototype-restore")Console.WriteLine(Restore(args[1],args[2],CancellationToken.None));
            else if(args[0]=="--prototype-check")Console.WriteLine(Inspection.Json().Serialize(Check(args[1],CancellationToken.None)));
            else throw new ArgumentException("Unknown prototype command");
        }
    }
}
