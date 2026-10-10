package com.guangyingruoshui.imagetools;

import java.io.*;
import java.util.*;
import java.util.zip.*;
import org.json.JSONObject;

/** Test-only IDAT ZIP wrapper. No private PNG chunks or executable scripts. */
final class IdatPackaging {
    static final byte[] MAGIC="AIPTIDAT".getBytes(Files.LATIN);
    static final int BUFFER=1024*1024;
    static class Info {JSONObject record;long offset,length;int comment;String stored;}
    static void validate(JSONObject r)throws Exception {
        String name=Files.name(r.getString("original_name"));long size=r.getLong("original_length");Files.unhex(r.getString("original_sha256"));
        if(!r.getString("format").equals("AIPTIDAT")||r.getInt("version")!=1||size<=0||size>Integer.MAX_VALUE||!Arrays.asList(".mp4",".zip",".rar",".7z").contains(Files.ext(name)))throw new IOException("IDAT 记录无效 / Invalid IDAT record");
    }
    static long endAt(RandomAccessFile s)throws Exception{return PackagingPrototype.zipEnd(s,0,s.length()-16,16);}
    static boolean hasRecord(File file)throws Exception {
        try(RandomAccessFile s=new RandomAccessFile(file,"r")){
            if(s.length()<38)return false;long end;
            try{end=endAt(s);}catch(IOException e){return false;}
            s.seek(end+20);int comment=(int)Files.le(Files.read(s,2),0,2);
            return comment>=24&&Arrays.equals(Files.read(s,8),MAGIC);
        }
    }
    static Info inspect(File file,Files.Job job)throws Exception {
        try(RandomAccessFile s=new RandomAccessFile(file,"r")){
            long end=endAt(s);s.seek(end);byte[] e=Files.read(s,22);int comment=(int)Files.le(e,20,2);
            if(comment<24||comment>2064||Files.le(e,4,2)!=0||Files.le(e,6,2)!=0||Files.le(e,8,2)!=1||Files.le(e,10,2)!=1)throw new IOException("IDAT ZIP 目录无效 / Invalid IDAT ZIP directory");
            byte[] meta=Files.read(s,comment-16);if(!Files.starts(meta,MAGIC))throw new IOException("没有 IDAT 记录；图片可能重新编码 / No IDAT record; image may be re-encoded");
            JSONObject record=new JSONObject(new String(meta,8,meta.length-8,Files.UTF8));validate(record);
            long cd=Files.le(e,16,4),cdSize=Files.le(e,12,4);Files.range(cd,cdSize,end);
            if(cd+cdSize!=end||cdSize<46)throw new IOException("IDAT ZIP 偏移无效 / Invalid IDAT ZIP offsets");
            s.seek(cd);byte[] directory=Files.read(s,46);if(Files.le(directory,0,4)!=0x02014b50L)throw new IOException("ZIP 目录损坏 / Invalid ZIP central header");
            long offset=Files.le(directory,42,4),length=s.length()-16-offset;Files.range(offset,length,s.length()-16);
            s.seek(offset);if(!Arrays.equals(Files.read(s,4),new byte[]{80,75,3,4}))throw new IOException("ZIP 本地头无效 / Invalid ZIP local header");
            s.seek(0);if(!Arrays.equals(Files.read(s,8),Files.PNG))throw new IOException("不是 PNG / Not PNG");
            boolean idat=false,found=false;int chunks=0;long other=8,last=0;byte[] buffer=new byte[BUFFER];
            while(true){job.check();long size=((long)s.readInt())&0xffffffffL;byte[] type=Files.read(s,4);String kind=new String(type,Files.LATIN);long start=s.getFilePointer();
                if(++chunks>100000||size>Integer.MAX_VALUE||size>s.length()-start-4||!kind.matches("[A-Za-z]{2}[A-Z][A-Za-z]")||chunks==1&&(!kind.equals("IHDR")||size!=13)||chunks>1&&kind.equals("IHDR"))throw new IOException("PNG 数据块无效 / Invalid PNG chunk");
                if(kind.equals("IDAT")){if(idat)throw new IOException("要求单个 IDAT 图像块 / Expected single IDAT");idat=true;if(offset>start&&offset<start+size&&offset+length==start+size)found=true;}
                else{other+=size+12;if(other>PackagingPrototype.COVER_LIMIT||!kind.equals("IEND")&&idat||Arrays.asList("acTL","fcTL","fdAT","aiMd","aiPd").contains(kind))throw new IOException("IDAT PNG 结构无效 / Invalid IDAT PNG structure");}
                CRC32 crc=new CRC32();crc.update(type);long left=size;
                while(left>0){job.check();int n=s.read(buffer,0,(int)Math.min(buffer.length,left));if(n<=0)throw new EOFException();crc.update(buffer,0,n);left-=n;if(job.progress!=null&&System.currentTimeMillis()-last>120){job.progress.update(s.getFilePointer(),s.length());last=System.currentTimeMillis();}}
                if((((long)s.readInt())&0xffffffffL)!=crc.getValue())throw new IOException("PNG CRC 校验失败 / PNG CRC mismatch");
                if(kind.equals("IEND")){if(size!=0||!found||s.getFilePointer()!=s.length())throw new IOException("IDAT PNG 结束无效 / Invalid IDAT ending");break;}
            }
            s.seek(offset);Info info=new Info();info.record=record;info.offset=offset;info.length=length;info.comment=comment-16;info.stored=Files.hash(s,length,null,job);return info;
        }
    }
    static PackagingPrototype.Info check(File file,Files.Job job)throws Exception {
        Info i=inspect(file,job);PackagingPrototype.Info result=new PackagingPrototype.Info();result.record=i.record;result.offset=i.offset;result.length=i.length;
        result.originalName=i.record.getString("original_name");result.originalHash=i.record.getString("original_sha256");result.originalLength=i.record.getLong("original_length");return result;
    }
    static File pack(byte[] cover,File wrapper,String name,long originalLength,String originalHash,File dir,Files.Job job)throws Exception {
        JSONObject record=new JSONObject();record.put("format","AIPTIDAT");record.put("version",1);record.put("original_name",name);record.put("original_length",originalLength);record.put("original_sha256",originalHash);validate(record);
        byte[] metadata=Files.concat(MAGIC,record.toString().getBytes(Files.UTF8));if(metadata.length>2048)throw new IOException("IDAT 记录过大 / IDAT record too large");
        ByteArrayOutputStream prefix=new ByteArrayOutputStream(),compressed=new ByteArrayOutputStream();prefix.write(Files.PNG);
        for(int at=8;at<cover.length;){long size=Files.be(cover,at,4);if(size>cover.length-at-12)throw new IOException("PNG 封面损坏 / Invalid PNG cover");String kind=new String(cover,at+4,4,Files.LATIN);if(kind.equals("IDAT"))compressed.write(cover,at+8,(int)size);else if(!kind.equals("IEND"))prefix.write(cover,at,(int)size+12);at+=(int)size+12;}
        long length=wrapper.length(),total=compressed.size()+length+metadata.length;if(total>Integer.MAX_VALUE)throw new IOException("IDAT 单块超过约 2 GiB / IDAT chunk over approximately 2 GiB");Files.space(dir,total+PackagingPrototype.COVER_LIMIT);
        File output=Files.unique(dir,Files.stem(name)+(Files.ext(name).equals(".mp4")?"_v2i":"_a2i")+".png"),temp=Files.temp(dir);
        try{
            try(RandomAccessFile input=new RandomAccessFile(wrapper,"r");RandomAccessFile target=new RandomAccessFile(temp,"rw")){
                long end=PackagingPrototype.zipEnd(input,0,length,0);input.seek(end+20);if(Files.le(Files.read(input,2),0,2)!=0)throw new IOException("ZIP 包裹注释必须为空 / Wrapper comment must be empty");
                target.write(prefix.toByteArray());target.writeInt((int)total);target.write("IDAT".getBytes(Files.LATIN));long dataAt=target.getFilePointer();target.write(compressed.toByteArray());long offset=target.getFilePointer();
                input.seek(0);Files.hash(input,length,PackagingPrototype.writer(target),job);target.write(metadata);Archive.zipOffsets(target,offset,length,offset,job);
                target.seek(offset+end+20);byte[] c=new byte[2];Files.le(c,0,metadata.length+16,2);target.write(c);
                target.seek(dataAt);CRC32 crc=new CRC32();crc.update("IDAT".getBytes(Files.LATIN));byte[] buffer=new byte[BUFFER];long left=total;
                while(left>0){job.check();int n=target.read(buffer,0,(int)Math.min(buffer.length,left));if(n<=0)throw new EOFException();crc.update(buffer,0,n);left-=n;}
                target.writeInt((int)crc.getValue());target.write(PackagingPrototype.END);target.getFD().sync();
            }
            Info checked=inspect(temp,job);if(!checked.record.getString("original_sha256").equals(originalHash))throw new IOException("IDAT 写入校验失败 / IDAT verification failed");Files.commit(temp,output,job);return output;
        }finally{temp.delete();}
    }
    static File restore(File input,File dir,Files.Job job)throws Exception {
        Info i=inspect(input,job);String name=i.record.getString("original_name");long originalLength=i.record.getLong("original_length");File output=Files.unique(dir,name);Files.space(dir,i.length+originalLength+PackagingPrototype.COVER_LIMIT);File temp=Files.temp(dir),original=Files.temp(dir);
        try{
            try(RandomAccessFile source=new RandomAccessFile(input,"r");RandomAccessFile target=new RandomAccessFile(temp,"rw")){
                source.seek(i.offset);if(!Files.hash(source,i.length,PackagingPrototype.writer(target),job).equals(i.stored))throw new IOException("输入发生变化 / Input changed");
                long end=PackagingPrototype.zipEnd(target,0,i.length,16);target.seek(end+20);byte[] c=new byte[2];Files.le(c,0,i.comment,2);target.write(c);Archive.zipOffsets(target,0,i.length,-i.offset,job);target.getFD().sync();
            }
            try(ZipInputStream zip=new ZipInputStream(new FileInputStream(temp));FileOutputStream target=new FileOutputStream(original)){
                ZipEntry entry=zip.getNextEntry();if(entry==null||entry.isDirectory()||!entry.getName().equals(name))throw new IOException("包裹内容不匹配 / Wrapper entry mismatch");
                java.security.MessageDigest sha=java.security.MessageDigest.getInstance("SHA-256");long done=0;byte[] b=new byte[BUFFER];int n;
                while((n=zip.read(b))!=-1){job.check();if(n>originalLength-done)throw new IOException("原文件长度超出记录 / Original length overflow");target.write(b,0,n);sha.update(b,0,n);done+=n;}
                if(done!=originalLength||!Files.hex(sha.digest()).equals(i.record.getString("original_sha256"))||zip.getNextEntry()!=null)throw new IOException("原文件 SHA-256 不匹配 / Original SHA-256 mismatch");target.getFD().sync();
            }
            try(RandomAccessFile s=new RandomAccessFile(original,"r")){Archive.validate(s,Archive.kind(name),job);}Files.commit(original,output,job);return output;
        }finally{temp.delete();original.delete();}
    }
}
