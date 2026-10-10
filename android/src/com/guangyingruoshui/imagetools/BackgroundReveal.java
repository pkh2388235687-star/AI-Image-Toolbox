package com.guangyingruoshui.imagetools;

import android.graphics.*;
import java.io.*;

// Independent source-over solution for white and black appearances.
final class BackgroundReveal {
    static int channel(double n){return (int)Math.max(0,Math.min(255,Math.floor(n+.5)));}
    static int flatten(int v,int shift,int background){int a=v>>>24;return (((v>>>shift)&255)*a+background*(255-a)+127)/255;}
    static double luma(int r,int g,int b){return (299*r+587*g+114*b)/1000.0;}
    static double alpha(double wr,double wg,double wb,double br,double bg,double bb){
        double sr=wr+br-255,sg=wg+bg-255,sb=wb+bb-255;
        double t0=Math.abs(sr),t1=Math.abs(sg),t2=Math.abs(sb),t;
        if(t0>t1){t=t0;t0=t1;t1=t;}if(t1>t2){t=t1;t1=t2;t2=t;}if(t0>t1){t=t0;t0=t1;t1=t;}
        double offset=(sr>=0?-2*br:2*(wr-255))+(sg>=0?-2*bg:2*(wg-255))+(sb>=0?-2*bb:2*(wb-255));
        double a=-offset/6;if(a<=t0)return a;offset+=t0;a=-offset/5;if(a<=t1)return a;offset+=t1;a=-offset/4;if(a<=t2)return a;offset+=t2;return -offset/3;
    }
    static int component(double white,double black,int alpha){double p=Math.max(0,Math.min(alpha,(white+black-255+alpha)/2));return alpha==0?0:channel(p*255/alpha);}
    static int pixel(int light,int dark,boolean color,int strength){
        return pixel(light,dark,color,strength,false);
    }
    static int pixel(int light,int dark,boolean color,int strength,boolean originalPriority){
        if(strength<0||strength>100)throw new IllegalArgumentException("Invalid color strength");
        int lr=flatten(light,16,255),lg=flatten(light,8,255),lb=flatten(light,0,255),dr=flatten(dark,16,0),dg=flatten(dark,8,0),db=flatten(dark,0,0);
        double ly=luma(lr,lg,lb),dy=luma(dr,dg,db),s=color?strength/100.0:0;
        double wr=127.5+(ly+(lr-ly)*s)*.5,wg=127.5+(ly+(lg-ly)*s)*.5,wb=127.5+(ly+(lb-ly)*s)*.5,br=(dy+(dr-dy)*s)*.5,bg=(dy+(dg-dy)*s)*.5,bb=(dy+(db-dy)*s)*.5;
        if(color&&originalPriority){
            int opacity=Math.max((int)Math.ceil(Math.max(br,Math.max(bg,bb))),channel(255+(br+bg+bb)/3-(127.5+ly*.5)));
            return opacity<<24|(opacity==0?0:channel(br*255/opacity))<<16|(opacity==0?0:channel(bg*255/opacity))<<8|(opacity==0?0:channel(bb*255/opacity));
        }
        int a=channel(alpha(wr,wg,wb,br,bg,bb));return a<<24|component(wr,br,a)<<16|component(wg,bg,a)<<8|component(wb,bb,a);
    }
    static Bitmap automaticCover(int w,int h){
        Bitmap b=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);Canvas c=new Canvas(b);Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(0,0,w,h,0xff6648e0,0xffee80b2,Shader.TileMode.CLAMP));c.drawRect(0,0,w,h,p);p.setShader(null);
        float unit=Math.min(w,h);p.setColor(0x1affffff);c.drawOval(new RectF(-unit*.2f,-unit*.2f,unit*.7f,unit*.7f),p);c.drawOval(new RectF(w-unit*.4f,h-unit*.4f,w+unit*.4f,h+unit*.4f),p);return b;
    }
    static Bitmap compose(Bitmap light,Bitmap dark,boolean color,int strength,Files.Job job)throws Exception{
        return compose(light,dark,color,strength,job,false);
    }
    static Bitmap compose(Bitmap light,Bitmap dark,boolean color,int strength,Files.Job job,boolean originalPriority)throws Exception{
        job.check();int w=dark.getWidth(),h=dark.getHeight();long budget=Math.min(192L*1024*1024,Runtime.getRuntime().maxMemory()*2/3);
        if((long)w*h>budget/20)throw new IOException("手机内存不足，请降低导出尺寸 / Reduce output size for this device");
        Bitmap white=null,black=null,result=null;boolean success=false;
        try{
            white=light==null?automaticCover(w,h):Images.fit(light,w,h,Color.WHITE);black=Images.fit(dark,w,h,Color.BLACK);result=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);result.setHasAlpha(true);
            int[] a=new int[w],b=new int[w],row=new int[w];
            for(int y=0;y<h;y++){job.check();white.getPixels(a,0,w,0,y,w,1);black.getPixels(b,0,w,0,y,w,1);for(int x=0;x<w;x++)row[x]=pixel(a[x],b[x],color,strength,originalPriority);result.setPixels(row,0,w,0,y,w,1);}
            success=true;return result;
        }finally{if(white!=null)white.recycle();if(black!=null)black.recycle();if(!success&&result!=null)result.recycle();}
    }
    static void encode(Bitmap image,File output,boolean color,int strength,Files.Job job)throws Exception{
        File tmp=Files.temp(output.getParentFile());try{
            Images.encode(image,tmp,0,job);
            try(InputStream in=new FileInputStream(tmp);OutputStream out=new FileOutputStream(output)){
                out.write(Files.read(in,8));byte[] head=Files.read(in,8);if(Files.be(head,0,4)!=13||!new String(head,4,4,Files.LATIN).equals("IHDR"))throw new IOException("Invalid PNG header");out.write(head);out.write(Files.read(in,17));
                Files.chunk(out,"duAL",new byte[]{1,(byte)(color?1:2),0,(byte)strength});Files.copy(in,out,job);
            }
        }finally{tmp.delete();}
    }
}
