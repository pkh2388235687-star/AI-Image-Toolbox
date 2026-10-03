package com.guangyingruoshui.imagetools;

import android.graphics.*;
import java.io.*;
import java.util.*;

final class Gif {
    // Streaming literal LZW blocks: bounded memory, valid GIF89a on API 21 and later.
    static final class Bits {OutputStream out;ByteArrayOutputStream block=new ByteArrayOutputStream(255);int bits,count;Bits(OutputStream o){out=o;}void code(int c)throws Exception{bits|=c<<count;count+=9;while(count>=8){write(bits&255);bits>>>=8;count-=8;}}void write(int b)throws Exception{block.write(b);if(block.size()==255)flush();}void flush()throws Exception{if(block.size()>0){out.write(block.size());block.writeTo(out);block.reset();}}void finish()throws Exception{if(count>0)write(bits&255);flush();out.write(0);}}
    static void word(OutputStream o,int v)throws Exception{o.write(v);o.write(v>>8);}
    static File encode(List<File> inputs,File dir,int size,int delay,boolean loop,int background,Files.Job j)throws Exception{if(inputs.size()<2||inputs.size()>200)throw new IOException("请选择 2–200 张图片 / Choose 2–200 images");Bitmap first=Images.load(inputs.get(0),size,false);int w=first.getWidth(),h=first.getHeight();first.recycle();File out=Files.unique(dir,"合成.gif"),tmp=Files.temp(dir);try(OutputStream o=new BufferedOutputStream(new FileOutputStream(tmp))){o.write("GIF89a".getBytes(Files.LATIN));word(o,w);word(o,h);o.write(0xf7);o.write(0);o.write(0);for(int i=0;i<256;i++){o.write((i>>5)*255/7);o.write(((i>>2)&7)*255/7);o.write((i&3)*255/3);}if(loop)o.write(new byte[]{33,(byte)255,11,78,69,84,83,67,65,80,69,50,46,48,3,1,0,0,0});int[] row=new int[w];int frame=0;for(File input:inputs){j.check();Bitmap image=Images.load(input,size,false),fit=Images.fit(image,w,h,background);image.recycle();try{o.write(new byte[]{33,(byte)249,4,4});word(o,Math.max(2,delay/10));o.write(0);o.write(0);o.write(44);word(o,0);word(o,0);word(o,w);word(o,h);o.write(0);o.write(8);Bits bits=new Bits(o);bits.code(256);int codes=0;for(int y=0;y<h;y++){j.check();fit.getPixels(row,0,w,0,y,w,1);for(int v:row){if(codes==250){bits.code(256);codes=0;}int color=((v>>>21)&7)<<5|((v>>>13)&7)<<2|((v>>>6)&3);bits.code(color);codes++;}}bits.code(257);bits.finish();}finally{fit.recycle();}if(j.progress!=null)j.progress.update(++frame,inputs.size());}o.write(59);}catch(Exception e){tmp.delete();throw e;}try{Files.commit(tmp,out,j);return out;}finally{tmp.delete();}}
}
