using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace QQImageSwitch
{
    // Adaptive tone mapping and a bounded global chroma budget. No source
    // contours are used to draw the automatically generated cover pattern.
    public static class BackgroundRevealAuto
    {
        public sealed class Profile
        {
            public int WhiteLow,WhiteHigh,BlackLow,BlackHigh,WhiteFloor=190,BlackCeiling=165;
            public double Strength,EffectiveStrength,Exposure=.55,ChromaLimit=20;
            public bool Pattern,Theme,SourceColor;
            public int TintR,TintG,TintB;
        }
        static int Byte(double v){return (int)Math.Max(0,Math.Min(255,Math.Floor(v+.5)));}
        static double Y(int v){return (((v>>16)&255)*299+((v>>8)&255)*587+(v&255)*114)/1000.0;}
        static double Tone(double y,int lo,int hi,double start,double end){if(hi-lo<16){lo=0;hi=255;}return start+(end-start)*Math.Max(0,Math.Min(1,(y-lo)/(hi-lo)));}
        static int Quantile(long[] histogram,double fraction){long total=0,sum=0;foreach(long n in histogram)total+=n;long target=Math.Max(1,(long)Math.Ceiling(total*fraction));for(int i=0;i<256;i++){sum+=histogram[i];if(sum>=target)return i;}return 255;}
        static void Histogram(Bitmap image,long[] histogram,long[] rgb,CancellationToken token)
        {
            BitmapData data=null;try{data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);int[] row=new int[image.Width];for(int y=0;y<image.Height;y++){token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);foreach(int v in row){histogram[Byte(Y(v))]++;rgb[0]+=(v>>16)&255;rgb[1]+=(v>>8)&255;rgb[2]+=v&255;if(rgb.Length>3)rgb[3]+=Math.Max((v>>16)&255,Math.Max((v>>8)&255,v&255))-Math.Min((v>>16)&255,Math.Min((v>>8)&255,v&255));}}}finally{if(data!=null)image.UnlockBits(data);}
        }
        static Bitmap Pattern(int w,int h,long[] sums,long count,int contrast,CancellationToken token,int palette,bool ornamental=false,double activity=0)
        {
            token.ThrowIfCancellationRequested();double r=sums[0]/(double)count,g=sums[1]/(double)count,b=sums[2]/(double)count; if(palette==1){r=104;g=80;b=208;}else if(palette==2){r=224;g=120;b=48;}else if(palette==3){r=40;g=168;b=144;} if(palette<0){r=g=b=128;} double l=(r*299+g*587+b*114)/1000; if(palette==0&&Math.Max(Math.Abs(r-l),Math.Max(Math.Abs(g-l),Math.Abs(b-l)))<3){r=104;g=80;b=208;l=(r*299+g*587+b*114)/1000;}
            Color first=Color.FromArgb(Byte(104+(r-l)*.5),Byte(104+(g-l)*.5),Byte(104+(b-l)*.5));
            Color last=Color.FromArgb(Byte(176+(r-l)*.5),Byte(176+(g-l)*.5),Byte(176+(b-l)*.5));
            var image=new Bitmap(w,h,PixelFormat.Format32bppArgb);
            try
            {
                using(var graphics=Graphics.FromImage(image))using(var gradient=new LinearGradientBrush(new Rectangle(0,0,w,h),first,last,55f))
                using(var soft=new SolidBrush(Color.FromArgb(30,255,255,255)))using(var glow=new SolidBrush(Color.FromArgb(18,255,255,255)))
                using(var edge=new Pen(Color.FromArgb(48,255,255,255),Math.Max(1,Math.Min(w,h)/700f)))using(var path=new GraphicsPath())
                {
                    graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.FillRectangle(gradient,0,0,w,h);float lift=contrast>120?.06f:0;
                    path.AddBezier(-.15f*w,(.65f+lift)*h,.35f*w,.38f*h,.32f*w,.15f*h,1.2f*w,-.06f*h);
                    path.AddLine(1.2f*w,-.06f*h,1.2f*w,-.25f*h);path.AddLine(1.2f*w,-.25f*h,-.15f*w,-.25f*h);path.CloseFigure();graphics.FillPath(soft,path);path.Reset();
                    token.ThrowIfCancellationRequested();path.AddBezier(-.15f*w,.9f*h,.3f*w,.6f*h,.67f*w,1.03f*h,1.15f*w,.4f*h);
                    path.AddLine(1.15f*w,.4f*h,1.15f*w,1.15f*h);path.AddLine(1.15f*w,1.15f*h,-.15f*w,1.15f*h);path.CloseFigure();graphics.FillPath(glow,path);path.Reset();
                    path.AddBezier(-.15f*w,.92f*h,.3f*w,.62f*h,.67f*w,1.05f*h,1.15f*w,.42f*h);graphics.DrawPath(edge,path);
                    if(ornamental)
                    {
                        // Independent large petals and ribbons mask soft color fields,
                        // rather than copying any outline from the source image.
                        using(var petal=new SolidBrush(Color.FromArgb(70,255,255,255)))using(var shade=new SolidBrush(Color.FromArgb(32,35,40,60)))
                        {
                            // Add source-independent woven ribbons across the whole cover.
                            // Density follows global chroma, never local source contours.
                            int bands=4+(int)Math.Round(activity*5);
                            using(var ribbon=new SolidBrush(Color.FromArgb(32+(int)Math.Round(activity*24),255,255,255)))
                            using(var seam=new Pen(Color.FromArgb(30,255,255,255),Math.Max(1,Math.Min(w,h)/600f)))
                            for(int i=0;i<bands;i++){
                                token.ThrowIfCancellationRequested();float yy=(i-.7f)/bands*h,thick=h*(.042f+.025f*(float)activity),bend=(i%2==0?1:-1)*h*.13f;
                                path.Reset();path.AddBezier(-.12f*w,yy,.3f*w,yy+bend,.64f*w,yy-bend,1.12f*w,yy+h*.12f);
                                path.AddBezier(1.12f*w,yy+h*.12f+thick,.64f*w,yy-bend+thick,.3f*w,yy+bend+thick,-.12f*w,yy+thick);path.CloseFigure();graphics.FillPath(ribbon,path);graphics.DrawPath(seam,path);
                            }
                            path.Reset();path.AddBezier(.68f*w,.28f*h,.39f*w,.17f*h,.48f*w,-.12f*h,.96f*w,.02f*h);path.AddBezier(.96f*w,.02f*h,.99f*w,.24f*h,.83f*w,.37f*h,.68f*w,.28f*h);path.CloseFigure();graphics.FillPath(petal,path);
                            path.Reset();path.AddBezier(.28f*w,.73f*h,-.08f*w,.78f*h,-.07f*w,.47f*h,.21f*w,.44f*h);path.AddBezier(.21f*w,.44f*h,.45f*w,.48f*h,.51f*w,.65f*h,.28f*w,.73f*h);path.CloseFigure();graphics.FillPath(petal,path);
                            token.ThrowIfCancellationRequested();path.Reset();path.AddBezier(-.1f*w,.3f*h,.38f*w,.28f*h,.62f*w,.58f*h,1.1f*w,.6f*h);path.AddBezier(1.1f*w,.6f*h,.63f*w,.74f*h,.3f*w,.39f*h,-.1f*w,.41f*h);path.CloseFigure();graphics.FillPath(shade,path);
                        }
                    }

                }
                return image;
            }
            catch{image.Dispose();throw;}
        }
        public static Bitmap CreateCover(Image dark,int bound,int palette,CancellationToken token){var size=Codec.OutputSize(dark,bound);Codec.CheckSize(size.Width,size.Height);using(var fitted=Codec.Fit(dark,size.Width,size.Height,Color.Black)){var h=new long[256];var rgb=new long[3];Histogram(fitted,h,rgb,token);return Pattern(size.Width,size.Height,rgb,(long)size.Width*size.Height,Quantile(h,.98)-Quantile(h,.02),token,palette);}}
        static double ChromaBudget(int contrast,int strength){return strength==0?0:Math.Max(16,Math.Min(48,16+strength*.32-Math.Max(0,contrast-96)/48.0));}
        static double Gamut(double tone,double chroma){return chroma>0?(255-tone)/chroma:chroma<0?-tone/chroma:1;}
        public static int Pixel(int white,int black,Profile p){return Pixel(white,black,p,black);}
        public static int Pixel(int white,int black,Profile p,int colorBase)
        {
            if(p.SourceColor)
            {
                double cy=Y(colorBase),cr=((colorBase>>16)&255)-cy,cg=((colorBase>>8)&255)-cy,cb=(colorBase&255)-cy;
                double tone=Y(black)*p.Exposure,sourcek=p.Strength*p.Exposure;
                double range=Math.Max(cr,Math.Max(cg,cb))-Math.Min(cr,Math.Min(cg,cb));if(range>0)sourcek=Math.Min(sourcek,p.ChromaLimit/range);
                sourcek=Math.Max(0,Math.Min(sourcek,Math.Min(Gamut(tone,cr),Math.Min(Gamut(tone,cg),Gamut(tone,cb)))));
                double sourcebr=tone+cr*sourcek,sourcebg=tone+cg*sourcek,sourcebb=tone+cb*sourcek,sourcewt=Tone(Y(white),p.WhiteLow,p.WhiteHigh,185,236);
                int sourcea=Math.Max((int)Math.Ceiling(Math.Max(sourcebr,Math.Max(sourcebg,sourcebb))),Byte(255+(sourcebr+sourcebg+sourcebb)/3-sourcewt));
                return sourcea<<24|(sourcea==0?0:Byte(sourcebr*255/sourcea))<<16|(sourcea==0?0:Byte(sourcebg*255/sourcea))<<8|(sourcea==0?0:Byte(sourcebb*255/sourcea));
            }
            double wy=Y(white),by=Y(black),wt=Tone(wy,p.WhiteLow,p.WhiteHigh,p.Theme?184:p.WhiteFloor,p.Theme?218:250),bt=Tone(by,p.BlackLow,p.BlackHigh,p.Theme?40:4,p.BlackCeiling),k=p.Strength*.5;
            double wr=wt+(((white>>16)&255)-wy)*k,wg=wt+(((white>>8)&255)-wy)*k,wb=wt+((white&255)-wy)*k;
            double br=bt+(((black>>16)&255)-by)*k,bg=bt+(((black>>8)&255)-by)*k,bb=bt+((black&255)-by)*k;
            if(p.Theme){wr=wt+p.TintR;wg=wt+p.TintG;wb=wt+p.TintB;br=bt+p.TintR;bg=bt+p.TintG;bb=bt+p.TintB;}
            double low=Math.Min(wr-br,Math.Min(wg-bg,wb-bb)),high=Math.Max(wr-br,Math.Max(wg-bg,wb-bb));int a=Byte(255-(low+high)*.5);
            int r=Channel(wr,br,a),g=Channel(wg,bg,a),b=Channel(wb,bb,a);return a<<24|r<<16|g<<8|b;
        }
        public static byte[] Encode(Bitmap image,bool color,Profile p,CancellationToken token)
        {
            var bytes=BackgroundReveal.Encode(image,color,(int)Math.Round(p.Strength*100),token);using(var output=new System.IO.MemoryStream()){output.Write(bytes,0,8);foreach(var chunk in Codec.Parse(bytes)){token.ThrowIfCancellationRequested();Codec.WriteChunk(output,chunk.Type,chunk.Data);if(chunk.Type=="duAL")Codec.WriteChunk(output,"rvAO",new byte[]{1,(byte)(p.SourceColor?3:p.Theme?2:1),(byte)(color?1:0),(byte)Math.Round(p.Strength*100),(byte)(p.TintR+128),(byte)(p.TintG+128),(byte)(p.TintB+128)});}return output.ToArray();}
        }
        static int Channel(double w,double b,int a){return a==0?0:Byte(Math.Max(0,Math.Min(a,(w+b-255+a)*.5))*255/a);}
        public static Bitmap Compose(Image light,Image dark,int bound,bool color,int maximumStrength,CancellationToken token,out Profile profile,int palette=0,bool sourceColor=false,double manualStrength=-1)
        {
            if(maximumStrength<0||maximumStrength>100||bound<0||manualStrength< -1||manualStrength>100)throw new ArgumentOutOfRangeException();token.ThrowIfCancellationRequested();var size=Codec.OutputSize(dark,bound);Codec.CheckSize(size.Width,size.Height);
            using(var black=Codec.Fit(dark,size.Width,size.Height,Color.Black))
            {
                var bh=new long[256];var rgb=new long[4];Histogram(black,bh,rgb,token);
                using(var white=light==null?Pattern(size.Width,size.Height,rgb,(long)size.Width*size.Height,Quantile(bh,.98)-Quantile(bh,.02),token,color?palette:-1,sourceColor&&color,Math.Min(1,rgb[3]/((double)size.Width*size.Height)*.55*(manualStrength>=0?manualStrength:maximumStrength)/4800)):Codec.Fit(light,size.Width,size.Height,Color.White))
                {
                    var wh=new long[256];var coverRgb=new long[3];Histogram(white,wh,coverRgb,token);var p=new Profile{WhiteLow=Quantile(wh,.02),WhiteHigh=Quantile(wh,.98),BlackLow=Quantile(bh,.01),BlackHigh=Quantile(bh,.99),WhiteFloor=Math.Max(184,Math.Min(196,190+(Quantile(bh,.5)-128)/24)),BlackCeiling=Quantile(bh,.98)-Quantile(bh,.02)<30?145:165,Pattern=light==null,SourceColor=sourceColor&&color,Exposure=Math.Max(.45,Math.Min(.65,.56+(128-Quantile(bh,.5))/1024.0)),Strength=color?maximumStrength/100.0:0,ChromaLimit=ChromaBudget(Quantile(bh,.98)-Quantile(bh,.02),maximumStrength)};
                    if(p.SourceColor&&manualStrength>=0){p.Strength=manualStrength/100.0;p.ChromaLimit=manualStrength==0?0:255;}
                    using(var small=p.SourceColor?Codec.Fit(black,Math.Max(1,size.Width*24/Math.Max(size.Width,size.Height)),Math.Max(1,size.Height*24/Math.Max(size.Width,size.Height)),Color.Black):null)
                    using(var colors=small==null?null:Codec.Fit(small,size.Width,size.Height,Color.Black))
                    {
                    BitmapData wd=null,bd=null,rd=null,cd=null;Bitmap result=null;bool success=false;
                    try
                    {
                        var rect=new Rectangle(Point.Empty,size);wd=white.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);bd=black.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);if(colors!=null)cd=colors.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);int[] w=new int[size.Width],b=new int[size.Width],row=new int[size.Width],c=cd==null?null:new int[size.Width];double limit=p.Strength;
                        // One scalar for the whole image avoids cover-shaped saturation masks.
                        if(!p.SourceColor)for(int y=0;y<size.Height;y++){token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(wd.Scan0,y*wd.Stride),w,0,w.Length);Marshal.Copy(IntPtr.Add(bd.Scan0,y*bd.Stride),b,0,b.Length);for(int x=0;x<w.Length;x++){int dr=((w[x]>>16)&255)-((b[x]>>16)&255),dg=((w[x]>>8)&255)-((b[x]>>8)&255),db=(w[x]&255)-(b[x]&255);int range=Math.Max(dr,Math.Max(dg,db))-Math.Min(dr,Math.Min(dg,db));if(range>0)limit=Math.Min(limit,4.0/range);double wy=Y(w[x]),by=Y(b[x]),wt=Tone(wy,p.WhiteLow,p.WhiteHigh,p.Theme?184:p.WhiteFloor,p.Theme?218:250),bt=Tone(by,p.BlackLow,p.BlackHigh,p.Theme?40:4,p.BlackCeiling);for(int shift=16;shift>=0;shift-=8){limit=Math.Min(limit,Gamut(wt,(((w[x]>>shift)&255)-wy)*.5));limit=Math.Min(limit,Gamut(bt,(((b[x]>>shift)&255)-by)*.5));}}}
                        // Very small chroma gives a colored ghost without useful color.
                        p.Strength=p.SourceColor?Math.Max(0,Math.Min(p.Strength,limit)):limit<.05?0:Math.Max(0,Math.Min(p.Strength,limit));if(color&&!p.SourceColor&&(light==null||limit<.05)){p.Theme=true;p.Strength=0;double count=(long)size.Width*size.Height,rr=coverRgb[0]/count,gg=coverRgb[1]/count,bb=coverRgb[2]/count,yy=(rr*299+gg*587+bb*114)/1000,range=Math.Max(Math.Abs(rr-yy),Math.Max(Math.Abs(gg-yy),Math.Abs(bb-yy)));if(range<3){p.TintR=-14;p.TintG=-3;p.TintB=17;}else{p.TintR=(int)Math.Round((rr-yy)*32/range);p.TintG=(int)Math.Round((gg-yy)*32/range);p.TintB=(int)Math.Round((bb-yy)*32/range);}}
                        result=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppArgb);rd=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                        double sourceChroma=0,keptChroma=0;for(int y=0;y<size.Height;y++){token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(wd.Scan0,y*wd.Stride),w,0,w.Length);Marshal.Copy(IntPtr.Add(bd.Scan0,y*bd.Stride),b,0,b.Length);if(cd!=null)Marshal.Copy(IntPtr.Add(cd.Scan0,y*cd.Stride),c,0,c.Length);for(int x=0;x<row.Length;x++){row[x]=Pixel(w[x],b[x],p,cd==null?b[x]:c[x]);if(p.SourceColor){int rr=(c[x]>>16)&255,gg=(c[x]>>8)&255,bb=c[x]&255;sourceChroma+=(Math.Max(rr,Math.Max(gg,bb))-Math.Min(rr,Math.Min(gg,bb)))*p.Exposure;int v=row[x],a=(int)((uint)v>>24);keptChroma+=(Math.Max((v>>16)&255,Math.Max((v>>8)&255,v&255))-Math.Min((v>>16)&255,Math.Min((v>>8)&255,v&255)))*a/255.0;}}Marshal.Copy(row,0,IntPtr.Add(rd.Scan0,y*rd.Stride),row.Length);}p.EffectiveStrength=p.SourceColor&&sourceChroma>0?Math.Min(p.Strength,keptChroma/sourceChroma):p.Strength;
                        success=true;profile=p;return result;
                    }
                    finally{if(cd!=null)colors.UnlockBits(cd);if(wd!=null)white.UnlockBits(wd);if(bd!=null)black.UnlockBits(bd);if(rd!=null)result.UnlockBits(rd);if(!success&&result!=null)result.Dispose();}
                    }
                }
            }
        }
    }
}
