using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QQImageSwitch
{
    public sealed class TextLayerSelector : UserControl
    {
        readonly Func<CoverTextOptions> model;readonly Action<CoverTextOptions> load;readonly Action changed;readonly SoftCombo choice;int selected;bool filling;
        public CoverTextOptions Active {get{var root=model();return selected>0&&selected<=root.Extra.Count?root.Extra[selected-1]:root;}}
        public TextLayerSelector(Func<CoverTextOptions> model,Action<CoverTextOptions> load,Action changed,Action commit=null)
        {
            this.model=model;this.load=load;this.changed=changed;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Dock=DockStyle.Top;BackColor=Color.Transparent;
            choice=new SoftCombo{Width=240,VisibleRows=3,Tag="raw"};Controls.Add(Ui.Flow(Ui.Text("文字图层"),choice,Ui.Button("添加文字",delegate{if(commit!=null)commit();if(model().Extra.Count>=31){MessageBox.Show(L.T("最多添加 32 条文字。"));return;}model().Extra.Add(new CoverTextOptions{Position=3,Y=75,Text=""});selected=model().Extra.Count;RefreshChoices();load(Active);changed();}),Ui.Button("删除当前文字",delegate{if(commit!=null)commit();if(selected>0){model().Extra.RemoveAt(selected-1);selected=Math.Max(0,selected-1);}else if(model().Extra.Count>0){var first=model().Extra[0];model().Assign(first);model().Extra.RemoveAt(0);}else model().Text="";RefreshChoices();load(Active);changed();})));
            choice.SelectedIndexChanged+=delegate{if(filling||choice.SelectedIndex<0)return;if(commit!=null)commit();selected=choice.SelectedIndex;load(Active);};L.Changed+=LanguageChanged;RefreshChoices();
        }
        public static string Caption(int index,string text)
        {
            string value=System.Text.RegularExpressions.Regex.Replace(text??"",@"\s+"," ").Trim();var elements=new System.Globalization.StringInfo(value);if(elements.LengthInTextElements>18)value=elements.SubstringByTextElements(0,18)+"…";
            return (index+1)+" · "+(value.Length==0?L.T("空文字"):value);
        }
        public void RefreshNames(string current=null)
        {
            if(choice.Items.Count!=model().Extra.Count+1){RefreshChoices();return;}bool edited=false;for(int i=0;i<choice.Items.Count;i++){string name=Caption(i,current!=null&&i==selected?current:i==0?model().Text:model().Extra[i-1].Text);if(choice.Items[i].ToString()!=name){choice.Items[i]=name;edited=true;}}if(edited)choice.Invalidate();
        }
        public void RefreshChoices(){filling=true;try{selected=Math.Max(0,Math.Min(model().Extra.Count,selected));choice.SelectedIndex=-1;choice.Items.Clear();for(int i=0;i<=model().Extra.Count;i++)choice.Items.Add(Caption(i,i==0?model().Text:model().Extra[i-1].Text));choice.SelectedIndex=selected;choice.Invalidate();}finally{filling=false;}}
        void LanguageChanged(){RefreshNames();}
        protected override void Dispose(bool disposing){if(disposing)L.Changed-=LanguageChanged;base.Dispose(disposing);}
#if SELF_TEST
        internal string TestCaption(int index){return choice.Items[index].ToString();}
        internal void TestAdd(){((Button)Controls[0].Controls[2]).PerformClick();}
        internal void TestDelete(){((Button)Controls[0].Controls[3]).PerformClick();}
        internal void TestChoose(int index){choice.TestChoice(index);}
#endif
    }
}
