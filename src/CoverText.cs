using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QQImageSwitch
{
    public class CoverTextOptions
    {
        public string Text="";
        // Keep legacy IDs 0..3 so existing remembered positions remain valid.
        public int Position;
        public int X=50,Y=50;
        public int SizePercent=7;
        public Color Color=Color.White;
        public CoverTextOptions Copy(){return new CoverTextOptions {Text=Text,Position=Position,X=X,Y=Y,SizePercent=SizePercent,Color=Color};}
        public void Draw(Bitmap bitmap,bool numbered)
        {
            string text=(Text??"").Trim();if(text.Length==0)return;
            if(text.Length>200)throw new ArgumentException("封面文字最多 200 个字。");
            float unit=Math.Min(bitmap.Width,bitmap.Height),margin=Math.Max(1,unit*.04f);
            float available=Math.Max(1,bitmap.Width-margin*2);
            if((Position==1||Position==2||Position==9)&&numbered)available=Math.Max(1,available-Math.Max(24,unit*.12f)-margin);
            int column=Position==1||Position==6||Position==8?2:Position==4||Position==7||Position==9?0:1;
            int row=Position==4||Position==5||Position==6?0:Position==1||Position==2||Position==9?2:1;
            using(var g=Graphics.FromImage(bitmap))
            using(var format=new StringFormat {Alignment=column==2?StringAlignment.Far:column==0?StringAlignment.Near:StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap})
            {
                float size=Math.Max(1,unit*Math.Max(2,Math.Min(18,SizePercent))/100f);
                using(var probe=new Font("Microsoft YaHei UI",size,FontStyle.Bold,GraphicsUnit.Pixel))
                {float measured=g.MeasureString(text,probe).Width;if(measured>available)size=Math.Max(1,size*available/measured);}
                float height=Math.Min(bitmap.Height-margin*2,size*1.8f);
                var bounds=new RectangleF(margin,row==0?margin:row==1?(bitmap.Height-height)/2:bitmap.Height-margin-height,available,Math.Max(1,height));
                if(Position==3)
                {
                    using(var probe=new Font("Microsoft YaHei UI",size,FontStyle.Bold,GraphicsUnit.Pixel))
                    {
                        float width=Math.Min(available,g.MeasureString(text,probe).Width+size*.12f);
                        float x=Math.Max(margin,Math.Min(bitmap.Width-margin-width,bitmap.Width*Math.Max(0,Math.Min(100,X))/100f-width/2));
                        float y=Math.Max(margin,Math.Min(bitmap.Height-margin-height,bitmap.Height*Math.Max(0,Math.Min(100,Y))/100f-height/2));
                        bounds=new RectangleF(x,y,width,Math.Max(1,height));
                    }
                }
                using(var path=new GraphicsPath())
                using(var ink=new SolidBrush(Color))
                using(var outline=new Pen(Color.GetBrightness()>.5f?System.Drawing.Color.FromArgb(180,30,25,45):System.Drawing.Color.FromArgb(200,255,255,255),Math.Max(1,size*.065f)){LineJoin=LineJoin.Round})
                using(var family=new FontFamily("Microsoft YaHei UI"))
                {
                    path.AddString(text,family,(int)FontStyle.Bold,size,bounds,format);
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawPath(outline,path);g.FillPath(ink,path);
                }
            }
        }
    }
    public class TextPositionEventArgs : EventArgs {public int X,Y;public bool Finished;}
    public class CoverCanvas : ImageCanvas
    {
        public bool TextPlacement;
        bool dragging;
        public event EventHandler<TextPositionEventArgs> PositionChanged;
        void Position(Point p,bool finished)
        {
            if(Image==null)return;
            var point=ToImage(p);
            int x=(int)Math.Round(point.X*100.0/Image.Width),y=(int)Math.Round(point.Y*100.0/Image.Height);
            if(PositionChanged!=null)PositionChanged(this,new TextPositionEventArgs {X=Math.Max(0,Math.Min(100,x)),Y=Math.Max(0,Math.Min(100,y)),Finished=finished});
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(TextPlacement&&Image!=null&&e.Button==MouseButtons.Left){dragging=true;Capture=true;Position(e.Location,false);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Position(e.Location,false);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging){dragging=false;Capture=false;Position(e.Location,true);}}
        protected override void OnClick(EventArgs e){if(!TextPlacement)base.OnClick(e);}
        internal void TestDrag(Point a,Point b){OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,a.X,a.Y,0));OnMouseMove(new MouseEventArgs(MouseButtons.Left,1,b.X,b.Y,0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,b.X,b.Y,0));}
    }
}
