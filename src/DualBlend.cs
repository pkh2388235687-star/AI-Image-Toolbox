using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace QQImageSwitch
{
    // Compatibility reader only; new transparent dual-image generation has been removed.
    public static class DualBlend
    {
        static int Byte(double n){return (int)Math.Max(0,Math.Min(255,Math.Floor(n+0.5)));}
        // Read-only compatibility for transparent dual PNGs created by older releases.
        public static bool IsLegacy(byte[] bytes)
        {
            var chunks=Codec.Parse(bytes);foreach(var chunk in chunks)if(chunk.Type=="acTL")return false;
            foreach(var chunk in chunks)if(chunk.Type=="duAL"){Background(bytes);return true;}
            int type=chunks[0].Data[9];if(type==4||type==6)return true;
            foreach(var chunk in chunks)if(chunk.Type=="tRNS")return true;return false;
        }
        public static Bitmap DecodeView(byte[] bytes,bool hidden,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!IsLegacy(bytes))return Codec.Decode(hidden?Codec.Restore(bytes):bytes);
            using(var source=Codec.Decode(bytes))return Extract(source,!hidden,token,Background(bytes));
        }
        public static Bitmap Render(Bitmap input,bool white,CancellationToken token,int dark=0)
        {return Transform(input,white,false,token,dark);}
        public static Bitmap Extract(Bitmap input,bool white,CancellationToken token,int dark=0)
        {return Transform(input,white,true,token,dark);}
        static Bitmap Transform(Bitmap input,bool white,bool requireAlpha,CancellationToken token,int dark)
        {
            if(dark<0||dark>128)throw new ArgumentOutOfRangeException("dark");
            Codec.CheckSize(input.Width,input.Height);token.ThrowIfCancellationRequested();
            var result=new Bitmap(input.Width,input.Height,PixelFormat.Format32bppArgb);
            BitmapData a=null,o=null;bool transparent=false,success=false;
            try
            {
                var rect=new Rectangle(0,0,input.Width,input.Height);
                a=input.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                o=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                int[] row=new int[input.Width];
                for(int y=0;y<input.Height;y++)
                {
                    token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(a.Scan0,y*a.Stride),row,0,row.Length);
                    for(int x=0;x<row.Length;x++)
                    {
                        int v=row[x],alpha=(int)((uint)v>>24);double bg=(white?255:dark)*(255-alpha)/255.0;transparent|=alpha<255;
                        row[x]=unchecked((int)0xff000000)|Byte(((v>>16)&255)*alpha/255.0+bg)<<16|Byte(((v>>8)&255)*alpha/255.0+bg)<<8|Byte((v&255)*alpha/255.0+bg);
                    }
                    Marshal.Copy(row,0,IntPtr.Add(o.Scan0,y*o.Stride),row.Length);
                }
                if(requireAlpha&&!transparent)throw new InvalidDataException("图片没有透明度，无法提取旧双图；请使用保留透明度的 PNG 原图。");
                success=true;return result;
            }
            finally{if(a!=null)input.UnlockBits(a);if(o!=null)result.UnlockBits(o);if(!success)result.Dispose();}
        }
        public static int Background(byte[] bytes){using(var s=new MemoryStream(bytes,false))return Background(s);}
        public static int Background(string path){using(var s=File.OpenRead(path))return Background(s);}
        static int Background(Stream s)
        {
            byte[] signature={137,80,78,71,13,10,26,10};
            for(int i=0;i<8;i++)if(s.ReadByte()!=signature[i])throw new InvalidDataException("请选择 PNG 原图。");
            using(var r=new BinaryReader(s))while(s.Position<s.Length)
            {
                byte[] head=r.ReadBytes(8);if(head.Length!=8)throw new InvalidDataException("PNG 数据块不完整。");
                long length=((long)head[0]<<24)|((long)head[1]<<16)|((long)head[2]<<8)|head[3];
                if(length>s.Length-s.Position-4)throw new InvalidDataException("PNG 数据块不完整。");
                string type=System.Text.Encoding.ASCII.GetString(head,4,4);
                if(type=="duAL")
                {
                    if(length!=4)throw new InvalidDataException("双图参数无效。");
                    byte[] data=r.ReadBytes(4),check=r.ReadBytes(4);uint crc=0xffffffff;
                    for(int k=4;k<12;k++){crc^=k<8?head[k]:data[k-8];for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
                    uint stored=(uint)check[0]<<24|(uint)check[1]<<16|(uint)check[2]<<8|check[3];
                    if((crc^0xffffffff)!=stored||data[0]!=1||data[1]<1||data[1]>2||data[2]>128||data[3]>100)throw new InvalidDataException("双图参数校验失败。");
                    return data[2];
                }
                if(type=="IDAT"||type=="IEND")return 0; // Older files were generated for pure black.
                s.Seek(length+4,SeekOrigin.Current);
            }
            throw new InvalidDataException("PNG 数据不完整。");
        }
        public static void RestorePair(string input,string folder,CancellationToken token)
        {
            string first=null;bool complete=false;
            try
            {
                int dark=Background(input);
                using(var source=Codec.Load(input))
                {
                    using(var white=Extract(source,true,token)){byte[] bytes=Codec.StaticPng(white);token.ThrowIfCancellationRequested();first=ImageTools.Unique(folder,input,"_白底显示",".png");Codec.SaveAtomic(first,bytes);}
                    using(var black=Extract(source,false,token,dark)){byte[] bytes=Codec.StaticPng(black);token.ThrowIfCancellationRequested();Codec.SaveAtomic(ImageTools.Unique(folder,input,"_黑底显示",".png"),bytes);}
                }
                complete=true;
            }
            finally{if(!complete&&first!=null&&File.Exists(first))File.Delete(first);}
        }
    }
}
