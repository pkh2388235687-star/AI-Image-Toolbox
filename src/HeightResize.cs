using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace QQImageSwitch
{
    // A native overlay keeps the grip out of table rows and preferred-size calculations.
    static class HeightResize
    {
        static readonly ConditionalWeakTable<Control,Grip> bindings=new ConditionalWeakTable<Control,Grip>();
        internal static int Resolve(Control target,int automatic){Grip grip;return bindings.TryGetValue(target,out grip)&&grip.Height>0?grip.Height:automatic;}
        internal static bool Manual(Control target){Grip grip;return bindings.TryGetValue(target,out grip)&&grip.Height>0;}
        internal static void Set(Control target,int height){Grip grip;if(!bindings.TryGetValue(target,out grip)){Enable(target);bindings.TryGetValue(target,out grip);}grip.Height=height;}
        internal static void Enable(Control target,Action<int> resize=null,int minimum=100)
        {
            Grip grip;if(!bindings.TryGetValue(target,out grip)){
                if(target is SoftTable&&target.BackColor==Color.White&&target.Padding.Horizontal>0){var padding=target.Padding;padding.Right=Math.Max(padding.Right,34);if(!(target is CompactFooter))padding.Bottom=Math.Max(padding.Bottom,34);target.Padding=padding;}
                grip=new Grip(target,minimum);bindings.Add(target,grip);
            }
            if(resize!=null)grip.Resize=resize;
        }
        internal static void Tree(Control root)
        {
            if(root is ImageCanvas||root is ListBox||root is DataGridView||(root is SoftTable&&root.BackColor==Color.White&&root.Padding.Horizontal>0))Enable(root,null,root is ImageCanvas?120:root is ListBox||root is DataGridView||root is CompactFooter?100:160);
            foreach(Control child in root.Controls)Tree(child);
        }
        internal static void TestResize(Control target,int height){Grip grip;if(!bindings.TryGetValue(target,out grip))throw new Exception("Missing height grip: "+target.GetType().Name);grip.Apply(height);}
        internal static Rectangle HandleBounds(Control target){Grip grip;return bindings.TryGetValue(target,out grip)?grip.Corner:Rectangle.Empty;}
        internal static bool HasGrip(Control target){Grip grip;return bindings.TryGetValue(target,out grip);}
        internal static void Paint(Control target,Graphics graphics){Grip grip;if(bindings.TryGetValue(target,out grip))grip.Paint(graphics);}
        sealed class Grip : NativeWindow
        {
            readonly Control target;readonly int minimum,automaticHeight;readonly float automaticFont;readonly bool automatic,scrolling;readonly ToolTip tip=new ToolTip{InitialDelay=400,ShowAlways=true};
            int originalHeight,startY,naturalHeight;bool dragging;Cursor cursor;public int Height;public Action<int> Resize;
            internal Grip(Control control,int min)
            {
                target=control;minimum=min;automaticHeight=control.Height;automaticFont=control.Font.Size;automatic=control.AutoSize;var scroll=control as ScrollableControl;scrolling=scroll!=null&&scroll.AutoScroll;
                target.HandleCreated+=Created;target.HandleDestroyed+=Destroyed;target.Disposed+=Disposed;
                if(target.IsHandleCreated)AssignHandle(target.Handle);
            }
            void Created(object sender,EventArgs e){AssignHandle(target.Handle);}
            void Destroyed(object sender,EventArgs e){ReleaseHandle();}
            void Disposed(object sender,EventArgs e){tip.Dispose();target.HandleCreated-=Created;target.HandleDestroyed-=Destroyed;target.Disposed-=Disposed;ReleaseHandle();}
            internal Rectangle Corner{get{int size=Math.Max(14,Math.Min(18,target.Font.Height/2+3));return new Rectangle(Math.Max(0,target.ClientSize.Width-size-12),Math.Max(0,target.ClientSize.Height-size-12),size,size);}}
            Rectangle HitCorner{get{var r=Corner;r.Inflate(4,4);return Rectangle.Intersect(r,target.ClientRectangle);}}
            internal void Paint(Graphics graphics)
            {
                if(!target.Visible||target.ClientSize.Width<40||target.ClientSize.Height<40)return;
                var r=Corner;
                using(var shape=SoftTheme.Round(r,4))using(var fill=new SolidBrush(Color.FromArgb(231,221,248)))using(var border=new Pen(Color.FromArgb(178,153,222)))
                {graphics.FillPath(fill,shape);graphics.DrawPath(border,shape);}
                using(var pen=new Pen(Color.FromArgb(113,86,169),1.4f))
                    for(int n=0;n<3;n++){int offset=5+n*3;graphics.DrawLine(pen,r.Right-offset,r.Bottom-3,r.Right-3,r.Bottom-offset);}
            }
            internal void Apply(int height)
            {
                Height=height<=0?0:Math.Max(minimum,Math.Min(4096,height));
                if(Resize!=null)Resize(Height);
                else
                {
                    int next=Height>0?Height:(int)Math.Round(automaticHeight*target.Font.Size/automaticFont);if(next<=0)next=target.Height;
                    if(target is ImageCanvas||target is DataGridView||target is ListBox)
                    {
                        target.MinimumSize=new Size(target.MinimumSize.Width,Height>0?next:0);
                        var table=target.Parent as TableLayoutPanel;if(table!=null){int row=table.GetRow(target);if(row>=0&&row<table.RowStyles.Count){table.RowStyles[row].SizeType=SizeType.Absolute;table.RowStyles[row].Height=next+target.Margin.Vertical;}}
                    }
                    else if(!(target is CompactFooter))
                    {
                        if(naturalHeight==0)naturalHeight=target.GetPreferredSize(new Size(target.Width,0)).Height;
                        target.AutoSize=Height==0&&automatic;var scroll=target as ScrollableControl;if(scroll!=null){scroll.AutoScroll=Height>0||scrolling;scroll.AutoScrollMinSize=new Size(0,Height>0?naturalHeight:0);}
                    }
                    target.Height=next;if(target.Parent!=null)target.Parent.PerformLayout();target.PerformLayout();
                }
                target.Invalidate();
            }
            Point Location(IntPtr value){long v=value.ToInt64();return new Point((short)v,(short)(v>>16));}
            protected override void WndProc(ref Message m)
            {
                if(target.IsDisposed){base.WndProc(ref m);return;}
                if(m.Msg==0x201&&HitCorner.Contains(Location(m.LParam)))
                {
                    originalHeight=target.Height;startY=target.PointToScreen(Location(m.LParam)).Y;dragging=true;if(cursor==null)cursor=target.Cursor;target.Cursor=Cursors.SizeNS;target.Capture=true;m.Result=IntPtr.Zero;return;
                }
                if(m.Msg==0x203&&HitCorner.Contains(Location(m.LParam))){Apply(0);m.Result=IntPtr.Zero;return;}
                if(m.Msg==0x200)
                {
                    if(dragging){Apply(originalHeight+target.PointToScreen(Location(m.LParam)).Y-startY);m.Result=IntPtr.Zero;return;}
                    if(HitCorner.Contains(Location(m.LParam))){if(cursor==null)cursor=target.Cursor;target.Cursor=Cursors.SizeNS;tip.SetToolTip(target,L.T("拖动调整高度；双击恢复自动高度"));}
                    else if(cursor!=null){target.Cursor=cursor;cursor=null;tip.SetToolTip(target,null);}
                }
                if(m.Msg==0x202&&dragging){dragging=false;target.Capture=false;if(cursor!=null)target.Cursor=cursor;cursor=null;m.Result=IntPtr.Zero;return;}
                if(m.Msg==0x215)dragging=false;
                if(m.Msg==0x2a3&&!dragging&&cursor!=null){target.Cursor=cursor;cursor=null;tip.SetToolTip(target,null);}

                base.WndProc(ref m);
                if(m.Msg==0xf&&target.IsHandleCreated){using(var g=Graphics.FromHwnd(target.Handle))Paint(g);}
                else if(m.Msg==0x318&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))Paint(g);}
            }
        }
    }
    class ResizableTextBox : TextBox
    {
        int automaticHeight=44;bool resizing;
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(!resizing&&!HeightResize.Manual(this))automaticHeight=Math.Max(44,Height);}
        public ResizableTextBox(){HeightResize.Enable(this,h=>{resizing=true;try{int next=h>0?h:automaticHeight;MinimumSize=new Size(80,next);Height=next;if(Parent!=null)Parent.PerformLayout();}finally{resizing=false;}},44);}
    }
}
