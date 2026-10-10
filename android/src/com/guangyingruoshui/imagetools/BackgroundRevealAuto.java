package com.guangyingruoshui.imagetools;
import android.graphics.*;
import java.io.*;

// Adaptive global chroma budget and source-independent cover geometry.
final class BackgroundRevealAuto {
    static final class Profile {int whiteLow,whiteHigh,blackLow,blackHigh,whiteFloor=190,blackCeiling=165;double strength,effectiveStrength,exposure=.55,chromaLimit=20;boolean pattern,theme,sourceColor;int tintR,tintG,tintB;}
    static final class Result {Bitmap image;Profile profile;Result(Bitmap b,Profile p){image=b;profile=p;}}
    static int channel(double v){return (int)Math.max(0,Math.min(255,Math.floor(v+.5)));}
    static double y(int v){return (((v>>>16)&255)*299+((v>>>8)&255)*587+(v&255)*114)/1000.0;}
    static double tone(double v,int lo,int hi,double start,double end){if(hi-lo<16){lo=0;hi=255;}return start+(end-start)*Math.max(0,Math.min(1,(v-lo)/(hi-lo)));}
    static int quantile(long[] histogram,double f){long total=0,sum=0;for(long n:histogram)total+=n;long target=Math.max(1,(long)Math.ceil(total*f));for(int i=0;i<256;i++){sum+=histogram[i];if(sum>=target)return i;}return 255;}
    static void histogram(Bitmap b,long[] h,long[] rgb,Files.Job job)throws Exception{int[] row=new int[b.getWidth()];for(int y=0;y<b.getHeight();y++){job.check();b.getPixels(row,0,row.length,0,y,row.length,1);for(int v:row){h[channel(y(v))]++;rgb[0]+=(v>>>16)&255;rgb[1]+=(v>>>8)&255;rgb[2]+=v&255;if(rgb.length>3)rgb[3]+=Math.max((v>>>16)&255,Math.max((v>>>8)&255,v&255))-Math.min((v>>>16)&255,Math.min((v>>>8)&255,v&255));}}}
    static Bitmap pattern(int w,int h,long[] sums,long count,int contrast,Files.Job job,int palette)throws Exception{return pattern(w,h,sums,count,contrast,job,palette,false);}
    static Bitmap pattern(int w,int h,long[] sums,long count,int contrast,Files.Job job,int palette,boolean ornamental)throws Exception{return pattern(w,h,sums,count,contrast,job,palette,ornamental,0);}
    static Bitmap pattern(int w,int h,long[] sums,long count,int contrast,Files.Job job,int palette,boolean ornamental,double activity)throws Exception{
        job.check();double r=sums[0]/(double)count,g=sums[1]/(double)count,b=sums[2]/(double)count;if(palette==1){r=104;g=80;b=208;}else if(palette==2){r=224;g=120;b=48;}else if(palette==3){r=40;g=168;b=144;}if(palette<0){r=g=b=128;}double l=(r*299+g*587+b*114)/1000;if(palette==0&&Math.max(Math.abs(r-l),Math.max(Math.abs(g-l),Math.abs(b-l)))<3){r=104;g=80;b=208;l=(r*299+g*587+b*114)/1000;}
        int first=Color.rgb(channel(104+(r-l)*.5),channel(104+(g-l)*.5),channel(104+(b-l)*.5)),last=Color.rgb(channel(176+(r-l)*.5),channel(176+(g-l)*.5),channel(176+(b-l)*.5));
        Bitmap image=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);boolean success=false;
        try{
            Canvas canvas=new Canvas(image);Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);paint.setShader(new LinearGradient(0,0,w*.7f,h,first,last,Shader.TileMode.CLAMP));canvas.drawRect(0,0,w,h,paint);paint.setShader(null);
            Path path=new Path();float lift=contrast>120?.06f:0;paint.setColor(0x1effffff);path.moveTo(-.15f*w,(.65f+lift)*h);path.cubicTo(.35f*w,.38f*h,.32f*w,.15f*h,1.2f*w,-.06f*h);path.lineTo(1.2f*w,-.25f*h);path.lineTo(-.15f*w,-.25f*h);path.close();canvas.drawPath(path,paint);
            job.check();path.reset();paint.setColor(0x12ffffff);path.moveTo(-.15f*w,.9f*h);path.cubicTo(.3f*w,.6f*h,.67f*w,1.03f*h,1.15f*w,.4f*h);path.lineTo(1.15f*w,1.15f*h);path.lineTo(-.15f*w,1.15f*h);path.close();canvas.drawPath(path,paint);
            path.reset();paint.setColor(0x30ffffff);paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(Math.max(1,Math.min(w,h)/700f));path.moveTo(-.15f*w,.92f*h);path.cubicTo(.3f*w,.62f*h,.67f*w,1.05f*h,1.15f*w,.42f*h);canvas.drawPath(path,paint);
            if(ornamental){
                paint.setStyle(Paint.Style.FILL);int bands=4+(int)Math.round(activity*5);
                for(int i=0;i<bands;i++){job.check();float yy=(i-.7f)/bands*h,thick=h*(.042f+.025f*(float)activity),bend=(i%2==0?1:-1)*h*.13f;path.reset();path.moveTo(-.12f*w,yy);path.cubicTo(.3f*w,yy+bend,.64f*w,yy-bend,1.12f*w,yy+h*.12f);path.lineTo(1.12f*w,yy+h*.12f+thick);path.cubicTo(.64f*w,yy-bend+thick,.3f*w,yy+bend+thick,-.12f*w,yy+thick);path.close();paint.setStyle(Paint.Style.FILL);paint.setColor(Color.argb(32+(int)Math.round(activity*24),255,255,255));canvas.drawPath(path,paint);paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(Math.max(1,Math.min(w,h)/600f));paint.setColor(0x1effffff);canvas.drawPath(path,paint);}
                paint.setStyle(Paint.Style.FILL);paint.setColor(0x46ffffff);path.reset();path.moveTo(.68f*w,.28f*h);path.cubicTo(.39f*w,.17f*h,.48f*w,-.12f*h,.96f*w,.02f*h);path.cubicTo(.99f*w,.24f*h,.83f*w,.37f*h,.68f*w,.28f*h);path.close();canvas.drawPath(path,paint);
                path.reset();path.moveTo(.28f*w,.73f*h);path.cubicTo(-.08f*w,.78f*h,-.07f*w,.47f*h,.21f*w,.44f*h);path.cubicTo(.45f*w,.48f*h,.51f*w,.65f*h,.28f*w,.73f*h);path.close();canvas.drawPath(path,paint);
                job.check();paint.setColor(0x2023283c);path.reset();path.moveTo(-.1f*w,.3f*h);path.cubicTo(.38f*w,.28f*h,.62f*w,.58f*h,1.1f*w,.6f*h);path.cubicTo(.63f*w,.74f*h,.3f*w,.39f*h,-.1f*w,.41f*h);path.close();canvas.drawPath(path,paint);
            }
success=true;return image;
        }finally{if(!success)image.recycle();}
    }
    static Bitmap createCover(Bitmap dark,int palette,Files.Job job)throws Exception{long[] h=new long[256],rgb=new long[3];histogram(dark,h,rgb,job);return pattern(dark.getWidth(),dark.getHeight(),rgb,(long)dark.getWidth()*dark.getHeight(),quantile(h,.98)-quantile(h,.02),job,palette);}
    static double chromaBudget(int contrast,int strength){return strength==0?0:Math.max(16,Math.min(48,16+strength*.32-Math.max(0,contrast-96)/48.0));}
    static double gamut(double tone,double chroma){return chroma>0?(255-tone)/chroma:chroma<0?-tone/chroma:1;}
    static int component(double w,double b,int a){return a==0?0:channel(Math.max(0,Math.min(a,(w+b-255+a)*.5))*255/a);}
    static int pixel(int white,int black,Profile p){return pixel(white,black,p,black);}
    static int pixel(int white,int black,Profile p,int colorBase){
        if(p.sourceColor){double cy=y(colorBase),cr=((colorBase>>>16)&255)-cy,cg=((colorBase>>>8)&255)-cy,cb=(colorBase&255)-cy,tone=y(black)*p.exposure,k=p.strength*p.exposure;
            double range=Math.max(cr,Math.max(cg,cb))-Math.min(cr,Math.min(cg,cb));if(range>0)k=Math.min(k,p.chromaLimit/range);
            k=Math.max(0,Math.min(k,Math.min(gamut(tone,cr),Math.min(gamut(tone,cg),gamut(tone,cb)))));double br=tone+cr*k,bg=tone+cg*k,bb=tone+cb*k,wt=tone(y(white),p.whiteLow,p.whiteHigh,185,236);int a=Math.max((int)Math.ceil(Math.max(br,Math.max(bg,bb))),channel(255+(br+bg+bb)/3-wt));
            return a<<24|(a==0?0:channel(br*255/a))<<16|(a==0?0:channel(bg*255/a))<<8|(a==0?0:channel(bb*255/a));}

        double wy=y(white),by=y(black),wt=tone(wy,p.whiteLow,p.whiteHigh,p.theme?184:p.whiteFloor,p.theme?218:250),bt=tone(by,p.blackLow,p.blackHigh,p.theme?40:4,p.blackCeiling),k=p.strength*.5;
        double wr=wt+(((white>>>16)&255)-wy)*k,wg=wt+(((white>>>8)&255)-wy)*k,wb=wt+((white&255)-wy)*k,br=bt+(((black>>>16)&255)-by)*k,bg=bt+(((black>>>8)&255)-by)*k,bb=bt+((black&255)-by)*k;
        if(p.theme){wr=wt+p.tintR;wg=wt+p.tintG;wb=wt+p.tintB;br=bt+p.tintR;bg=bt+p.tintG;bb=bt+p.tintB;}
        double low=Math.min(wr-br,Math.min(wg-bg,wb-bb)),high=Math.max(wr-br,Math.max(wg-bg,wb-bb));int a=channel(255-(low+high)*.5);return a<<24|component(wr,br,a)<<16|component(wg,bg,a)<<8|component(wb,bb,a);
    }
    static Result compose(Bitmap light,Bitmap dark,boolean color,int maximumStrength,Files.Job job)throws Exception{return compose(light,dark,color,maximumStrength,job,0);}
    static Result compose(Bitmap light,Bitmap dark,boolean color,int maximumStrength,Files.Job job,int palette)throws Exception{return compose(light,dark,color,maximumStrength,job,palette,false);}
    static Result compose(Bitmap light,Bitmap dark,boolean color,int maximumStrength,Files.Job job,int palette,boolean sourceColor)throws Exception{return compose(light,dark,color,maximumStrength,job,palette,sourceColor,-1);}
    static Result compose(Bitmap light,Bitmap dark,boolean color,int maximumStrength,Files.Job job,int palette,boolean sourceColor,int manualStrength)throws Exception{
        if(maximumStrength<0||maximumStrength>100||manualStrength< -1||manualStrength>100)throw new IllegalArgumentException("Invalid maximum strength");job.check();int width=dark.getWidth(),height=dark.getHeight();long budget=Math.min(192L*1024*1024,Runtime.getRuntime().maxMemory()*2/3);if((long)width*height>budget/28)throw new IOException("手机内存不足，请降低尺寸 / Reduce output size");
        Bitmap black=null,white=null,result=null,small=null,colors=null;boolean success=false;
        try{
            black=Images.fit(dark,width,height,Color.BLACK);long[] bh=new long[256],rgb=new long[4];histogram(black,bh,rgb,job);white=light==null?pattern(width,height,rgb,(long)width*height,quantile(bh,.98)-quantile(bh,.02),job,color?palette:-1,sourceColor&&color,Math.min(1,rgb[3]/((double)width*height)*.55*(manualStrength>=0?manualStrength:maximumStrength)/4800)):Images.fit(light,width,height,Color.WHITE);long[] wh=new long[256],coverRgb=new long[3];histogram(white,wh,coverRgb,job);
            Profile p=new Profile();p.whiteLow=quantile(wh,.02);p.whiteHigh=quantile(wh,.98);p.blackLow=quantile(bh,.01);p.blackHigh=quantile(bh,.99);p.whiteFloor=Math.max(184,Math.min(196,190+(quantile(bh,.5)-128)/24));p.blackCeiling=quantile(bh,.98)-quantile(bh,.02)<30?145:165;p.pattern=light==null;p.sourceColor=sourceColor&&color;p.exposure=Math.max(.45,Math.min(.65,.56+(128-quantile(bh,.5))/1024.0));p.strength=color?maximumStrength/100.0:0;p.chromaLimit=chromaBudget(quantile(bh,.98)-quantile(bh,.02),maximumStrength);
            if(p.sourceColor&&manualStrength>=0){p.strength=manualStrength/100.0;p.chromaLimit=manualStrength==0?0:255;}
            if(p.sourceColor){int edge=Math.max(width,height);small=Images.fit(black,Math.max(1,width*24/edge),Math.max(1,height*24/edge),Color.BLACK);colors=Images.fit(small,width,height,Color.BLACK);}
            int[] w=new int[width],b=new int[width],row=new int[width],c=p.sourceColor?new int[width]:null;double limit=p.strength;
            if(!p.sourceColor)for(int y=0;y<height;y++){job.check();white.getPixels(w,0,width,0,y,width,1);black.getPixels(b,0,width,0,y,width,1);for(int x=0;x<width;x++){int dr=((w[x]>>>16)&255)-((b[x]>>>16)&255),dg=((w[x]>>>8)&255)-((b[x]>>>8)&255),db=(w[x]&255)-(b[x]&255),range=Math.max(dr,Math.max(dg,db))-Math.min(dr,Math.min(dg,db));if(range>0)limit=Math.min(limit,4.0/range);double wy=y(w[x]),by=y(b[x]),wt=tone(wy,p.whiteLow,p.whiteHigh,p.theme?184:p.whiteFloor,p.theme?218:250),bt=tone(by,p.blackLow,p.blackHigh,p.theme?40:4,p.blackCeiling);for(int shift=16;shift>=0;shift-=8){limit=Math.min(limit,gamut(wt,(((w[x]>>>shift)&255)-wy)*.5));limit=Math.min(limit,gamut(bt,(((b[x]>>>shift)&255)-by)*.5));}}}
            p.strength=p.sourceColor?Math.max(0,Math.min(p.strength,limit)):limit<.05?0:Math.max(0,Math.min(p.strength,limit));if(color&&!p.sourceColor&&(light==null||limit<.05)){p.theme=true;p.strength=0;double count=(long)width*height,rr=coverRgb[0]/count,gg=coverRgb[1]/count,bb=coverRgb[2]/count,yy=(rr*299+gg*587+bb*114)/1000,range=Math.max(Math.abs(rr-yy),Math.max(Math.abs(gg-yy),Math.abs(bb-yy)));if(range<3){p.tintR=-14;p.tintG=-3;p.tintB=17;}else{p.tintR=(int)Math.rint((rr-yy)*32/range);p.tintG=(int)Math.rint((gg-yy)*32/range);p.tintB=(int)Math.rint((bb-yy)*32/range);}}result=Bitmap.createBitmap(width,height,Bitmap.Config.ARGB_8888);result.setHasAlpha(true);
            double sourceChroma=0,keptChroma=0;for(int y=0;y<height;y++){job.check();white.getPixels(w,0,width,0,y,width,1);black.getPixels(b,0,width,0,y,width,1);if(colors!=null)colors.getPixels(c,0,width,0,y,width,1);for(int x=0;x<width;x++){row[x]=pixel(w[x],b[x],p,colors==null?b[x]:c[x]);if(p.sourceColor){int rr=(c[x]>>>16)&255,gg=(c[x]>>>8)&255,bb=c[x]&255;sourceChroma+=(Math.max(rr,Math.max(gg,bb))-Math.min(rr,Math.min(gg,bb)))*p.exposure;int v=row[x],a=v>>>24;keptChroma+=(Math.max((v>>>16)&255,Math.max((v>>>8)&255,v&255))-Math.min((v>>>16)&255,Math.min((v>>>8)&255,v&255)))*a/255.0;}}result.setPixels(row,0,width,0,y,width,1);}p.effectiveStrength=p.sourceColor&&sourceChroma>0?Math.min(p.strength,keptChroma/sourceChroma):p.strength;success=true;return new Result(result,p);
        }finally{if(colors!=null)colors.recycle();if(small!=null)small.recycle();if(black!=null)black.recycle();if(white!=null)white.recycle();if(!success&&result!=null)result.recycle();}
    }
    static void encode(Bitmap image,File output,boolean color,Profile p,Files.Job job)throws Exception{
        File tmp=Files.temp(output.getParentFile());try{BackgroundReveal.encode(image,tmp,color,(int)Math.rint(p.strength*100),job);try(InputStream in=new FileInputStream(tmp);OutputStream out=new FileOutputStream(output)){out.write(Files.read(in,33));Files.chunk(out,"rvAO",new byte[]{1,(byte)(p.sourceColor?3:p.theme?2:1),(byte)(color?1:0),(byte)Math.rint(p.strength*100),(byte)(p.tintR+128),(byte)(p.tintG+128),(byte)(p.tintB+128)});Files.copy(in,out,job);}}finally{tmp.delete();}
    }
}
