using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace QQImageSwitch
{
    // Independently solves source-over compositing on white and black backgrounds.
    // The two RGB appearances share one alpha; arbitrary colors are approximated.
    public static class BackgroundReveal
    {
        static int Byte(double n){return (int)Math.Max(0,Math.Min(255,Math.Floor(n+.5)));}
        static int Flatten(int value,int shift,int background)
        {int a=(int)((uint)value>>24);return (((value>>shift)&255)*a+background*(255-a)+127)/255;}
        static double Luma(int r,int g,int b){return (299*r+587*g+114*b)/1000.0;}
        static void Swap(ref double a,ref double b){if(a>b){double t=a;a=b;b=t;}}
        // Exact minimum of the convex, piecewise quadratic RGB error. At most
        // three breakpoints, with no allocation or iterative search per pixel.
        static double Alpha(double wr,double wg,double wb,double br,double bg,double bb)
        {
            double sr=wr+br-255,sg=wg+bg-255,sb=wb+bb-255;
            double t0=Math.Abs(sr),t1=Math.Abs(sg),t2=Math.Abs(sb);
            Swap(ref t0,ref t1);Swap(ref t1,ref t2);Swap(ref t0,ref t1);
            double offset=(sr>=0?-2*br:2*(wr-255))+(sg>=0?-2*bg:2*(wg-255))+(sb>=0?-2*bb:2*(wb-255));
            double a=-offset/6;if(a<=t0)return a;offset+=t0;
            a=-offset/5;if(a<=t1)return a;offset+=t1;
            a=-offset/4;if(a<=t2)return a;offset+=t2;
            return -offset/3;
        }
        static int Channel(double white,double black,int alpha)
        {double premultiplied=Math.Max(0,Math.Min(alpha,(white+black-255+alpha)/2));return alpha==0?0:Byte(premultiplied*255/alpha);}
        public static int Pixel(int light,int dark,bool color,int strength,bool originalPriority=false)
        {
            if(strength<0||strength>100)throw new ArgumentOutOfRangeException("strength");
            int lr=Flatten(light,16,255),lg=Flatten(light,8,255),lb=Flatten(light,0,255);
            int dr=Flatten(dark,16,0),dg=Flatten(dark,8,0),db=Flatten(dark,0,0);
            double ly=Luma(lr,lg,lb),dy=Luma(dr,dg,db);
            double s=color?strength/100.0:0;
            double wr=127.5+(ly+(lr-ly)*s)*.5,wg=127.5+(ly+(lg-ly)*s)*.5,wb=127.5+(ly+(lb-ly)*s)*.5;
            double br=(dy+(dr-dy)*s)*.5,bg=(dy+(dg-dy)*s)*.5,bb=(dy+(db-dy)*s)*.5;
            if(color&&originalPriority)
            {
                // Keep the black appearance independent of the cover. A shared
                // alpha still means that the white appearance is a color compromise.
                int opacity=Math.Max((int)Math.Ceiling(Math.Max(br,Math.Max(bg,bb))),Byte(255+(br+bg+bb)/3-(127.5+ly*.5)));
                return opacity<<24|(opacity==0?0:Byte(br*255/opacity))<<16|(opacity==0?0:Byte(bg*255/opacity))<<8|(opacity==0?0:Byte(bb*255/opacity));
            }
            int a=Byte(Alpha(wr,wg,wb,br,bg,bb));
            return a<<24|Channel(wr,br,a)<<16|Channel(wg,bg,a)<<8|Channel(wb,bb,a);
        }
        public static Bitmap Compose(Image light,Image dark,int bound,bool color,int strength,CancellationToken token,bool originalPriority=false)
        {
            if(bound<0||strength<0||strength>100)throw new ArgumentOutOfRangeException();
            token.ThrowIfCancellationRequested();Size size=Codec.OutputSize(dark,bound);Codec.CheckSize(size.Width,size.Height);
            using(var w=light==null?Codec.DefaultCover(size.Width,size.Height,false):Codec.Fit(light,size.Width,size.Height,Color.White))
            using(var b=Codec.Fit(dark,size.Width,size.Height,Color.Black))
            {
                var result=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppArgb);
                BitmapData wd=null,bd=null,rd=null;bool success=false;
                try
                {
                    var rect=new Rectangle(Point.Empty,size);wd=w.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);bd=b.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);rd=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                    int[] white=new int[size.Width],black=new int[size.Width],row=new int[size.Width];
                    for(int y=0;y<size.Height;y++)
                    {
                        token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(wd.Scan0,y*wd.Stride),white,0,row.Length);Marshal.Copy(IntPtr.Add(bd.Scan0,y*bd.Stride),black,0,row.Length);
                        for(int x=0;x<row.Length;x++)row[x]=Pixel(white[x],black[x],color,strength,originalPriority);
                        Marshal.Copy(row,0,IntPtr.Add(rd.Scan0,y*rd.Stride),row.Length);
                    }
                    success=true;return result;
                }
                finally{if(wd!=null)w.UnlockBits(wd);if(bd!=null)b.UnlockBits(bd);if(rd!=null)result.UnlockBits(rd);if(!success)result.Dispose();}
            }
        }
        public static byte[] Encode(Bitmap image,bool color,int strength,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();byte[] png=Codec.StaticPng(image);token.ThrowIfCancellationRequested();
            using(var output=new MemoryStream())
            {
                output.Write(png,0,8);foreach(var chunk in Codec.Parse(png)){token.ThrowIfCancellationRequested();Codec.WriteChunk(output,chunk.Type,chunk.Data);if(chunk.Type=="IHDR")Codec.WriteChunk(output,"duAL",new byte[]{1,(byte)(color?1:2),0,(byte)strength});}
                return output.ToArray();
            }
        }
    }
}
