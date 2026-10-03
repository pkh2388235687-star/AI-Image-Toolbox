$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing.dll -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class AppIconBuilder
{
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr LoadImage(IntPtr instance,string file,uint type,int width,int height,uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static GraphicsPath Round(float x,float y,float w,float h,float r)
    {
        var p=new GraphicsPath();float d=r*2;
        p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
    }
    static void Box(Graphics g,float x,float y,float w,float h,float r,Color color)
    {using(var p=Round(x,y,w,h,r))using(var b=new SolidBrush(color))g.FillPath(b,p);}
    static PointF[] Star(float x,float y,float a,float b)
    {return new[]{new PointF(x,y-a),new PointF(x+b,y-b),new PointF(x+a,y),new PointF(x+b,y+b),new PointF(x,y+a),new PointF(x-b,y+b),new PointF(x-a,y),new PointF(x-b,y-b)};}
    public static Bitmap Draw(int size)
    {
        using(var master=new Bitmap(1024,1024,PixelFormat.Format32bppArgb))
        {
            using(var g=Graphics.FromImage(master))
            {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(4,4);
                using(var p=Round(8,8,240,240,58))using(var fill=new LinearGradientBrush(new Rectangle(8,8,240,240),Color.FromArgb(193,173,243),Color.FromArgb(136,114,215),45f))using(var border=new Pen(Color.FromArgb(232,221,251),2)){g.FillPath(fill,p);g.DrawPath(border,p);}
                Box(g,42,54,172,160,28,Color.FromArgb(50,110,85,189));
                Box(g,34,40,166,156,27,Color.FromArgb(229,223,252));
                Box(g,52,58,166,156,27,Color.FromArgb(255,253,254));
                using(var clip=Round(66,72,138,128,17))
                {
                    var state=g.Save();g.SetClip(clip);using(var sky=new SolidBrush(Color.FromArgb(220,239,245)))g.FillRectangle(sky,66,72,138,128);
                    using(var sun=new SolidBrush(Color.FromArgb(241,170,191)))g.FillEllipse(sun,87,90,32,32);
                    using(var hill=new SolidBrush(Color.FromArgb(168,198,200)))g.FillPolygon(hill,new[]{new PointF(66,175),new PointF(115,127),new PointF(167,183),new PointF(188,161),new PointF(204,178),new PointF(204,210),new PointF(66,210)});
                    using(var hill=new SolidBrush(Color.FromArgb(129,157,173)))g.FillPolygon(hill,new[]{new PointF(66,190),new PointF(144,144),new PointF(204,184),new PointF(204,210),new PointF(66,210)});
                    g.Restore(state);
                }
                using(var fill=new SolidBrush(Color.FromArgb(154,123,219)))using(var border=new Pen(Color.FromArgb(255,251,255),5){LineJoin=LineJoin.Round}){var star=Star(190,72,37,10);g.FillPolygon(fill,star);g.DrawPolygon(border,star);}
                using(var fill=new SolidBrush(Color.FromArgb(255,246,207)))g.FillPolygon(fill,Star(57,223,14,4));
            }
            var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(master,new Rectangle(0,0,size,size));}return result;
        }
    }
    public static void Build(string directory)
    {
        Directory.CreateDirectory(directory);int[] sizes={16,20,24,32,40,48,64,128,256};byte[][] images=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++)using(var b=Draw(sizes[i]))using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            int size=sizes[i],maskStride=((size+31)/32)*4;
            writer.Write(40);writer.Write(size);writer.Write(size*2);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(0);writer.Write(size*size*4+maskStride*size);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
            for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++){Color c=b.GetPixel(x,y);writer.Write(c.B);writer.Write(c.G);writer.Write(c.R);writer.Write(c.A);}
            for(int y=size-1;y>=0;y--){byte[] row=new byte[maskStride];for(int x=0;x<size;x++)if(b.GetPixel(x,y).A==0)row[x/8]|=(byte)(0x80>>(x%8));writer.Write(row);}
            writer.Flush();images[i]=stream.ToArray();
        }
        using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,"app.ico"))))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
            foreach(byte[] bytes in images)writer.Write(bytes);
        }
        foreach(int size in sizes)
        {
            var handle=LoadImage(IntPtr.Zero,Path.Combine(directory,"app.ico"),1,size,size,0x10);if(handle==IntPtr.Zero)throw new Exception("Windows could not load icon: "+size);
            try{using(var icon=Icon.FromHandle(handle))using(var bitmap=icon.ToBitmap()){if(bitmap.Width!=size)throw new Exception("Icon size failed: "+size);}}finally{DestroyIcon(handle);}
        }
    }
}
'@
[AppIconBuilder]::Build((Join-Path $PSScriptRoot 'assets'))
Write-Output 'Built app icon: 16, 20, 24, 32, 40, 48, 64, 128, 256 pixels.'
