using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace QQImageSwitch
{
    // ZIP recovery metadata is inside IDAT, in the outer ZIP comment.
    // PNG carries no private chunks; no code or automatic execution is embedded.
    static class IdatPackaging
    {
        static readonly byte[] Magic=Encoding.ASCII.GetBytes("AIPTIDAT");
        static readonly byte[] End={0,0,0,0,73,69,78,68,174,66,96,130};
        static readonly byte[] Signature={137,80,78,71,13,10,26,10};
        sealed class Record {public string format="AIPTIDAT",original_name,original_sha256;public int version=1;public long original_length;}
        sealed class Info {public Record Record;public long Offset,Length;public int Comment;public string StoredHash;}
        static void Validate(Record r)
        {
            if(r==null||r.format!="AIPTIDAT"||r.version!=1||r.original_length<=0||r.original_length>int.MaxValue)throw new InvalidDataException("IDAT 记录无效 / Invalid IDAT record");
            FileDisguise.SafeName(r.original_name);
            if(!new[]{".mp4",".zip",".rar",".7z"}.Contains(Path.GetExtension(r.original_name).ToLowerInvariant())||!System.Text.RegularExpressions.Regex.IsMatch(r.original_sha256??"","^[a-f0-9]{64}$"))throw new InvalidDataException("IDAT 原文件记录无效 / Invalid IDAT original record");
        }
        static long EndAt(Stream s){return PackagingPrototype.ZipEnd(s,0,s.Length-16,16);}
        public static bool HasRecord(string path)
        {
            using(var s=File.OpenRead(path)){
                if(s.Length<38)return false;long end;
                try{end=EndAt(s);}catch(InvalidDataException){return false;}
                s.Position=end+20;int comment=BitConverter.ToUInt16(PackagingPrototype.Read(s,2),0);
                return comment>=Magic.Length+16&&PackagingPrototype.Read(s,Magic.Length).SequenceEqual(Magic);
            }
        }
        static Info Inspect(string path,CancellationToken token,IProgress<FileProgress> progress)
        {
            using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileDisguise.BufferSize)){
                long end=EndAt(s);s.Position=end;var e=PackagingPrototype.Read(s,22);int comment=BitConverter.ToUInt16(e,20);
                if(comment<24||comment>2064||BitConverter.ToUInt16(e,4)!=0||BitConverter.ToUInt16(e,6)!=0||BitConverter.ToUInt16(e,8)!=1||BitConverter.ToUInt16(e,10)!=1)throw new InvalidDataException("IDAT ZIP 目录无效 / Invalid IDAT ZIP directory");
                var meta=PackagingPrototype.Read(s,comment-16);if(!meta.Take(8).SequenceEqual(Magic))throw new InvalidDataException("没有 IDAT 记录；图片可能已重新编码 / No IDAT record; image may be re-encoded");
                var record=Inspection.Json().Deserialize<Record>(new UTF8Encoding(false,true).GetString(meta,8,meta.Length-8));Validate(record);
                long cd=BitConverter.ToUInt32(e,16),cdSize=BitConverter.ToUInt32(e,12);
                FileDisguise.Range(cd,cdSize,end);if(cd+cdSize!=end||cdSize<46)throw new InvalidDataException("IDAT ZIP 偏移无效 / Invalid IDAT ZIP offsets");
                s.Position=cd;var directory=PackagingPrototype.Read(s,46);if(BitConverter.ToUInt32(directory,0)!=0x02014b50)throw new InvalidDataException("IDAT ZIP 目录损坏 / Invalid ZIP central header");
                long offset=BitConverter.ToUInt32(directory,42),length=s.Length-16-offset;
                FileDisguise.Range(offset,length,s.Length-16);s.Position=offset;if(!PackagingPrototype.Read(s,4).SequenceEqual(new byte[]{80,75,3,4}))throw new InvalidDataException("IDAT ZIP 本地头无效 / Invalid ZIP local header");
                s.Position=0;if(!PackagingPrototype.Read(s,8).SequenceEqual(Signature))throw new InvalidDataException("不是 PNG / Not PNG");
                bool idat=false,found=false;int chunks=0;long other=8;var buffer=new byte[FileDisguise.BufferSize];
                while(true){token.ThrowIfCancellationRequested();var head=PackagingPrototype.Read(s,8);long size=PackagingPrototype.Be(head,0);string type=Encoding.ASCII.GetString(head,4,4);long start=s.Position;
                    if(++chunks>100000||size>int.MaxValue||size>s.Length-start-4||!System.Text.RegularExpressions.Regex.IsMatch(type,"^[A-Za-z]{2}[A-Z][A-Za-z]$")||chunks==1&&(type!="IHDR"||size!=13)||chunks>1&&type=="IHDR")throw new InvalidDataException("PNG 数据块无效 / Invalid PNG chunk");
                    if(type=="IDAT"){if(idat)throw new InvalidDataException("IDAT 测试格式要求单个图像块 / Expected single IDAT");idat=true;if(offset>start&&offset<start+size&&offset+length==start+size)found=true;}
                    else{other+=size+12;if(other>FileDisguise.CoverLimit||type!="IEND"&&idat||new[]{"acTL","fcTL","fdAT","aiMd","aiPd"}.Contains(type))throw new InvalidDataException("IDAT PNG 结构无效 / Invalid IDAT PNG structure");}
                    uint crc=PackagingPrototype.Crc(0xffffffff,head.Skip(4).ToArray(),4);long left=size;
                    while(left>0){token.ThrowIfCancellationRequested();int n=s.Read(buffer,0,(int)Math.Min(left,buffer.Length));if(n<=0)throw new EndOfStreamException();crc=PackagingPrototype.Crc(crc,buffer,n);left-=n;if(progress!=null)progress.Report(new FileProgress{Stage="检查 IDAT / Check IDAT",Completed=s.Position,Total=s.Length});}
                    if(PackagingPrototype.Be(PackagingPrototype.Read(s,4),0)!=(crc^0xffffffff))throw new InvalidDataException("PNG CRC 校验失败 / PNG CRC mismatch");
                    if(type=="IEND"){if(size!=0||!found||s.Position!=s.Length)throw new InvalidDataException("IDAT PNG 结束无效 / Invalid IDAT ending");break;}
                }
                s.Position=offset;string hash=PackagingPrototype.Hash(s,length,token,progress,"复核 IDAT / Verify IDAT");
                return new Info{Record=record,Offset=offset,Length=length,Comment=comment-16,StoredHash=hash};
            }
        }
        public static HiddenFileInfo Check(string path,CancellationToken token,IProgress<FileProgress> progress=null)
        {var i=Inspect(path,token,progress);return new HiddenFileInfo{Name=i.Record.original_name,Extension=Path.GetExtension(i.Record.original_name),Length=i.Record.original_length,Offset=i.Offset,Sha256=i.Record.original_sha256,StoredSha256=i.StoredHash};}
        public static void Pack(byte[] jpeg,string wrapper,string output,string name,long originalLength,string originalHash,CancellationToken token,IProgress<FileProgress> progress)
        {
            byte[] cover=PackagingPrototype.PngCover(jpeg);var r=new Record{original_name=name,original_length=originalLength,original_sha256=originalHash};Validate(r);
            byte[] metadata=Magic.Concat(Encoding.UTF8.GetBytes(Inspection.Json().Serialize(r))).ToArray();if(metadata.Length>2048)throw new InvalidDataException("IDAT 记录过大 / IDAT record too large");
            using(var prefix=new MemoryStream())using(var compressed=new MemoryStream()){
                prefix.Write(Signature,0,8);for(int at=8;at<cover.Length;){int size=checked((int)PackagingPrototype.Be(cover,at));string type=Encoding.ASCII.GetString(cover,at+4,4);if(size>cover.Length-at-12)throw new InvalidDataException("PNG 封面损坏 / Invalid cover");if(type=="IDAT")compressed.Write(cover,at+8,size);else if(type!="IEND")prefix.Write(cover,at,size+12);at+=size+12;}
                long length=new FileInfo(wrapper).Length,total=compressed.Length+length+metadata.Length;if(total>int.MaxValue)throw new InvalidDataException("IDAT 单块超过约 2 GiB / IDAT chunk over approximately 2 GiB");
                PackagingPrototype.Space(output,total);string temp=PackagingPrototype.Temp(output);try{
                    using(var source=File.OpenRead(wrapper))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,FileDisguise.BufferSize)){
                        long end=PackagingPrototype.ZipEnd(source,0,length,0);source.Position=end+20;if(BitConverter.ToUInt16(PackagingPrototype.Read(source,2),0)!=0)throw new InvalidDataException("包裹 ZIP 已有注释 / Wrapper comment must be empty");
                        var before=prefix.ToArray();var image=compressed.ToArray();target.Write(before,0,before.Length);long chunkAt=target.Position;target.Write(PackagingPrototype.Big((uint)total),0,4);target.Write(Encoding.ASCII.GetBytes("IDAT"),0,4);long dataAt=target.Position;target.Write(image,0,image.Length);long offset=target.Position;
                        source.Position=0;PackagingPrototype.Hash(source,length,token,progress,"封装 IDAT / Package IDAT",target);target.Write(metadata,0,metadata.Length);
                        FileDisguise.ZipOffsets(target,offset,length,offset,token);target.Position=offset+end+20;target.Write(BitConverter.GetBytes((ushort)(metadata.Length+16)),0,2);
                        target.Position=dataAt;uint crc=PackagingPrototype.Crc(0xffffffff,Encoding.ASCII.GetBytes("IDAT"),4);var b=new byte[FileDisguise.BufferSize];long left=total;while(left>0){token.ThrowIfCancellationRequested();int n=target.Read(b,0,(int)Math.Min(left,b.Length));if(n==0)throw new EndOfStreamException();crc=PackagingPrototype.Crc(crc,b,n);left-=n;}
                        target.Write(PackagingPrototype.Big(crc^0xffffffff),0,4);target.Write(End,0,12);target.Flush(true);
                    }
                    var verified=Inspect(temp,token,progress);if(verified.Record.original_sha256!=originalHash)throw new IOException("IDAT 写入校验失败 / IDAT verification failed");token.ThrowIfCancellationRequested();File.Move(temp,output);
                }finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
        public static string Restore(string path,string folder,CancellationToken token,IProgress<FileProgress> progress)
        {
            var i=Inspect(path,token,progress);string output=FileDisguise.RestoredName(folder,i.Record.original_name);PackagingPrototype.Space(output,i.Length+i.Record.original_length);string temp=PackagingPrototype.Temp(output),original=PackagingPrototype.Temp(output);
            try{
                using(var source=File.OpenRead(path))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite)){
                    source.Position=i.Offset;if(PackagingPrototype.Hash(source,i.Length,token,progress,"还原 IDAT / Restore IDAT",target)!=i.StoredHash)throw new IOException("输入发生变化 / Input changed");
                    long end=PackagingPrototype.ZipEnd(target,0,i.Length,16);target.Position=end+20;target.Write(BitConverter.GetBytes((ushort)i.Comment),0,2);FileDisguise.ZipOffsets(target,0,i.Length,-i.Offset,token);target.Flush(true);
                }
                using(var zip=new ZipArchive(File.OpenRead(temp),ZipArchiveMode.Read)){
                    if(zip.Entries.Count!=1||zip.Entries[0].FullName!=i.Record.original_name||zip.Entries[0].Length!=i.Record.original_length)throw new InvalidDataException("包裹内容不匹配 / Wrapper entry mismatch");
                    using(var entry=zip.Entries[0].Open())using(var target=new FileStream(original,FileMode.CreateNew,FileAccess.Write)){
                        if(PackagingPrototype.Hash(entry,i.Record.original_length,token,progress,"核验原文件 / Verify original",target)!=i.Record.original_sha256||entry.ReadByte()!=-1)throw new InvalidDataException("原文件 SHA-256 不匹配 / Original SHA-256 mismatch");target.Flush(true);
                    }
                }
                FileDisguise.ValidateFileAs(original,Path.GetExtension(i.Record.original_name),token);token.ThrowIfCancellationRequested();File.Move(original,output);return output;
            }finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(original))File.Delete(original);}
        }
    }
}
