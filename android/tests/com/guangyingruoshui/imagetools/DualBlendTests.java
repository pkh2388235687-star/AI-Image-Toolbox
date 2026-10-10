package com.guangyingruoshui.imagetools;
import android.graphics.Bitmap;import android.util.Base64;import java.io.*;import java.util.*;
final class DualBlendTests {
    static final String[] SAMPLES={"iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAQBBbnGecQAAACtJREFUeJxj1AxJydbnkmLW5ZJg1uGSZGK6r/zn7nd1jrvcmsJ3pbWl7gEAhvsJiQIsXQUAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBATBBsTeoggAAACtJREFUeJxjDCxpzFbnFGVW4RBhVuYQYWJ6Kvv37g959rucivx3xZVE7wEAjpMJmuuBZh0AAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAYBBVfIGOgAAAChJREFUeJxjnLJqU7YEKy+zGCsPCDMxfRD8d/e3MMtdNhHOu3yivPcAm0EJuBoYAuoAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAgBBbDcgKAAAACtJREFUeJxj9PX1bZeRkWGRlpZmkZCQYGH6////FR4enqvi4uJXFBUVrwIAjS0Jhoe78boAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAjBBs3EW2wAAAChJREFUeJxjzM/PbxcTE2MBYRERERamf//+XeHi4roqJCR0RUpK6ioAlIYJkon2KxsAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAoBBV7S4YwAAACtJREFUeJxjXL58eTsvLy8LHx8fCw8PDwvT////r7CxsV3l5ua+IiAgcBUAoS4Js9Gzqw8AAAAASUVORK5CYII="};
    static final String[] WHITE={"iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJklEQVR4nGNcun3ffyFmTgYhJk4GYWZuBsadNy/9B3GEmLgYQBIAnUcHNDyrTu4AAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJklEQVR4nGPcdvTUf34mDgY+JnYGfmZOBsZjD24DBdiBAhwMIBoAolIHWh74f1AAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJElEQVR4nGO8dPv+f05GFgZOJlYGTkZWBsY7b15CBBhBAiwMAKprB5Y3wxU9AAAAAElFTkSuQmCC",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAHUlEQVR4nGNcuHDhfx4eHgZeXl4GEM24f/9+FAEAlo4G/1VxBk0AAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAIklEQVR4nGPcvHnzf05OTgZubm4GLi4uBsbTp0//BzFgGACc0wcvy8tnLAAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAHUlEQVR4nGO8cOHCfzY2NgZ2dnYGEM14//59FAEAp0QHgJP9t1YAAAAASUVORK5CYII="},DARK={"iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAI0lEQVR4nGMUVNb6L8rGwyDKCsK8DEwf/39k+PgPhD8xgNgAnIENWPGzxGIAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJUlEQVR4nGO08w36L8jCxSDIysUgAKSZvvz7wvDlPxAD6a9ADACrCw3XxDsA9QAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAIUlEQVR4nGPsmDT1PzcTGwMXMxsDNxAz/fz/k+EHEMNoAMNpDq76ARFRAAAAAElFTkSuQmCC",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAIklEQVR4nGPU1NT8LyAgwCAoKMgAopk+ffrE8PnzZwYYDQChsg1vTBZHnQAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAI0lEQVR4nGMMDAz8z8vLy8DHx8cAopm+fv3K8O3bNwYQDcIAr2UN7ZfRHSMAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJklEQVR4nGOcOnXqf3Z2dgZOTk4GDg4OBqafP38y/Pr1iwFEgzAAxgkOuaoU1IEAAAAASUVORK5CYII="};
    static void require(boolean ok,String message)throws IOException{if(!ok)throw new IOException("Legacy dual: "+message);}
    static void same(Bitmap actual,Bitmap expected)throws IOException{require(actual.getWidth()==expected.getWidth()&&actual.getHeight()==expected.getHeight(),"dimensions");for(int y=0;y<actual.getHeight();y++)for(int x=0;x<actual.getWidth();x++){int a=actual.getPixel(x,y),b=expected.getPixel(x,y);for(int shift:new int[]{0,8,16,24})require(Math.abs(((a>>>shift)&255)-((b>>>shift)&255))<=1,"appearance pixels");}}
    static File fixture(File dir,String name,String base64)throws IOException{File f=new File(dir,name);try(OutputStream out=new FileOutputStream(f)){out.write(Base64.decode(base64,Base64.DEFAULT));}return f;}
    static void run(File dir,Files.Job job)throws Exception{
        for(int i=0;i<SAMPLES.length;i++){
            File input=fixture(dir,"legacy-"+i+".png",SAMPLES[i]);int dark=new int[]{0,48,128}[i%3];require(DualBlend.isLegacy(input)&&DualBlend.background(input)==dark,"old profile");Bitmap source=Images.load(input,0,true);
            try{for(boolean white:new boolean[]{true,false}){File expected=fixture(dir,"expected-"+i+"-"+white+".png",white?WHITE[i]:DARK[i]);Bitmap target=Images.load(expected,0,true),view=DualBlend.extract(source,white,job,dark);try{same(view,target);}finally{target.recycle();view.recycle();}}
                List<File> first=DualBlend.restore(input,dir,0,job),second=DualBlend.restore(input,dir,0,job);require(first.size()==2&&second.size()==2&&!first.get(0).equals(second.get(0)),"restore collisions");
                byte[] bad=Base64.decode(SAMPLES[i],Base64.DEFAULT);bad[43]^=1;try(OutputStream out=new FileOutputStream(input)){out.write(bad);}boolean rejected=false;try{DualBlend.isLegacy(input);}catch(IOException e){rejected=true;}require(rejected,"bad profile CRC");
            }finally{source.recycle();}
        }
        File flat=fixture(dir,"legacy-flat.png",WHITE[0]);Bitmap opaque=Images.load(flat,0,true);try{boolean rejected=false;try{Bitmap view=DualBlend.extract(opaque,false,job,0);view.recycle();}catch(IOException e){rejected=true;}require(rejected,"opaque file");Files.Job cancelled=new Files.Job();cancelled.cancelled=true;boolean stopped=false;try{Bitmap view=DualBlend.extract(opaque,false,cancelled,0);view.recycle();}catch(InterruptedIOException e){stopped=true;}require(stopped,"cancellation");}finally{opaque.recycle();}
        byte[] old=Base64.decode(SAMPLES[0],Base64.DEFAULT);ByteArrayOutputStream raw=new ByteArrayOutputStream();raw.write(old,0,33);raw.write(old,49,old.length-49);File unprofiled=new File(dir,"legacy-no-profile.png");try(OutputStream out=new FileOutputStream(unprofiled)){out.write(raw.toByteArray());}require(DualBlend.isLegacy(unprofiled)&&DualBlend.background(unprofiled)==0,"unprofiled PNG");
        File cover=fixture(dir,"legacy-cover.png",WHITE[0]),hidden=fixture(dir,"legacy-hidden.png",DARK[0]),dynamic=new File(dir,"legacy-dynamic.png");Bitmap a=Images.load(cover,0,true),b=Images.load(hidden,0,true);try{Images.dual(a,b,dynamic,job);require(!DualBlend.isLegacy(dynamic),"dynamic misidentified");File restored=Files.dualRestore(dynamic,dir,job);Bitmap actual=Images.load(restored,0,true);try{same(actual,b);}finally{actual.recycle();}}finally{a.recycle();b.recycle();}
    }
}
