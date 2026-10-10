package com.guangyingruoshui.imagetools;

import android.graphics.Bitmap;
import java.io.*;
import java.util.*;

// Compatibility reader only. New transparent dual-image generation has been removed.
final class DualBlend {
    static int channel(double n){return (int)Math.max(0,Math.min(255,Math.floor(n+.5)));}
    // Read-only compatibility for old transparent dual PNGs; no new-image encoder.
    static boolean isLegacy(File file)throws IOException{
        try(RandomAccessFile r=new RandomAccessFile(file,"r")){
            if(!Arrays.equals(Files.read(r,8),Files.PNG))throw new IOException("请选择 PNG 原图 / Select a PNG");
            boolean alpha=false,animated=false;
            while(r.getFilePointer()<r.length()){
                byte[] head=Files.read(r,8);long length=Files.be(head,0,4);Files.range(r.getFilePointer(),length+4,r.length());
                String type=new String(head,4,4,Files.LATIN);
                if(type.equals("duAL")){background(file);return !animated;}
                if(type.equals("acTL"))animated=true;
                if(type.equals("IHDR")){if(length!=13)throw new IOException("Invalid PNG header");byte[] data=Files.read(r,13);alpha=data[9]==4||data[9]==6;r.skipBytes(4);continue;}
                if(type.equals("tRNS"))alpha=true;
                if(type.equals("IDAT")||type.equals("IEND"))return !animated&&alpha;
                r.seek(r.getFilePointer()+length+4);
            }throw new IOException("Truncated PNG");
        }
    }
    static Bitmap render(Bitmap input,boolean white,Files.Job job)throws Exception{return transform(input,white,false,job,0);}
    static Bitmap extract(Bitmap input,boolean white,Files.Job job,int dark)throws Exception{return transform(input,white,true,job,dark);}
    static Bitmap transform(Bitmap input,boolean white,boolean requireAlpha,Files.Job job,int dark)throws Exception{
        if(dark<0||dark>128)throw new IllegalArgumentException("Invalid dual parameters");
        job.check();int w=input.getWidth(),h=input.getHeight();Bitmap result=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);
        boolean success=false,transparent=false;int[] row=new int[w];
        try{for(int y=0;y<h;y++){
            job.check();input.getPixels(row,0,w,0,y,w,1);
            for(int x=0;x<w;x++){int v=row[x],a=v>>>24;double bg=(white?255:dark)*(255-a)/255.0;transparent|=a<255;
                row[x]=0xff000000|channel(((v>>>16)&255)*a/255.0+bg)<<16|channel(((v>>>8)&255)*a/255.0+bg)<<8|channel((v&255)*a/255.0+bg);
            }result.setPixels(row,0,w,0,y,w,1);
        }if(requireAlpha&&!transparent)throw new IOException("图片没有透明度，请使用 PNG 原图 / No transparency; use the original PNG");success=true;return result;
        }finally{if(!success)result.recycle();}
    }
    static int background(File file)throws IOException{
        try(RandomAccessFile r=new RandomAccessFile(file,"r")){
            if(!Arrays.equals(Files.read(r,8),Files.PNG))throw new IOException("请选择 PNG 原图 / Select a PNG");
            while(r.getFilePointer()<r.length()){
                byte[] head=Files.read(r,8);long length=Files.be(head,0,4);Files.range(r.getFilePointer(),length+4,r.length());
                String type=new String(head,4,4,Files.LATIN);
                if(type.equals("duAL")){
                    if(length!=4)throw new IOException("Invalid dual parameters");
                    byte[] data=Files.read(r,4),check=Files.read(r,4);java.util.zip.CRC32 crc=new java.util.zip.CRC32();crc.update(head,4,4);crc.update(data);
                    if(crc.getValue()!=Files.be(check,0,4)||data[0]!=1||data[1]<1||data[1]>2||(data[2]&255)>128||(data[3]&255)>100)throw new IOException("双图参数校验失败 / Invalid dual parameters");
                    return data[2]&255;
                }
                if(type.equals("IDAT")||type.equals("IEND"))return 0;
                r.seek(r.getFilePointer()+length+4);
            }
            throw new IOException("Truncated PNG");
        }
    }
    static List<File> restore(File input,File dir,int bound,Files.Job job)throws Exception{
        int dark=background(input);Bitmap source=Images.load(input,bound,bound==0);ArrayList<File> outputs=new ArrayList<>();boolean success=false;
        try{Files.space(dir,(long)source.getWidth()*source.getHeight()*9);
            for(boolean white:new boolean[]{true,false}){
                Bitmap display=extract(source,white,job,dark);File tmp=null;
                try{tmp=Files.temp(dir);File out=Files.unique(dir,Files.stem(input.getName())+(white?"_白底显示.png":"_黑底显示.png"));Images.encode(display,tmp,0,job);Files.commit(tmp,out,job);outputs.add(out);}
                finally{display.recycle();if(tmp!=null)tmp.delete();}
            }success=true;return outputs;
        }finally{source.recycle();if(!success)for(File out:outputs)out.delete();}
    }
}
