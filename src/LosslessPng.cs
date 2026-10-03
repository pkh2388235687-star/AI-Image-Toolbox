using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace QQImageSwitch
{
    // PNG scanline prediction and DEFLATE; no quantization or pixel changes.
    static class LosslessPng
    {
        internal static void WriteBest(Bitmap image,Stream output,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();long start=output.Position;image.Save(output,ImageFormat.Png);long nativeLength=output.Position-start;
            string path=Path.Combine(Path.GetTempPath(),"aipt-png-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var alternative=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,65536))
                {
                    WriteAdaptive(image,alternative,token);token.ThrowIfCancellationRequested();
                    if(alternative.Length<nativeLength)
                    {
                        alternative.Position=0;output.Position=start;output.SetLength(start);var buffer=new byte[1048576];int count;
                        while((count=alternative.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();output.Write(buffer,0,count);}
                    }
                }
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        internal static void WriteAdaptive(Bitmap image,Stream output,CancellationToken token)
        {
            Codec.CheckSize(image.Width,image.Height);output.Write(new byte[]{137,80,78,71,13,10,26,10},0,8);
            var header=new byte[13];Put32(header,0,(uint)image.Width);Put32(header,4,(uint)image.Height);header[8]=8;header[9]=6;Codec.WriteChunk(output,"IHDR",header);
            var rectangle=new Rectangle(0,0,image.Width,image.Height);var bits=image.LockBits(rectangle,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try
            {
                int length=checked(image.Width*4);var bgra=new byte[length];var row=new byte[length];var previous=new byte[length];var trial=new byte[length];var best=new byte[length+1];uint a=1,b=0;
                using(var chunks=new ChunkStream(output))
                {
                    chunks.WriteByte(0x78);chunks.WriteByte(0x9c);
                    using(var compression=new DeflateStream(chunks,CompressionLevel.Optimal,true))
                    {
                        for(int y=0;y<image.Height;y++)
                        {
                            token.ThrowIfCancellationRequested();Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),bgra,0,length);
                            for(int x=0;x<length;x+=4){row[x]=bgra[x+2];row[x+1]=bgra[x+1];row[x+2]=bgra[x];row[x+3]=bgra[x+3];}
                            long bestScore=long.MaxValue;
                            for(byte filter=0;filter<5;filter++)
                            {
                                long score=0;
                                for(int x=0;x<length;x++)
                                {
                                    int left=x<4?0:row[x-4],up=previous[x],corner=x<4?0:previous[x-4];
                                    int prediction=filter==0?0:filter==1?left:filter==2?up:filter==3?(left+up)/2:Paeth(left,up,corner);
                                    byte value=unchecked((byte)(row[x]-prediction));trial[x]=value;score+=Math.Abs((int)unchecked((sbyte)value));
                                }
                                if(score<bestScore){bestScore=score;best[0]=filter;Buffer.BlockCopy(trial,0,best,1,length);}
                            }
                            for(int offset=0;offset<best.Length;)
                            {
                                int stop=Math.Min(offset+5552,best.Length);for(;offset<stop;offset++){a+=best[offset];b+=a;}a%=65521;b%=65521;
                            }
                            compression.Write(best,0,best.Length);var swap=previous;previous=row;row=swap;
                        }
                    }
                    var checksum=new byte[4];Put32(checksum,0,(b<<16)|a);chunks.Write(checksum,0,4);chunks.Flush();
                }
                Codec.WriteChunk(output,"IEND",new byte[0]);
            }
            finally{image.UnlockBits(bits);}
        }
        static int Paeth(int left,int up,int corner)
        {int value=left+up-corner,a=Math.Abs(value-left),b=Math.Abs(value-up),c=Math.Abs(value-corner);return a<=b&&a<=c?left:b<=c?up:corner;}
        static void Put32(byte[] data,int offset,uint value){data[offset]=(byte)(value>>24);data[offset+1]=(byte)(value>>16);data[offset+2]=(byte)(value>>8);data[offset+3]=(byte)value;}
        sealed class ChunkStream : Stream
        {
            readonly Stream target;readonly byte[] buffer=new byte[65536];int used;
            internal ChunkStream(Stream output){target=output;}
            public override void Write(byte[] data,int offset,int count)
            {while(count>0){int take=Math.Min(count,buffer.Length-used);Buffer.BlockCopy(data,offset,buffer,used,take);used+=take;offset+=take;count-=take;if(used==buffer.Length)Flush();}}
            public override void Flush(){if(used==0)return;var data=new byte[used];Buffer.BlockCopy(buffer,0,data,0,used);Codec.WriteChunk(target,"IDAT",data);used=0;}
            protected override void Dispose(bool disposing){if(disposing)Flush();base.Dispose(disposing);}
            public override bool CanRead{get{return false;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return true;}}
            public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
            public override int Read(byte[] data,int offset,int count){throw new NotSupportedException();}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}public override void SetLength(long length){throw new NotSupportedException();}
        }
    }
}
