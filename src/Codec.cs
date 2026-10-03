using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace QQImageSwitch
{
    // Independent implementation of the public PNG/APNG container format.
    public static class Codec
    {
        static readonly byte[] Signature = {137,80,78,71,13,10,26,10};
        static readonly uint[] CrcTable = MakeCrcTable();
        public const long MaxPixels = 32000000;
        public const int MaxSide = 8192;
        public class Chunk { public string Type; public byte[] Data; }

        public static void CheckSize(int w, int h)
        {
            if (w < 1 || h < 1 || w > MaxSide || h > MaxSide || (long)w*h > MaxPixels)
                throw new InvalidDataException("图片过大：单边最多 8192 像素，总像素最多 3200 万。请先缩小图片。");
        }
        public static Bitmap Load(string path)
        {
            if (new FileInfo(path).Length > 160L*1024*1024) throw new InvalidDataException("输入文件超过 160 MB。");
            using (var img = Image.FromFile(path))
            {
                CheckSize(img.Width, img.Height);
                try
                {
                    if (Array.IndexOf(img.PropertyIdList, 0x112) >= 0)
                    {
                        int n = img.GetPropertyItem(0x112).Value[0];
                        RotateFlipType[] ops = {RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                            RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,
                            RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};
                        if (n > 0 && n < ops.Length) img.RotateFlip(ops[n]);
                    }
                }
                catch (ArgumentException) { }
                return ((Bitmap)img).Clone(new Rectangle(0,0,img.Width,img.Height),PixelFormat.Format32bppArgb);
            }
        }
        public static Size OutputSize(Image real, int maxSide)
        {
            double ratio = maxSide > 0 ? Math.Min(1.0, (double)maxSide/Math.Max(real.Width,real.Height)) : 1.0;
            return new Size(Math.Max(1,(int)Math.Round(real.Width*ratio)),Math.Max(1,(int)Math.Round(real.Height*ratio)));
        }
        public static Bitmap Fit(Image img, int w, int h, Color background)
        {
            CheckSize(w,h);
            if(img is Bitmap && img.Width==w && img.Height==h && background.A==0)
                return ((Bitmap)img).Clone(new Rectangle(0,0,w,h),PixelFormat.Format32bppArgb);
            var b = new Bitmap(w,h,PixelFormat.Format32bppArgb);
            using(var g = Graphics.FromImage(b))
            {
                g.Clear(background);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                double r = Math.Min((double)w/img.Width,(double)h/img.Height);
                int iw=Math.Max(1,(int)Math.Round(img.Width*r)), ih=Math.Max(1,(int)Math.Round(img.Height*r));
                using(var attrs = new ImageAttributes())
                {
                    attrs.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(img,new Rectangle((w-iw)/2,(h-ih)/2,iw,ih),0,0,img.Width,img.Height,GraphicsUnit.Pixel,attrs);
                }
            }
            return b;
        }
        public static Bitmap DefaultCover(int w,int h,bool caption=true)
        {
            CheckSize(w,h);
            var b = new Bitmap(w,h,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(b))
            using(var brush=new LinearGradientBrush(new Rectangle(0,0,w,h),Color.FromArgb(102,72,224),Color.FromArgb(238,128,178),45f))
            {
                g.FillRectangle(brush,0,0,w,h);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                float unit=Math.Min(w,h);
                using(var translucent=new SolidBrush(Color.FromArgb(26,255,255,255)))
                {
                    g.FillEllipse(translucent,-unit*.2f,-unit*.2f,unit*.9f,unit*.9f);
                    g.FillEllipse(translucent,w-unit*.4f,h-unit*.4f,unit*.8f,unit*.8f);
                }
                if(caption)using(var title=new Font("Microsoft YaHei UI",Math.Max(1,unit/17),FontStyle.Bold,GraphicsUnit.Pixel))
                using(var small=new Font("Microsoft YaHei UI",Math.Max(1,unit/34),FontStyle.Regular,GraphicsUnit.Pixel))
                using(var sf=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                {
                    g.DrawString(L.T("点击查看原图"),title,Brushes.White,new RectangleF(0,h*.38f,w,h*.16f),sf);
                    g.DrawString(L.T("另一张图片，藏在这里"),small,Brushes.White,new RectangleF(0,h*.54f,w,h*.09f),sf);
                }
            }
            return b;
        }
        public static byte[] StaticPng(Bitmap bitmap)
        {
            using(var s=new MemoryStream())
            {
                s.Write(Signature,0,8);
                WriteChunk(s,"IHDR",Header(bitmap.Width,bitmap.Height));
                WritePieces(s,"IDAT",CompressPixels(bitmap),false,0);
                WriteChunk(s,"IEND",new byte[0]);
                return s.ToArray();
            }
        }
        public static byte[] Encode(Bitmap cover,Bitmap real)
        {
            if(cover.Size!=real.Size) throw new ArgumentException("封面和原图的画布尺寸必须相同。");
            using(var s=new MemoryStream())
            {
                s.Write(Signature,0,8);
                WriteChunk(s,"IHDR",Header(real.Width,real.Height));
                WriteChunk(s,"acTL",Join(U32(2),U32(0)));
                // This marker also makes files recognizable by the supplied Android app.
                WriteChunk(s,"tEXt",Encoding.ASCII.GetBytes("ChatBarApngDisguise\0"+"1;STATIC;1"));
                WritePieces(s,"IDAT",CompressPixels(cover),false,0);
                uint seq=0;
                WriteChunk(s,"fcTL",FrameControl(seq++,real.Width,real.Height,0));
                seq=WritePieces(s,"fdAT",CompressPixels(real),true,seq);
                WriteChunk(s,"fcTL",FrameControl(seq++,1,1,1));
                // Transparent OVER dummy frame leaves the full hidden image unchanged.
                WritePieces(s,"fdAT",Zlib(new byte[5]),true,seq);
                WriteChunk(s,"IEND",new byte[0]);
                return s.ToArray();
            }
        }
        static byte[] FrameControl(uint seq,int w,int h,byte blend)
        { return Join(U32(seq),U32((uint)w),U32((uint)h),U32(0),U32(0),new byte[]{0,10,0,100,0,blend}); }
        static byte[] Header(int w,int h)
        { return Join(U32((uint)w),U32((uint)h),new byte[]{8,6,0,0,0}); }
        static uint WritePieces(Stream s,string type,byte[] z,bool sequence,uint seq)
        {
            for(int p=0;p<z.Length;p+=65536)
            {
                int n=Math.Min(65536,z.Length-p);
                byte[] part=new byte[n]; Buffer.BlockCopy(z,p,part,0,n);
                WriteChunk(s,type,sequence?Join(U32(seq++),part):part);
            }
            return seq;
        }
        static byte[] CompressPixels(Bitmap b)
        {
            CheckSize(b.Width,b.Height);
            int row=checked(b.Width*4);
            byte[] raw=new byte[checked(row*b.Height)];
            var bd=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try
            {
                byte[] bgra=new byte[row];
                for(int y=0;y<b.Height;y++)
                {
                    Marshal.Copy(IntPtr.Add(bd.Scan0,y*bd.Stride),bgra,0,row);
                    for(int x=0;x<row;x+=4)
                    {
                        int p=y*row+x; raw[p]=bgra[x+2];raw[p+1]=bgra[x+1];raw[p+2]=bgra[x];raw[p+3]=bgra[x+3];
                    }
                }
            }
            finally { b.UnlockBits(bd); }
            byte[] filtered=new byte[checked((row+1)*b.Height)];
            byte[] current=new byte[row], best=new byte[row];
            for(int y=0;y<b.Height;y++)
            {
                long bestScore=long.MaxValue; byte kind=0;
                for(byte mode=0;mode<=2;mode++)
                {
                    long score=0;
                    for(int x=0;x<row;x++)
                    {
                        int predictor=mode==1?(x>=4?raw[y*row+x-4]:0):mode==2?(y>0?raw[(y-1)*row+x]:0):0;
                        byte val=unchecked((byte)(raw[y*row+x]-predictor));
                        current[x]=val;score+=Math.Abs((int)unchecked((sbyte)val));
                    }
                    if(score<bestScore){bestScore=score;kind=mode;Buffer.BlockCopy(current,0,best,0,row);}
                }
                int p=y*(row+1); filtered[p]=kind;Buffer.BlockCopy(best,0,filtered,p+1,row);
            }
            return Zlib(filtered);
        }
        static byte[] Zlib(byte[] raw)
        {
            using(var s=new MemoryStream())
            {
                s.WriteByte(0x78);s.WriteByte(0x9c);
                using(var d=new DeflateStream(s,CompressionLevel.Optimal,true)) d.Write(raw,0,raw.Length);
                uint a=1,b=0;
                for(int p=0;p<raw.Length;p++){a=(a+raw[p])%65521;b=(b+a)%65521;}
                byte[] adler=U32((b<<16)|a);s.Write(adler,0,4);
                return s.ToArray();
            }
        }
        public static List<Chunk> Parse(byte[] bytes,bool enforceSize=true)
        {
            if(bytes.Length<33) throw new InvalidDataException("不是有效的 PNG 文件。");
            for(int i=0;i<8;i++) if(bytes[i]!=Signature[i]) throw new InvalidDataException("请选择原始 PNG 文件。");
            var chunks=new List<Chunk>();int p=8;bool end=false;
            while(p<=bytes.Length-12)
            {
                uint n=Read32(bytes,p);
                if(n>160*1024*1024 || (long)p+12+n>bytes.Length) throw new InvalidDataException("PNG 数据块损坏或不完整。");
                string type=Encoding.ASCII.GetString(bytes,p+4,4);
                uint crc=Crc(bytes,p+4,checked((int)n+4));
                if(crc!=Read32(bytes,p+8+(int)n)) throw new InvalidDataException("PNG 校验失败："+type);
                byte[] data=new byte[n];Buffer.BlockCopy(bytes,p+8,data,0,(int)n);
                chunks.Add(new Chunk {Type=type,Data=data});p+=12+(int)n;
                if(type=="IEND"){end=true;break;}
            }
            if(!end||chunks.Count==0||chunks[0].Type!="IHDR"||chunks[0].Data.Length!=13) throw new InvalidDataException("PNG 缺少头部或结束块。");
            int width=checked((int)Read32(chunks[0].Data,0)),height=checked((int)Read32(chunks[0].Data,4));
            if(width<1||height<1)throw new InvalidDataException("PNG 图片尺寸无效。");
            if(enforceSize)CheckSize(width,height);
            return chunks;
        }
        public static byte[] Restore(byte[] png)
        {
            var chunks=Parse(png); byte[] header=chunks[0].Data;byte[] control=null;
            var data=new List<byte[]>(); bool defaultIsFrame=false, idatSeen=false, animated=false;
            foreach(var c in chunks)
            {
                if(c.Type=="acTL") animated=true;
                if(c.Type=="IDAT") { idatSeen=true; if(control!=null) defaultIsFrame=true; }
                if(c.Type=="fcTL")
                {
                    if(data.Count>0) break;
                    if(c.Data.Length!=26) throw new InvalidDataException("APNG 帧控制块损坏。");
                    control=c.Data;
                    if(!idatSeen) defaultIsFrame=true;
                }
                if(c.Type=="fdAT"&&control!=null)
                {
                    if(c.Data.Length<5) throw new InvalidDataException("APNG 帧数据损坏。");
                    byte[] part=new byte[c.Data.Length-4];Buffer.BlockCopy(c.Data,4,part,0,part.Length);data.Add(part);
                }
            }
            if(!animated||control==null||data.Count==0||defaultIsFrame)
                throw new InvalidDataException("没有找到独立于封面的隐藏图。请选择双图 PNG 原文件；QQ 压缩图无法还原。");
            if(Read32(control,4)!=Read32(header,0)||Read32(control,8)!=Read32(header,4)||Read32(control,12)!=0||Read32(control,16)!=0)
                throw new InvalidDataException("暂不支持局部动画帧；此工具及所提供 APK 的全画布隐藏图均可还原。");
            using(var s=new MemoryStream())
            {
                s.Write(Signature,0,8);WriteChunk(s,"IHDR",header);
                foreach(var c in chunks) if(c.Type=="PLTE"||c.Type=="tRNS") WriteChunk(s,c.Type,c.Data);
                foreach(var part in data) WriteChunk(s,"IDAT",part);
                WriteChunk(s,"IEND",new byte[0]);return s.ToArray();
            }
        }
        public static Bitmap Decode(byte[] png)
        {
            using(var s=new MemoryStream(png))
            using(var img=(Bitmap)Image.FromStream(s))
                return img.Clone(new Rectangle(0,0,img.Width,img.Height),PixelFormat.Format32bppArgb);
        }
        public static void SaveAtomic(string path,byte[] bytes)
        {
            path=Path.GetFullPath(path);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllBytes(temp,bytes);if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);}
            finally {if(File.Exists(temp)) File.Delete(temp);}
        }
        public static uint Read32(byte[] a,int p)
        { return ((uint)a[p]<<24)|((uint)a[p+1]<<16)|((uint)a[p+2]<<8)|a[p+3]; }
        static byte[] U32(uint v){return new byte[]{(byte)(v>>24),(byte)(v>>16),(byte)(v>>8),(byte)v};}
        static byte[] Join(params byte[][] arrays)
        {
            int n=0;foreach(var a in arrays)n=checked(n+a.Length);byte[] b=new byte[n];int p=0;
            foreach(var a in arrays){Buffer.BlockCopy(a,0,b,p,a.Length);p+=a.Length;}return b;
        }
        internal static void WriteChunk(Stream s,string type,byte[] data)
        {
            byte[] length=U32((uint)data.Length),content=Join(Encoding.ASCII.GetBytes(type),data),crc=U32(Crc(content,0,content.Length));
            s.Write(length,0,4);s.Write(content,0,content.Length);s.Write(crc,0,4);
        }
        static uint Crc(byte[] data,int offset,int n)
        { uint c=0xffffffff;for(int i=offset;i<offset+n;i++)c=CrcTable[(c^data[i])&255]^(c>>8);return c^0xffffffff; }
        static uint[] MakeCrcTable()
        {var t=new uint[256];for(uint n=0;n<256;n++){uint c=n;for(int k=0;k<8;k++)c=(c&1)!=0?0xedb88320^(c>>1):c>>1;t[n]=c;}return t;}
    }
}
