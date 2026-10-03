using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace QQImageSwitch
{
    public static class ImageTools
    {
        public static string Unique(string folder,string input,string suffix,string extension)
        {
            Directory.CreateDirectory(folder);
            string stem=Path.GetFileNameWithoutExtension(input);
            foreach(char c in Path.GetInvalidFileNameChars())stem=stem.Replace(c,'_');
            stem=stem.Trim().TrimEnd('.');if(stem.Length>70)stem=stem.Substring(0,70);if(stem.Length==0)stem="图片";
            string path=Path.Combine(folder,stem+suffix+extension);int n=2;
            while(File.Exists(path)||Directory.Exists(path))path=Path.Combine(folder,stem+suffix+"_"+(n++)+extension);
            return path;
        }
        public static byte[] Read(string path)
        {
            if(new FileInfo(path).Length>160L*1024*1024)throw new InvalidDataException("输入文件超过 160 MB。");
            return File.ReadAllBytes(path);
        }
        public static string Clean(string input,string folder)
        {
            byte[] bytes=Read(input),clean;string extension;
            if(bytes.Length>8&&bytes[0]==137&&Encoding.ASCII.GetString(bytes,1,3)=="PNG")
            {
                Codec.Parse(bytes,false);
                var keep=new HashSet<string>{"IHDR","PLTE","IDAT","IEND","tRNS","cHRM","gAMA","sRGB","iCCP","sBIT","cICP","mDCV","cLLI","bKGD","pHYs","acTL","fcTL","fdAT"};
                using(var output=new MemoryStream())
                {
                    output.Write(bytes,0,8);int p=8;
                    while(p<bytes.Length)
                    {
                        int length=checked((int)Codec.Read32(bytes,p));string type=Encoding.ASCII.GetString(bytes,p+4,4);
                        if(keep.Contains(type))output.Write(bytes,p,length+12);
                        if(type=="eXIf")
                        {
                            byte[] data=new byte[length];Buffer.BlockCopy(bytes,p+8,data,0,length);int orientation=Orientation(data,0);
                            if(orientation>1)Codec.WriteChunk(output,"eXIf",OrientationExif(orientation));
                        }
                        p+=length+12;if(type=="IEND")break;
                    }
                    clean=output.ToArray();
                }
                extension=".png";
            }
            else if(bytes.Length>13&&Encoding.ASCII.GetString(bytes,0,3)=="GIF")
            {clean=GifCodec.Clean(bytes);extension=".gif";}
            else if(bytes.Length>12&&Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&Encoding.ASCII.GetString(bytes,8,4)=="WEBP")
            {clean=CleanWebP(bytes);extension=".webp";}
            else if(bytes.Length>4&&bytes[0]==255&&bytes[1]==216)
            {
                clean=CleanJpeg(bytes);extension=".jpg";
            }
            else
            {throw new InvalidDataException("无损清信息支持 PNG / APNG、JPEG、WebP 和 GIF。其他格式请先转换。");}
            VerifyClean(clean);
            string path=Unique(folder,input,"_已清信息",extension);Codec.SaveAtomic(path,clean);return path;
        }
        static int Orientation(byte[] bytes,int offset)
        {
            if(offset+8>bytes.Length)return 1;bool little=bytes[offset]==73&&bytes[offset+1]==73;
            if(!little&&!(bytes[offset]==77&&bytes[offset+1]==77))return 1;
            Func<int,int> shortAt=p=>little?bytes[p]|bytes[p+1]<<8:bytes[p]<<8|bytes[p+1];
            Func<int,uint> longAt=p=>little?BitConverter.ToUInt32(bytes,p):Codec.Read32(bytes,p);
            uint relative=longAt(offset+4);if(relative>bytes.Length-offset-2)return 1;int directory=offset+(int)relative,count=shortAt(directory);
            for(int i=0;i<count;i++)
            {
                int p=directory+2+i*12;if(p+12>bytes.Length)return 1;
                if(shortAt(p)==0x112&&shortAt(p+2)==3&&longAt(p+4)==1){int n=shortAt(p+8);return n>=1&&n<=8?n:1;}
            }
            return 1;
        }
        static byte[] OrientationExif(int orientation)
        {return new byte[]{73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,(byte)orientation,0,0,0,0,0,0,0};}
        static void VerifyClean(byte[] bytes)
        {
            if(bytes[0]==137)
            {
                foreach(var chunk in Codec.Parse(bytes,false))
                {
                    if(chunk.Type=="tEXt"||chunk.Type=="iTXt"||chunk.Type=="zTXt"||chunk.Type=="comf")throw new InvalidDataException("工作流清理校验失败。");
                    if(chunk.Type=="eXIf"&&!chunk.Data.SequenceEqual(OrientationExif(Orientation(chunk.Data,0))))throw new InvalidDataException("EXIF 清理校验失败。");
                }
            }
        }
        static byte[] CleanWebP(byte[] bytes)
        {
            uint size=BitConverter.ToUInt32(bytes,4);long end=8L+size;
            if(end>bytes.Length||end<12)throw new InvalidDataException("WebP 数据不完整。");
            using(var output=new MemoryStream())
            {
                output.Write(bytes,0,12);int p=12;bool oriented=false;
                var keep=new HashSet<string>{"VP8X","VP8 ","VP8L","ALPH","ANIM","ANMF","ICCP"};
                while(p<end)
                {
                    if(p+8>end)throw new InvalidDataException("WebP 数据块不完整。");
                    string type=Encoding.ASCII.GetString(bytes,p,4);uint length=BitConverter.ToUInt32(bytes,p+4);long total=8L+length+(length&1);
                    if(p+total>end)throw new InvalidDataException("WebP 数据块不完整。");
                    if(keep.Contains(type))
                    {
                        if(type=="VP8X")
                        {if(length!=10)throw new InvalidDataException("WebP 扩展头无效。");byte[] chunk=new byte[total];Buffer.BlockCopy(bytes,p,chunk,0,chunk.Length);chunk[8]&=0xf3;output.Write(chunk,0,chunk.Length);}
                        else output.Write(bytes,p,(int)total);
                    }
                    else if(type=="EXIF")
                    {
                        byte[] data=new byte[length];Buffer.BlockCopy(bytes,p+8,data,0,(int)length);
                        int offset=data.Length>=6&&Encoding.ASCII.GetString(data,0,6)=="Exif\0\0"?6:0;
                        int orientation=Orientation(data,offset);
                        if(orientation>1)
                        {
                            oriented=true;byte[] minimal=OrientationExif(orientation),tag=Encoding.ASCII.GetBytes("EXIF");
                            output.Write(tag,0,4);byte[] n=BitConverter.GetBytes((uint)minimal.Length);output.Write(n,0,4);output.Write(minimal,0,minimal.Length);
                        }
                    }
                    p+=(int)total;
                }
                byte[] clean=output.ToArray();if(oriented&&Encoding.ASCII.GetString(clean,12,4)=="VP8X")clean[20]|=8;
                Buffer.BlockCopy(BitConverter.GetBytes((uint)(clean.Length-8)),0,clean,4,4);return clean;
            }
        }
        static byte[] CleanJpeg(byte[] bytes)
        {
            using(var output=new MemoryStream())
            {
                output.Write(bytes,0,2);int p=2;
                while(p<bytes.Length)
                {
                    int start=p;if(bytes[p++]!=255)throw new InvalidDataException("JPEG 标记损坏。");
                    while(p<bytes.Length&&bytes[p]==255)p++;if(p>=bytes.Length)throw new InvalidDataException("JPEG 数据不完整。");int marker=bytes[p++];
                    if(marker==0xd9){output.Write(bytes,start,p-start);return output.ToArray();}
                    if(marker==0x01||(marker>=0xd0&&marker<=0xd7)){output.Write(bytes,start,p-start);continue;}
                    if(p+2>bytes.Length)throw new InvalidDataException("JPEG 数据不完整。");int length=bytes[p]*256+bytes[p+1];
                    if(length<2||p+length>bytes.Length)throw new InvalidDataException("JPEG 数据块不完整。");
                    bool app=marker>=0xe0&&marker<=0xef;
                    bool keep=!app&&marker!=0xfe;
                    if(marker==0xe0&&length>=7)keep=Encoding.ASCII.GetString(bytes,p+2,5)=="JFIF\0";
                    if(marker==0xee&&length>=7)keep=Encoding.ASCII.GetString(bytes,p+2,5)=="Adobe";
                    if(marker==0xe2&&length>=14)keep=Encoding.ASCII.GetString(bytes,p+2,12)=="ICC_PROFILE\0";
                    if(marker==0xe1&&length>=8&&Encoding.ASCII.GetString(bytes,p+2,6)=="Exif\0\0")
                    {
                        byte[] exif=new byte[length-8];Buffer.BlockCopy(bytes,p+8,exif,0,exif.Length);int orientation=Orientation(exif,0);
                        if(orientation>1)
                        {
                            byte[] minimal=OrientationExif(orientation);output.WriteByte(255);output.WriteByte(0xe1);output.WriteByte(0);output.WriteByte((byte)(minimal.Length+8));
                            byte[] signature=Encoding.ASCII.GetBytes("Exif\0\0");output.Write(signature,0,6);output.Write(minimal,0,minimal.Length);
                        }
                    }
                    if(keep)output.Write(bytes,start,p+length-start);p+=length;
                    if(marker==0xda)
                    {
                        int scan=p;
                        while(p<bytes.Length)
                        {
                            if(bytes[p]!=255){p++;continue;}
                            int next=p+1;while(next<bytes.Length&&bytes[next]==255)next++;
                            if(next>=bytes.Length)throw new InvalidDataException("JPEG 扫描数据不完整。");
                            if(bytes[next]==0||(bytes[next]>=0xd0&&bytes[next]<=0xd7)){p=next+1;continue;}break;
                        }
                        output.Write(bytes,scan,p-scan);
                    }
                }
                throw new InvalidDataException("JPEG 缺少图像数据。");
            }
        }
        static byte[] Pixels(Bitmap image)
        {
            var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{byte[] bytes=new byte[checked(image.Width*image.Height*4)];for(int y=0;y<image.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),bytes,y*image.Width*4,image.Width*4);return bytes;}
            finally{image.UnlockBits(data);}
        }
        static void Put(Bitmap image,byte[] bytes)
        {
            var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<image.Height;y++)Marshal.Copy(bytes,y*image.Width*4,IntPtr.Add(data.Scan0,y*data.Stride),image.Width*4);}
            finally{image.UnlockBits(data);}
        }
        public static Bitmap Redact(Bitmap image,Rectangle rectangle,IList<Point> stroke,int brushWidth,int strength,string effect,Color color)
        {
            using(var mask=new GraphicsPath())
            {
                if(stroke!=null&&stroke.Count>0)
                {
                    int half=Math.Max(1,brushWidth)/2;
                    if(stroke.Count>1)
                    {
                        using(var line=new GraphicsPath())using(var pen=new Pen(Color.Black,brushWidth){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
                        {line.AddLines(stroke.ToArray());line.Widen(pen);mask.AddPath(line,false);}
                    }
                    foreach(var point in stroke)mask.AddEllipse(point.X-half,point.Y-half,brushWidth,brushWidth);
                    mask.FillMode=FillMode.Winding;
                }
                else
                {
                    rectangle=Rectangle.Intersect(rectangle,new Rectangle(0,0,image.Width,image.Height));
                    if(rectangle.Width<1||rectangle.Height<1)throw new ArgumentException("请先框选或涂抹需要处理的区域。");
                    mask.AddRectangle(rectangle);
                }
                Rectangle bounds=Rectangle.Ceiling(mask.GetBounds());bounds.Inflate(Math.Max(1,strength),Math.Max(1,strength));
                bounds=Rectangle.Intersect(bounds,new Rectangle(0,0,image.Width,image.Height));
                if(bounds.Width<1||bounds.Height<1)throw new ArgumentException("选区在图片以外。");
                var result=image.Clone(new Rectangle(0,0,image.Width,image.Height),PixelFormat.Format32bppArgb);
                try
                {
                    using(var g=Graphics.FromImage(result))
                    {
                        g.SetClip(mask);g.CompositingMode=CompositingMode.SourceCopy;
                        if(effect=="solid")using(var fill=new SolidBrush(color))g.FillRectangle(fill,bounds);
                        else using(var patch=image.Clone(bounds,PixelFormat.Format32bppArgb))
                        {
                            int w=patch.Width,h=patch.Height;byte[] source=Pixels(patch),output=new byte[source.Length];
                            if(effect=="blur")
                            {
                                int radius=Math.Max(1,strength/2);byte[] horizontal=new byte[source.Length];
                                for(int y=0;y<h;y++)for(int c=0;c<4;c++)
                                {
                                    int sum=0,count=0;
                                    for(int x=0;x<=Math.Min(w-1,radius);x++){sum+=source[(y*w+x)*4+c];count++;}
                                    for(int x=0;x<w;x++)
                                    {
                                        horizontal[(y*w+x)*4+c]=(byte)(sum/count);
                                        if(x-radius>=0){sum-=source[(y*w+x-radius)*4+c];count--;}
                                        if(x+radius+1<w){sum+=source[(y*w+x+radius+1)*4+c];count++;}
                                    }
                                }
                                for(int x=0;x<w;x++)for(int c=0;c<4;c++)
                                {
                                    int sum=0,count=0;
                                    for(int y=0;y<=Math.Min(h-1,radius);y++){sum+=horizontal[(y*w+x)*4+c];count++;}
                                    for(int y=0;y<h;y++)
                                    {
                                        output[(y*w+x)*4+c]=(byte)(sum/count);
                                        if(y-radius>=0){sum-=horizontal[((y-radius)*w+x)*4+c];count--;}
                                        if(y+radius+1<h){sum+=horizontal[((y+radius+1)*w+x)*4+c];count++;}
                                    }
                                }
                            }
                            else
                            {
                                int block=Math.Max(2,strength);
                                for(int y=0;y<h;y+=block)for(int x=0;x<w;x+=block)
                                {
                                    int endX=Math.Min(w,x+block),endY=Math.Min(h,y+block),count=(endX-x)*(endY-y);long[] sums=new long[4];
                                    for(int yy=y;yy<endY;yy++)for(int xx=x;xx<endX;xx++)for(int c=0;c<4;c++)sums[c]+=source[(yy*w+xx)*4+c];
                                    for(int yy=y;yy<endY;yy++)for(int xx=x;xx<endX;xx++)for(int c=0;c<4;c++)output[(yy*w+xx)*4+c]=(byte)(sums[c]/count);
                                }
                            }
                            Put(patch,output);g.DrawImageUnscaled(patch,bounds.Location);
                        }
                    }
                    return result;
                }
                catch{result.Dispose();throw;}
            }
        }
    }

    public static class GifCodec
    {
        static int Word(byte[] b,int p){return b[p]|b[p+1]<<8;}
        static void Word(Stream s,int value){s.WriteByte((byte)value);s.WriteByte((byte)(value>>8));}
        static int TableSize(byte packed){return 3*(1<<((packed&7)+1));}
        static int BlocksEnd(byte[] b,int p)
        {
            while(true){if(p>=b.Length)throw new InvalidDataException("GIF 数据不完整。");int size=b[p++];if(size==0)return p;if(p+size>b.Length)throw new InvalidDataException("GIF 数据不完整。");p+=size;}
        }
        public static byte[] Clean(byte[] input)
        {
            if(input.Length<13||Encoding.ASCII.GetString(input,0,3)!="GIF")throw new InvalidDataException("不是 GIF 文件。");
            int p=13+((input[10]&128)!=0?TableSize(input[10]):0);if(p>input.Length)throw new InvalidDataException("GIF 色表不完整。");
            using(var output=new MemoryStream())
            {
                output.Write(input,0,p);
                while(p<input.Length)
                {
                    int start=p;byte block=input[p++];
                    if(block==0x3b){output.WriteByte(block);return output.ToArray();}
                    if(block==0x2c)
                    {
                        if(p+9>input.Length)throw new InvalidDataException("GIF 图像块不完整。");byte packed=input[p+8];p+=9;
                        if((packed&128)!=0)p+=TableSize(packed);if(p>=input.Length)throw new InvalidDataException("GIF 图像块不完整。");
                        p=BlocksEnd(input,p+1);output.Write(input,start,p-start);
                    }
                    else if(block==0x21)
                    {
                        if(p>=input.Length)throw new InvalidDataException("GIF 扩展块不完整。");byte label=input[p++];int extStart=p;p=BlocksEnd(input,p);
                        bool keep=label==0xf9||label==0x01;
                        if(label==0xff&&input[extStart]==11)
                        {
                            string app=Encoding.ASCII.GetString(input,extStart+1,11);keep=app=="NETSCAPE2.0"||app=="ANIMEXTS1.0";
                        }
                        if(keep)output.Write(input,start,p-start);
                    }
                    else throw new InvalidDataException("无法识别 GIF 数据块。");
                }
                throw new InvalidDataException("GIF 缺少结束块。");
            }
        }
        static void Frame(Stream output,Bitmap bitmap,int delay)
        {
            byte[] b;using(var s=new MemoryStream()){bitmap.Save(s,ImageFormat.Gif);b=s.ToArray();}
            int globalSize=(b[10]&128)!=0?TableSize(b[10]):0,p=13+globalSize;
            byte paletteFlags=(byte)(b[10]&7);int paletteStart=13,paletteSize=globalSize;
            while(p<b.Length)
            {
                byte type=b[p++];if(type==0x21){p++;p=BlocksEnd(b,p);continue;}
                if(type!=0x2c)throw new InvalidDataException("系统 GIF 编码器未返回图像帧。");
                int descriptor=p;if(p+9>b.Length)throw new InvalidDataException("GIF 图像帧不完整。");byte flags=b[p+8];p+=9;
                if((flags&128)!=0){paletteFlags=(byte)(flags&7);paletteStart=p;paletteSize=TableSize(flags);p+=paletteSize;}
                if(paletteSize==0)throw new InvalidDataException("GIF 帧缺少色表。");
                int imageStart=p;p=BlocksEnd(b,p+1);
                output.WriteByte(0x21);output.WriteByte(0xf9);output.WriteByte(4);output.WriteByte(4);Word(output,Math.Max(1,delay/10));output.WriteByte(0);output.WriteByte(0);
                output.WriteByte(0x2c);Word(output,0);Word(output,0);Word(output,bitmap.Width);Word(output,bitmap.Height);
                output.WriteByte((byte)(128|(flags&64)|paletteFlags));output.Write(b,paletteStart,paletteSize);output.Write(b,imageStart,p-imageStart);return;
            }
            throw new InvalidDataException("GIF 图像帧缺失。");
        }
        public static byte[] Merge(IList<string> files,int maxSide,int delay,bool loop,Color background,CancellationToken token,IProgress<int> progress)
        {
            if(files.Count<2||files.Count>200)throw new ArgumentException("合成 GIF 需要 2 至 200 张图片。");
            if(maxSide<1||maxSide>2048||delay<20||delay>10000)throw new ArgumentException("GIF 尺寸或帧间隔超出范围。");
            Size canvas;using(var first=Codec.Load(files[0]))canvas=Codec.OutputSize(first,maxSide);
            using(var output=new MemoryStream())
            {
                byte[] header=Encoding.ASCII.GetBytes("GIF89a");output.Write(header,0,header.Length);Word(output,canvas.Width);Word(output,canvas.Height);
                output.WriteByte(0x70);output.WriteByte(0);output.WriteByte(0);
                if(loop){byte[] ext={0x21,0xff,11,78,69,84,83,67,65,80,69,50,46,48,3,1,0,0,0};output.Write(ext,0,ext.Length);}
                for(int i=0;i<files.Count;i++)
                {
                    token.ThrowIfCancellationRequested();
                    using(var source=Codec.Load(files[i]))using(var frame=Codec.Fit(source,canvas.Width,canvas.Height,background))Frame(output,frame,delay);
                    if(output.Length>160L*1024*1024)throw new InvalidDataException("GIF 超过 160 MB，请减少帧数或降低尺寸。");
                    if(progress!=null)progress.Report(i+1);
                }
                token.ThrowIfCancellationRequested();output.WriteByte(0x3b);return output.ToArray();
            }
        }
    }
}
