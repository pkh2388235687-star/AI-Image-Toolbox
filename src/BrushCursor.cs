using System.IO;
using System.Windows.Forms;

namespace QQImageSwitch
{
    static class BrushCursor
    {
        static Cursor blank;
        public static Cursor Blank
        {
            get
            {
                if(blank==null)using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    int n=32,length=40+n*n*4+n*4;
                    writer.Write((ushort)0);writer.Write((ushort)2);writer.Write((ushort)1);writer.Write((byte)n);writer.Write((byte)n);writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write(length);writer.Write(22);
                    writer.Write(40);writer.Write(n);writer.Write(n*2);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(0);writer.Write(n*n*4+n*4);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(new byte[n*n*4]);for(int i=0;i<n*4;i++)writer.Write((byte)255);
                    writer.Flush();stream.Position=0;blank=new Cursor(stream);
                }
                return blank;
            }
        }
    }
}
