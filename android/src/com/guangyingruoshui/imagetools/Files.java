package com.guangyingruoshui.imagetools;

import java.io.*;
import java.nio.charset.Charset;
import java.security.MessageDigest;
import java.util.*;
import java.util.zip.*;

final class Files {
    static final Charset UTF8=Charset.forName("UTF-8"), LATIN=Charset.forName("ISO-8859-1");
    static final byte[] PNG={(byte)137,80,78,71,13,10,26,10};
    interface Progress {void update(long done,long total);}
    static class Job {volatile boolean cancelled;Progress progress;void check() throws IOException {if(cancelled||Thread.currentThread().isInterrupted())throw new InterruptedIOException("操作已取消 / Cancelled");}}
    static void range(long start,long length,long total)throws IOException{if(start<0||length<0||start>total||length>total-start)throw new IOException("文件已截断或长度无效 / Invalid file length");}
    static byte[] read(InputStream s,int size)throws IOException{if(size<0||size>32*1024*1024)throw new IOException("数据块过大 / Oversized chunk");byte[] b=new byte[size];int p=0,n;while(p<size&&(n=s.read(b,p,size-p))>0)p+=n;if(p!=size)throw new EOFException("文件不完整 / Truncated file");return b;}
    static byte[] read(RandomAccessFile s,int n)throws IOException{byte[] b=new byte[n];s.readFully(b);return b;}
    static long copy(InputStream s,OutputStream out,Job j)throws IOException{byte[] b=new byte[1024*1024];long done=0,last=0;int n;while((n=s.read(b))!=-1){j.check();out.write(b,0,n);done+=n;if(j.progress!=null&&System.currentTimeMillis()-last>120){j.progress.update(done,-1);last=System.currentTimeMillis();}}j.check();return done;}
    static String hash(RandomAccessFile s,long size,OutputStream out,Job j)throws Exception{MessageDigest md=MessageDigest.getInstance("SHA-256");byte[] b=new byte[1024*1024];long done=0,last=0;range(s.getFilePointer(),size,s.length());while(done<size){j.check();int n=s.read(b,0,(int)Math.min(size-done,b.length));if(n<0)throw new EOFException();md.update(b,0,n);if(out!=null)out.write(b,0,n);done+=n;if(j.progress!=null&&System.currentTimeMillis()-last>120){j.progress.update(done,size);last=System.currentTimeMillis();}}return hex(md.digest());}
    static String hash(File f,Job j)throws Exception{try(RandomAccessFile s=new RandomAccessFile(f,"r")){return hash(s,s.length(),null,j);}}
    static String hex(byte[] b){StringBuilder s=new StringBuilder();for(byte v:b)s.append(String.format(Locale.ROOT,"%02x",v&255));return s.toString();}
    static byte[] unhex(String s)throws IOException{if(s==null||!s.matches("[a-fA-F0-9]{64}"))throw new IOException("无效校验值 / Invalid hash");byte[] b=new byte[32];for(int i=0;i<32;i++)b[i]=(byte)Integer.parseInt(s.substring(i*2,i*2+2),16);return b;}
    static String name(String s)throws IOException{if(s==null||s.length()==0||s.equals(".")||s.equals("..")||s.matches(".*[\\\\/:*?\"<>|\\x00-\\x1f].*")||s.endsWith(".")||s.endsWith(" ")||s.getBytes(UTF8).length>1024)throw new IOException("原文件名无效 / Invalid filename");String base=s.split("\\.")[0].toUpperCase(Locale.ROOT);if(base.matches("CON|PRN|AUX|NUL|CLOCK\\$|COM[1-9]|LPT[1-9]"))throw new IOException("保留文件名 / Reserved name");return s;}
    static String stem(String s){int i=s.lastIndexOf('.');return i>0?s.substring(0,i):s;}
    static String ext(String s){int i=s.lastIndexOf('.');return i<0?"":s.substring(i).toLowerCase(Locale.ROOT);}
    static File unique(File dir,String name)throws IOException{if(!dir.isDirectory()&&!dir.mkdirs())throw new IOException("无法创建目录 / Cannot create folder");name(name);String ext=ext(name),stem=stem(name);while(stem.getBytes(UTF8).length>180)stem=stem.substring(0,stem.offsetByCodePoints(stem.length(),-1));File f=new File(dir,stem+ext);int n=2;while(f.exists())f=new File(dir,stem+"_"+(n++)+ext);return f;}
    static File temp(File dir)throws IOException{return File.createTempFile("aipt-",".tmp",dir);}
    static void space(File dir,long bytes)throws IOException{if(bytes<0||bytes>dir.getUsableSpace()-1024*1024)throw new IOException("存储空间不足 / Insufficient storage");}
    static void commit(File tmp,File out,Job j)throws IOException{j.check();if(out.exists()||!tmp.renameTo(out))throw new IOException("无法提交输出 / Cannot commit output");}
    static long le(byte[] b,int p,int n)throws IOException{if(p<0||p+n>b.length)throw new EOFException();long v=0;for(int i=0;i<n;i++)v|=(long)(b[p+i]&255)<<(8*i);if(n==8&&v<0)throw new IOException("长度溢出 / Length overflow");return v;}
    static void le(byte[] b,int p,long v,int n){for(int i=0;i<n;i++)b[p+i]=(byte)(v>>>(8*i));}
    static long be(byte[] b,int p,int n){long v=0;for(int i=0;i<n;i++)v=v<<8|(b[p+i]&255);return v;}
    static byte[] be(long...values){byte[] b=new byte[values.length*4];for(int k=0;k<values.length;k++)for(int i=0;i<4;i++)b[k*4+i]=(byte)(values[k]>>>(24-i*8));return b;}
    static byte[] concat(byte[]...all){int n=0;for(byte[] b:all)n+=b.length;byte[] r=new byte[n];int p=0;for(byte[] b:all){System.arraycopy(b,0,r,p,b.length);p+=b.length;}return r;}
    static boolean starts(byte[] b,byte[] magic){if(b.length<magic.length)return false;for(int i=0;i<magic.length;i++)if(b[i]!=magic[i])return false;return true;}
    static void chunk(OutputStream out,String type,byte[] data)throws IOException{byte[] t=type.getBytes(LATIN);CRC32 c=new CRC32();c.update(t);c.update(data);out.write(be(data.length));out.write(t);out.write(data);out.write(be(c.getValue()));}
    static int zero(byte[] b,int from)throws IOException{for(int i=from;i<b.length;i++)if(b[i]==0)return i;throw new IOException("文本块不完整 / Invalid text chunk");}
    static byte[] inflate(byte[] b,int offset,int size)throws IOException{try(InputStream in=new InflaterInputStream(new ByteArrayInputStream(b,offset,size));ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buf=new byte[8192];int n;while((n=in.read(buf))!=-1){if(out.size()+n>8*1024*1024)throw new IOException("生成信息过大 / Metadata too large");out.write(buf,0,n);}return out.toByteArray();}}
    static File dualRestore(File input,File dir,Job job)throws Exception{
        File out=unique(dir,stem(input.getName())+"_还原.png"),tmp=temp(dir);boolean found=false;int frame=0;
        try(InputStream s=new BufferedInputStream(new FileInputStream(input));OutputStream o=new BufferedOutputStream(new FileOutputStream(tmp))){if(!Arrays.equals(read(s,8),PNG))throw new IOException("请选择双图 PNG / Select a dual PNG");o.write(PNG);long sequence=0;while(true){job.check();byte[] head=read(s,8);long length=be(head,0,4);if(length>64*1024*1024)throw new IOException("PNG 数据块过大 / Oversized PNG chunk");String type=new String(head,4,4,LATIN);byte[] b=read(s,(int)length),crc=read(s,4);CRC32 c=new CRC32();c.update(head,4,4);c.update(b);if(c.getValue()!=be(crc,0,4))throw new IOException("PNG 校验失败 / PNG CRC mismatch");
            if(type.equals("IHDR")||type.equals("PLTE")||type.equals("tRNS")||type.equals("iCCP")||type.equals("sRGB")||type.equals("gAMA")||type.equals("cHRM"))chunk(o,type,b);
            if(type.equals("fcTL")){frame++;if(frame==1&&(length!=26||be(b,12,4)!=0||be(b,16,4)!=0))throw new IOException("不支持此动画帧 / Unsupported frame");}
            if(type.equals("fdAT")&&frame==1){if(length<4||be(b,0,4)!=++sequence)throw new IOException("动画序列无效 / Invalid frame sequence");chunk(o,"IDAT",Arrays.copyOfRange(b,4,b.length));found=true;}
            if(type.equals("IEND")){if(!found)throw new IOException("没有隐藏图 / No hidden image");chunk(o,"IEND",new byte[0]);break;}
        }}catch(Exception e){tmp.delete();throw e;}try{commit(tmp,out,job);return out;}finally{tmp.delete();}
    }
}
