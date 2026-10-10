package com.guangyingruoshui.imagetools;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import java.io.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.zip.*;
import org.json.JSONObject;

/** Independent compatibility-test implementation. No network or automatic execution. */
final class PackagingPrototype {
    static final byte[] END={0,0,0,0,73,69,78,68,(byte)174,66,96,(byte)130};
    static final int BUFFER=1024*1024, COVER_LIMIT=512*1024;
    static class Info {
        JSONObject record; long offset,length; String originalName,originalHash; long originalLength;
    }
    static byte[] read(RandomAccessFile s,int n)throws Exception{return Files.read(s,n);}
    static String text(byte[] b){return new String(b,Files.UTF8);}
    static void be(RandomAccessFile s,long value)throws Exception{s.writeInt((int)value);}
    static void chunk(RandomAccessFile s,String name,byte[] data)throws Exception{
        byte[] type=name.getBytes(Files.LATIN);CRC32 crc=new CRC32();crc.update(type);crc.update(data);
        be(s,data.length);s.write(type);s.write(data);be(s,crc.getValue());
    }
    static void validateRecord(JSONObject r)throws Exception{
        int version=r.getInt("version");long length=r.getLong("length");String name=Files.name(r.getString("name")),kind=r.getString("kind");
        if(!r.getString("format").equals("AIPTPNG")||(version!=1&&version!=2)||length<=0||length>Integer.MAX_VALUE||!Arrays.asList("zip","rar","7z","mp4").contains(kind)||!Files.ext(name).equals("."+kind))throw new IOException("PNG 文件记录无效 / Invalid PNG record");
        Files.unhex(r.getString("sha256"));Files.unhex(r.getString("stored_sha256"));
        if(kind.equals("zip")){long comment=r.getLong("zip_comment");if(comment<0||comment>65519)throw new IOException("ZIP 注释过长 / ZIP comment too long");}
        else if(!r.isNull("zip_comment"))throw new IOException("ZIP 记录无效 / Invalid ZIP record");
        if(version==2){String original=Files.name(r.getString("original_name"));long originalLength=r.getLong("original_length");Files.unhex(r.getString("original_sha256"));if(!kind.equals("zip")||originalLength<=0||originalLength>Integer.MAX_VALUE||!Arrays.asList(".mp4",".zip",".rar",".7z").contains(Files.ext(original)))throw new IOException("原文件记录无效 / Invalid original record");}
    }
    static Info check(File file,Files.Job job)throws Exception{
        if(IdatPackaging.hasRecord(file))return IdatPackaging.check(file,job);
        try(RandomAccessFile s=new RandomAccessFile(file,"r")){
            if(!Arrays.equals(read(s,8),Files.PNG))throw new IOException("不是 PNG / Not a PNG");
            Info info=new Info();boolean idat=false,payload=false;int count=0;String stored=null;long coverBytes=8,last=0;byte[] buffer=new byte[BUFFER];
            while(true){job.check();long length=((long)s.readInt())&0xffffffffL;byte[] type=read(s,4);String kind=new String(type,Files.LATIN);
                if(++count>100000||length>Integer.MAX_VALUE||length>s.length()-s.getFilePointer()-4||!kind.matches("[A-Za-z]{2}[A-Z][A-Za-z]"))throw new IOException("PNG 数据块无效 / Invalid PNG chunk");
                if(count==1&&(!kind.equals("IHDR")||length!=13)||kind.equals("IHDR")&&count!=1||Arrays.asList("acTL","fcTL","fdAT").contains(kind))throw new IOException("PNG 封面无效或为动画 / Invalid or animated PNG cover");
                if(kind.equals("IDAT")){if(payload)throw new IOException("PNG 块顺序无效 / Invalid PNG order");idat=true;}
                if(kind.equals("aiMd")&&(info.record!=null||length>2048||!idat||payload))throw new IOException("重复或无效 PNG 记录 / Duplicate PNG record");
                if(kind.equals("aiPd")){if(info.record==null||payload||!idat||length!=info.record.getLong("length"))throw new IOException("PNG 文件块无效 / Invalid PNG payload");payload=true;info.offset=s.getFilePointer();info.length=length;}
                else if(payload&&!kind.equals("IEND"))throw new IOException("文件块后有多余数据 / Extra data after payload");
                if(!kind.equals("aiMd")&&!kind.equals("aiPd")){coverBytes+=length+12;if(coverBytes>COVER_LIMIT)throw new IOException("PNG 封面过大 / PNG cover too large");}
                CRC32 crc=new CRC32();crc.update(type);MessageDigest sha=kind.equals("aiPd")?MessageDigest.getInstance("SHA-256"):null;ByteArrayOutputStream meta=kind.equals("aiMd")?new ByteArrayOutputStream():null;long remaining=length;
                while(remaining>0){job.check();int n=s.read(buffer,0,(int)Math.min(buffer.length,remaining));if(n<=0)throw new EOFException();crc.update(buffer,0,n);if(sha!=null)sha.update(buffer,0,n);if(meta!=null)meta.write(buffer,0,n);remaining-=n;if(job.progress!=null&&(remaining==0||System.currentTimeMillis()-last>120)){job.progress.update(s.getFilePointer(),s.length());last=System.currentTimeMillis();}}
                if((((long)s.readInt())&0xffffffffL)!=crc.getValue())throw new IOException("PNG CRC 校验失败 / PNG CRC mismatch");
                if(meta!=null){info.record=new JSONObject(text(meta.toByteArray()));validateRecord(info.record);}if(sha!=null)stored=Files.hex(sha.digest());
                if(kind.equals("IEND")){if(length!=0||!idat||s.getFilePointer()!=s.length())throw new IOException("PNG 文件尾无效 / Invalid PNG ending");break;}
            }
            if(info.record==null||!payload||!info.record.getString("stored_sha256").equals(stored))throw new IOException("无文件记录或校验失败；图片可能被重新编码 / Missing record or checksum mismatch; image may be re-encoded");
            boolean wrapped=info.record.getInt("version")==2;info.originalName=wrapped?info.record.getString("original_name"):info.record.getString("name");info.originalHash=wrapped?info.record.getString("original_sha256"):info.record.getString("sha256");info.originalLength=wrapped?info.record.getLong("original_length"):info.length;return info;
        }
    }
    static byte[] pngCover(byte[] cover,Files.Job job)throws Exception{
        if(Files.starts(cover,Files.PNG)&&cover.length<=COVER_LIMIT&&Arrays.equals(Arrays.copyOfRange(cover,cover.length-12,cover.length),END))return cover;
        Bitmap original=BitmapFactory.decodeByteArray(cover,0,cover.length);if(original==null)throw new IOException("无法读取封面 / Cannot decode cover");
        try{int w=original.getWidth(),h=original.getHeight();for(int i=0;i<32;i++){job.check();Bitmap b=Images.fit(original,w,h,Color.WHITE);try{ByteArrayOutputStream out=new ByteArrayOutputStream();if(!b.compress(Bitmap.CompressFormat.PNG,100,out))throw new IOException("封面编码失败 / Cover encoding failed");if(out.size()<=COVER_LIMIT)return out.toByteArray();}finally{b.recycle();}w=Math.max(1,(int)(w*.8));h=Math.max(1,(int)(h*.8));}throw new IOException("PNG 封面过大 / PNG cover too large");}finally{original.recycle();}
    }
    static long zipEnd(RandomAccessFile f,long base,long length,int extra)throws Exception{
        int tail=(int)Math.min(65557,length);f.seek(base+length-tail);byte[] b=read(f,tail);for(int p=b.length-22;p>=0;p--)if(Files.le(b,p,4)==0x06054b50L&&p+22+Files.le(b,p+20,2)==b.length+extra)return base+length-tail+p;throw new IOException("ZIP 结束记录无效 / Invalid ZIP end record");
    }
    static OutputStream writer(final RandomAccessFile file){return new OutputStream(){public void write(int value)throws IOException{file.write(value);}public void write(byte[] b,int p,int n)throws IOException{file.write(b,p,n);}};}
    static File pack(byte[] cover,File source,String originalName,File dir,int mode,Files.Job job)throws Exception{
        if(mode==1)return Archive.pack(cover,source,originalName,dir,job);
        job.check();Files.name(originalName);int kind=Archive.kind(originalName);if(originalName.toLowerCase(Locale.ROOT).matches(".*\\.part\\d+\\.rar"))throw new IOException("不支持分卷 / Multipart input unsupported");
        try(RandomAccessFile s=new RandomAccessFile(source,"r")){Archive.validate(s,kind,job);}
        String suffix=kind==1?"_v2i":"_a2i";
        if(mode==0){File output=Files.unique(dir,Files.stem(originalName)+"_copyb"+suffix+".jpg");Files.space(dir,source.length()+cover.length);File temp=Files.temp(dir);try{
            try(RandomAccessFile input=new RandomAccessFile(source,"r");RandomAccessFile target=new RandomAccessFile(temp,"rw")){target.write(cover);String hash=Files.hash(input,input.length(),writer(target),job);target.getFD().sync();target.seek(cover.length);if(!Files.hash(target,input.length(),null,job).equals(hash))throw new IOException("拼接校验失败 / Concatenation verification failed");}
            Files.commit(temp,output,job);return output;
        }finally{temp.delete();}}
        if(mode!=2&&mode!=3)throw new IOException("封装方式无效 / Invalid packaging method");if(source.length()<=0||source.length()>Integer.MAX_VALUE-4096L)throw new IOException("PNG 包裹最多约 2 GiB / PNG wrapper limit approximately 2 GiB");Files.space(dir,source.length()*2+COVER_LIMIT+4096);
        File wrapper=Files.temp(dir);String originalHash;long originalLength=source.length();
        try{try(RandomAccessFile input=new RandomAccessFile(source,"r");FileOutputStream file=new FileOutputStream(wrapper);ZipOutputStream zip=new ZipOutputStream(file)){zip.setLevel(0);ZipEntry entry=new ZipEntry(originalName);zip.putNextEntry(entry);originalHash=Files.hash(input,originalLength,zip,job);zip.closeEntry();zip.finish();file.getFD().sync();}
            byte[] png=pngCover(cover,job);if(mode==3)return IdatPackaging.pack(png,wrapper,originalName,originalLength,originalHash,dir,job);File output=Files.unique(dir,Files.stem(originalName)+suffix+".png");File temp=Files.temp(dir);try{
                try(RandomAccessFile input=new RandomAccessFile(wrapper,"r");RandomAccessFile target=new RandomAccessFile(temp,"rw")){
                    long length=input.length();if(length>Integer.MAX_VALUE)throw new IOException("PNG 数据块超过限制 / PNG chunk limit exceeded");long e=zipEnd(input,0,length,0);input.seek(e+20);int comment=(int)Files.le(read(input,2),0,2);
                    input.seek(0);JSONObject record=new JSONObject();record.put("format","AIPTPNG");record.put("version",2);record.put("name",originalName+".zip");record.put("kind","zip");record.put("length",length);record.put("sha256",Files.hash(input,length,null,job));record.put("stored_sha256",String.format(Locale.ROOT,"%064d",0));record.put("zip_comment",comment);record.put("original_name",originalName);record.put("original_sha256",originalHash);record.put("original_length",originalLength);validateRecord(record);
                    byte[] metadata=record.toString().getBytes(Files.UTF8);if(metadata.length>2048)throw new IOException("文件记录过大 / Metadata too large");target.write(png,0,png.length-12);long recordAt=target.getFilePointer();chunk(target,"aiMd",metadata);be(target,length);byte[] type="aiPd".getBytes(Files.LATIN);target.write(type);long offset=target.getFilePointer();input.seek(0);if(!Files.hash(input,length,writer(target),job).equals(record.getString("sha256")))throw new IOException("包裹文件发生变化 / Wrapper changed");
                    Archive.zipOffsets(target,offset,length,offset,job);e=zipEnd(target,offset,length,0);target.seek(e+20);byte[] c=new byte[2];Files.le(c,0,comment+16,2);target.write(c);target.seek(offset);record.put("stored_sha256",Files.hash(target,length,null,job));
                    CRC32 crc=new CRC32();crc.update(type);target.seek(offset);byte[] buffer=new byte[BUFFER];long remaining=length;while(remaining>0){job.check();int n=target.read(buffer,0,(int)Math.min(buffer.length,remaining));if(n<=0)throw new EOFException();crc.update(buffer,0,n);remaining-=n;}be(target,crc.getValue());target.write(END);target.seek(recordAt);byte[] finalMetadata=record.toString().getBytes(Files.UTF8);if(finalMetadata.length!=metadata.length)throw new IOException("记录长度改变 / Metadata length changed");chunk(target,"aiMd",finalMetadata);target.getFD().sync();
                }
                check(temp,job);Files.commit(temp,output,job);return output;
            }finally{temp.delete();}
        }finally{wrapper.delete();}
    }
    static File restore(File input,File dir,Files.Job job)throws Exception{
        try(RandomAccessFile s=new RandomAccessFile(input,"r")){
            byte[] signature=read(s,(int)Math.min(8,s.length()));if(!Arrays.equals(signature,Files.PNG)){
                s.seek(0);byte[][] record={null};int end=Archive.jpeg(read(s,(int)Math.min(s.length(),COVER_LIMIT+2048)),record);if(record[0]!=null)return Archive.restore(input,dir,job);return restoreCopy(input,dir,end,job);
            }
        }
        if(IdatPackaging.hasRecord(input))return IdatPackaging.restore(input,dir,job);
        Info info=check(input,job);Files.space(dir,info.length+info.originalLength+COVER_LIMIT);File out=Files.unique(dir,info.originalName),temp=Files.temp(dir),original=Files.temp(dir);try{
            try(RandomAccessFile source=new RandomAccessFile(input,"r");RandomAccessFile target=new RandomAccessFile(temp,"rw")){
                source.seek(info.offset);if(!Files.hash(source,info.length,writer(target),job).equals(info.record.getString("stored_sha256")))throw new IOException("输入发生变化 / Input changed");
                if(info.record.getString("kind").equals("zip")){long end=zipEnd(target,0,info.length,16);target.seek(end+20);byte[] comment=new byte[2];Files.le(comment,0,info.record.getInt("zip_comment"),2);target.write(comment);Archive.zipOffsets(target,0,info.length,-info.offset,job);}
                target.seek(0);if(!Files.hash(target,info.length,null,job).equals(info.record.getString("sha256")))throw new IOException("包裹校验失败 / Wrapper checksum mismatch");target.getFD().sync();
            }
            if(info.record.getInt("version")==2){try(ZipInputStream zip=new ZipInputStream(new FileInputStream(temp));FileOutputStream target=new FileOutputStream(original)){
                ZipEntry entry=zip.getNextEntry();if(entry==null||!entry.getName().equals(info.originalName)||entry.isDirectory())throw new IOException("包裹内容不匹配 / Wrapper entry mismatch");
                byte[] buffer=new byte[BUFFER];MessageDigest sha=MessageDigest.getInstance("SHA-256");long done=0;int n;while((n=zip.read(buffer))!=-1){job.check();if(n>info.originalLength-done)throw new IOException("原文件长度超出记录 / Original length overflow");target.write(buffer,0,n);sha.update(buffer,0,n);done+=n;}if(done!=info.originalLength||!Files.hex(sha.digest()).equals(info.originalHash)||zip.getNextEntry()!=null)throw new IOException("原文件校验失败 / Original checksum mismatch");target.getFD().sync();}
                try(RandomAccessFile s=new RandomAccessFile(original,"r")){Archive.validate(s,Archive.kind(info.originalName),job);}Files.commit(original,out,job);
            }else{try(RandomAccessFile s=new RandomAccessFile(temp,"r")){Archive.validate(s,Archive.kind(info.originalName),job);}Files.commit(temp,out,job);}return out;
        }finally{temp.delete();original.delete();}
    }
    static File restoreCopy(File input,File dir,long offset,Files.Job job)throws Exception{
        String ext;try(RandomAccessFile s=new RandomAccessFile(input,"r")){s.seek(offset);byte[] b=read(s,(int)Math.min(64,s.length()-offset));if(b.length>=4&&b[0]==80&&b[1]==75)ext=".zip";else if(b.length>=7&&new String(b,0,4,Files.LATIN).equals("Rar!"))ext=".rar";else if(b.length>=6&&Files.starts(b,new byte[]{55,122,(byte)188,(byte)175,39,28}))ext=".7z";else if(b.length>=8&&new String(b,4,4,Files.LATIN).equals("ftyp"))ext=".mp4";else throw new IOException("无法识别纯拼接数据 / Unrecognized concatenated data");}
        String stem=Files.stem(input.getName());if(stem.length()>150)stem=stem.substring(0,150);File out=Files.unique(dir,stem+"_restored"+ext);Files.space(dir,input.length()-offset+COVER_LIMIT);File temp=Files.temp(dir);try{
            try(RandomAccessFile source=new RandomAccessFile(input,"r");FileOutputStream target=new FileOutputStream(temp)){source.seek(offset);Files.hash(source,source.length()-offset,target,job);target.getFD().sync();}try(RandomAccessFile s=new RandomAccessFile(temp,"r")){Archive.validate(s,Archive.kind(out.getName()),job);}Files.commit(temp,out,job);return out;
        }finally{temp.delete();}
    }
}
