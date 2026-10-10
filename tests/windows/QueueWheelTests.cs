using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QQImageSwitch
{
    static class QueueWheelTests
    {
        class TestForm : Form {protected override bool ShowWithoutActivation {get{return true;}}}
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wparam,IntPtr lparam);
        static void Wheel(Control target,int delta)
        {
            SendMessage(target.Handle,0x20a,new IntPtr((long)(ushort)(short)delta<<16),IntPtr.Zero);
            Application.DoEvents();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception("Queue wheel: "+message);}
        public static void Run()
        {
            using(var form=new TestForm {Size=new Size(500,400),Opacity=0,ShowInTaskbar=false})
            using(var page=new SoftScrollPanel {Dock=DockStyle.Fill,AutoScroll=true,AutoScrollMinSize=new Size(0,1800)})
            using(var list=new SoftList {Bounds=new Rectangle(10,20,340,160),IntegralHeight=false})
            using(var grid=new SoftGrid {Bounds=new Rectangle(10,200,340,160),AllowUserToAddRows=false,RowHeadersVisible=false})
            {
                form.Controls.Add(page);page.Controls.Add(list);page.Controls.Add(grid);grid.Columns.Add("file","File");
                form.Show();Application.DoEvents();
                Check(page.VerticalScroll.Visible,"outer scrollbar missing");
                Wheel(list,-120);Check(page.VerticalScroll.Value>0,"empty list swallowed downward wheel");
                int before=page.VerticalScroll.Value;Wheel(list,120);Check(page.VerticalScroll.Value<before,"empty list swallowed upward wheel");
                list.Items.Add("one");list.Items.Add("two");Wheel(list,-120);Check(page.VerticalScroll.Value>0,"short queue swallowed wheel");
                before=page.VerticalScroll.Value;Wheel(grid,-120);Check(page.VerticalScroll.Value>before,"empty grid swallowed downward wheel");
                before=page.VerticalScroll.Value;Wheel(grid,120);Check(page.VerticalScroll.Value<before,"empty grid swallowed upward wheel");
                for(int i=0;i<70;i++){list.Items.Add("file "+i);grid.Rows.Add("file "+i);}
                Application.DoEvents();page.AutoScrollPosition=Point.Empty;Application.DoEvents();
                before=page.VerticalScroll.Value;Wheel(list,-120);Check(list.TopIndex>0&&page.VerticalScroll.Value==before,"list did not scroll internally first");
                list.TopIndex=list.Items.Count-1;before=page.VerticalScroll.Value;Wheel(list,-120);Check(page.VerticalScroll.Value>before,"list bottom did not hand off");
                list.TopIndex=0;before=page.VerticalScroll.Value;Wheel(list,120);Check(page.VerticalScroll.Value<before,"list top did not hand off");
                page.AutoScrollPosition=Point.Empty;grid.FirstDisplayedScrollingRowIndex=0;Application.DoEvents();
                before=page.VerticalScroll.Value;Wheel(grid,-120);Check(grid.FirstDisplayedScrollingRowIndex>0&&page.VerticalScroll.Value==before,"grid did not scroll internally first");
                grid.FirstDisplayedScrollingRowIndex=grid.Rows.Count-1;before=page.VerticalScroll.Value;Wheel(grid,-120);Check(page.VerticalScroll.Value>before,"grid bottom did not hand off");
                grid.FirstDisplayedScrollingRowIndex=0;before=page.VerticalScroll.Value;Wheel(grid,120);Check(page.VerticalScroll.Value<before,"grid top did not hand off");
                Check(list.SelectedIndex==-1&&grid.Rows.Count==70,"wheel changed queue contents or selection");
                using(var number=new SoftNumber{Bounds=new Rectangle(360,20,100,30),Value=50})
                {
                    page.Controls.Add(number);page.AutoScrollPosition=Point.Empty;list.Focus();Application.DoEvents();before=page.VerticalScroll.Value;
                    Wheel(number,-120);Check(number.Value==49&&page.VerticalScroll.Value==before,"hovered number did not decrement independently");
                    Wheel(number,120);Check(number.Value==50&&page.VerticalScroll.Value==before,"number did not increment independently");
                    foreach(Control child in number.Controls){list.Focus();Application.DoEvents();Wheel(child,-120);Check(number.Value==49&&page.VerticalScroll.Value==before,"numeric child did not decrement once");Wheel(child,120);Check(number.Value==50&&page.VerticalScroll.Value==before,"numeric child did not increment once");}
                    Wheel(number,60);Check(number.Value==50,"partial wheel moved too early");Wheel(number,60);Check(number.Value==51,"partial wheel did not accumulate");
                    number.Value=number.Maximum;Wheel(number,120);Check(number.Value==number.Maximum,"wheel exceeded maximum");number.Value=number.Minimum;Wheel(number,-120);Check(number.Value==number.Minimum,"wheel exceeded minimum");
                    number.DecimalPlaces=1;number.Increment=.5m;number.Value=5;Wheel(number,240);Check(number.Value==6,"wheel lost configured decimal increment");
                    number.Enabled=false;Wheel(number,120);Check(number.Value==6,"disabled number changed value");
                }
                form.Close();
            }
        }
    }
}
