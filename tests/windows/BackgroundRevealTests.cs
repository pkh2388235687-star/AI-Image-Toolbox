using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace QQImageSwitch
{
    static class BackgroundRevealTests
    {
        static void Require(bool value,string message){if(!value)throw new Exception("Background reveal: "+message);}
        static double Gray(int v){return (.299*((v>>16)&255)+.587*((v>>8)&255)+.114*(v&255));}
        static double Error(int pixel,int light,int dark,int strength)
        {
            double ly=Gray(light),dy=Gray(dark),s=strength/100.0,total=0;int a=(int)((uint)pixel>>24);
            foreach(int shift in new[]{16,8,0}){double w=127.5+.5*(ly+(((light>>shift)&255)-ly)*s),b=.5*(dy+(((dark>>shift)&255)-dy)*s),p=((pixel>>shift)&255)*a/255.0;total+=(p-b)*(p-b)+(p+255-a-w)*(p+255-a-w);}return total;
        }
        static double ReferenceMinimum(int light,int dark,int strength)
        {
            double ly=Gray(light),dy=Gray(dark),s=strength/100.0,best=double.MaxValue;
            for(int a=0;a<=255;a++)
            {
                double e=0;foreach(int shift in new[]{16,8,0}){double w=127.5+.5*(ly+(((light>>shift)&255)-ly)*s),b=.5*(dy+(((dark>>shift)&255)-dy)*s);double p=Math.Max(0,Math.Min(a,(w+b-255+a)/2));e+=(p-b)*(p-b)+(p+255-a-w)*(p+255-a-w);}best=Math.Min(best,e);
            }return best;
        }


        static void SourceColorTests(string dir)
        {
            var rng=new Random(731);
            using(var vectors=new StreamWriter(Path.Combine(dir,"source-color-pixels.csv")))for(int i=0;i<320;i++){
                int w=unchecked((int)0xff000000)|rng.Next(0x1000000),b=unchecked((int)0xff000000)|rng.Next(0x1000000),c=unchecked((int)0xff000000)|rng.Next(0x1000000);
                var p=new BackgroundRevealAuto.Profile{SourceColor=true,Strength=(i%101)/100.0,ChromaLimit=(i%3)*10,Exposure=.45+(i%21)/100.0,WhiteLow=8,WhiteHigh=233};
                vectors.WriteLine(((uint)w).ToString("x8")+","+((uint)b).ToString("x8")+","+((uint)c).ToString("x8")+","+p.Strength.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+p.Exposure.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+p.ChromaLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+((uint)BackgroundRevealAuto.Pixel(w,b,p,c)).ToString("x8"));
            }
            for(int i=0;i<6000;i++){
                int first=unchecked((int)0xff000000)|rng.Next(0x1000000),other=unchecked((int)0xff000000)|rng.Next(0x1000000),b=unchecked((int)0xff000000)|rng.Next(0x1000000),c=unchecked((int)0xff000000)|rng.Next(0x1000000);
                var p=new BackgroundRevealAuto.Profile{SourceColor=true,Strength=.6,Exposure=.55,WhiteLow=0,WhiteHigh=255};int v=BackgroundRevealAuto.Pixel(first,b,p,c),v2=BackgroundRevealAuto.Pixel(other,b,p,c);
                double[] channels=new[]{((v>>16)&255)*((uint)v>>24)/255.0,((v>>8)&255)*((uint)v>>24)/255.0,(v&255)*((uint)v>>24)/255.0};Require(channels.Max()-channels.Min()<=20.8,"source chroma exceeded leak cap");
                foreach(int shift in new[]{16,8,0})Require(Math.Abs(((v>>shift)&255)*((uint)v>>24)/255.0-((v2>>shift)&255)*((uint)v2>>24)/255.0)<=1.001,"source hue depends on cover");
            }
            using(var source=new Bitmap(180,120)){
                Color[] palette={Color.Red,Color.Lime,Color.Blue,Color.Yellow,Color.Cyan,Color.Magenta};for(int y=0;y<120;y++)for(int x=0;x<180;x++)source.SetPixel(x,y,palette[x/30]);Bitmap firstBlack=null;
                try{foreach(int preset in new[]{0,1}){BackgroundRevealAuto.Profile p;using(var image=BackgroundRevealAuto.Compose(null,source,0,true,85,CancellationToken.None,out p,preset,true)){
                    Require(p.SourceColor&&!p.Theme,"source colors became shared tint");byte[] png=BackgroundRevealAuto.Encode(image,true,p,CancellationToken.None);Require(Codec.Parse(png).Single(c=>c.Type=="rvAO").Data[1]==3,"source profile marker");using(var decoded=Codec.Decode(png))using(var black=DualBlend.Render(decoded,false,CancellationToken.None))using(var white=DualBlend.Render(decoded,true,CancellationToken.None)){
                        for(int y=0;y<120;y++)for(int x=0;x<180;x++){var q=white.GetPixel(x,y);Require(Math.Max(q.R,Math.Max(q.G,q.B))-Math.Min(q.R,Math.Min(q.G,q.B))<=Math.Ceiling(p.ChromaLimit)+1,"actual white PNG exceeded adaptive chroma cap");}
                        for(int i=0;i<6;i++){var q=black.GetPixel(i*30+15,60);Color expected=palette[i];foreach(int shift in new[]{16,8,0})foreach(int other in new[]{16,8,0})if(((expected.ToArgb()>>shift)&255)>((expected.ToArgb()>>other)&255))Require(((q.ToArgb()>>shift)&255)-((q.ToArgb()>>other)&255)>8,"regional source hue lost");}
                        if(firstBlack==null)firstBlack=(Bitmap)black.Clone();else for(int y=0;y<120;y++)for(int x=0;x<180;x++){var a=firstBlack.GetPixel(x,y);var b=black.GetPixel(x,y);Require(Math.Abs(a.R-b.R)<=1&&Math.Abs(a.G-b.G)<=1&&Math.Abs(a.B-b.B)<=1,"palette recolored source");}
                        for(int y=0;y<120;y++)for(int x=0;x<180;x++)Require(decoded.GetPixel(x,y).ToArgb()==image.GetPixel(x,y).ToArgb(),"source PNG changed RGBA");Codec.SaveAtomic(Path.Combine(dir,"source-preset-"+preset+"-black.png"),Codec.StaticPng(black));Codec.SaveAtomic(Path.Combine(dir,"source-preset-"+preset+"-white.png"),Codec.StaticPng(white));
                    }
                    string path=Path.Combine(dir,"source-preset-"+preset+".png");Codec.SaveAtomic(path,png);DualBlend.RestorePair(path,dir,CancellationToken.None);
                }}}finally{if(firstBlack!=null)firstBlack.Dispose();}
                BackgroundRevealAuto.Profile gray;using(var image=BackgroundRevealAuto.Compose(null,source,0,false,85,CancellationToken.None,out gray,1,true)){Require(!gray.SourceColor&&!gray.Theme,"gray entered color processing");for(int y=0;y<120;y++)for(int x=0;x<180;x++){var q=image.GetPixel(x,y);Require(q.R==q.G&&q.G==q.B,"gray PNG chroma");}}
                using(var cover=BackgroundRevealAuto.CreateCover(source,0,-1,CancellationToken.None))for(int y=0;y<120;y++)for(int x=0;x<180;x++){var q=cover.GetPixel(x,y);Require(q.R==q.G&&q.G==q.B,"grayscale cover contains color");}
                bool cancelled=false;try{BackgroundRevealAuto.Compose(null,source,0,true,85,new CancellationToken(true),out gray,0,true);}catch(OperationCanceledException){cancelled=true;}Require(cancelled,"source cancellation ignored");
            }
            using(var flat=new Bitmap(72,48))using(var ramp=new Bitmap(72,48)){
                using(var g=Graphics.FromImage(flat))g.Clear(Color.Gray);for(int y=0;y<48;y++)for(int x=0;x<72;x++){int v=x*255/71;ramp.SetPixel(x,y,Color.FromArgb(v,v,v));}
                BackgroundRevealAuto.Profile low,high;using(var a=BackgroundRevealAuto.Compose(null,flat,0,true,85,CancellationToken.None,out low,0,true))using(var b=BackgroundRevealAuto.Compose(null,ramp,0,true,85,CancellationToken.None,out high,0,true)){Require(high.ChromaLimit<low.ChromaLimit,"leak budget did not adapt to source contrast");Require(low.ChromaLimit<=48&&high.ChromaLimit>=16,"invalid adaptive budget");}
                BackgroundRevealAuto.Profile zero;using(var a=BackgroundRevealAuto.Compose(null,ramp,0,true,0,CancellationToken.None,out zero,0,true))using(var black=DualBlend.Render(a,false,CancellationToken.None))for(int y=0;y<48;y++)for(int x=0;x<72;x++){var q=black.GetPixel(x,y);Require(q.R==q.G&&q.G==q.B,"zero strength contains color");}
            }
            File.WriteAllText(Path.Combine(dir,"background-source-tests.md"),"PASS: six regional source hues survive actual PNG encoding/extraction; actual white chroma <= ceil(adaptive budget)+1; 6000 random cap checks <=20.8; contrast-adaptive budget and zero strength; no global tint; changing cover palettes changes black appearances by <=1 code level; 6000 fixed-profile cover-independence pairs <=1.001; grayscale cover and PNG contain no chroma; RGBA roundtrip, extraction and cancellation. 320 vectors for JVM comparison. Color detail/saturation/brightness are adjusted; no universal ghost-free claim.");
        }

        static void AutoTests(string dir)
        {
            var rng=new Random(43199);
            using(var vectors=new StreamWriter(Path.Combine(dir,"auto-pixels.csv")))
            for(int i=0;i<640;i++){
                var p=new BackgroundRevealAuto.Profile{WhiteLow=8,WhiteHigh=233,BlackLow=12,BlackHigh=242,WhiteFloor=190,BlackCeiling=165,Strength=i%4==0?.07:0,Theme=i%4==1,TintR=-14,TintG=-3,TintB=17};
                int w=unchecked((int)0xff000000)|rng.Next(0x1000000),b=unchecked((int)0xff000000)|rng.Next(0x1000000);
                vectors.WriteLine(((uint)w).ToString("x8")+","+((uint)b).ToString("x8")+","+p.Strength.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+(p.Theme?1:0)+","+((uint)BackgroundRevealAuto.Pixel(w,b,p)).ToString("x8"));
            }
            // Under a fixed shared tint, changing the other image must not add its contours.
            foreach(bool theme in new[]{false,true})for(int i=0;i<3000;i++){
                var p=new BackgroundRevealAuto.Profile{WhiteLow=0,WhiteHigh=255,BlackLow=0,BlackHigh=255,Theme=theme,TintR=-14,TintG=-3,TintB=17};
                int w=unchecked((int)0xff000000)|rng.Next(0x1000000),b=unchecked((int)0xff000000)|rng.Next(0x1000000),other=unchecked((int)0xff000000)|rng.Next(0x1000000);
                int v=BackgroundRevealAuto.Pixel(w,b,p),changeB=BackgroundRevealAuto.Pixel(w,other,p),changeW=BackgroundRevealAuto.Pixel(other,b,p);
                foreach(int shift in new[]{16,8,0}){
                    double white=((v>>shift)&255)*((uint)v>>24)/255.0+255-((uint)v>>24),whiteOther=((changeB>>shift)&255)*((uint)changeB>>24)/255.0+255-((uint)changeB>>24);
                    double black=((v>>shift)&255)*((uint)v>>24)/255.0,blackOther=((changeW>>shift)&255)*((uint)changeW>>24)/255.0;
                    Require(Math.Abs(white-whiteOther)<=1.51,"auto white depends on dark contours");Require(Math.Abs(black-blackOther)<=1.51,"auto black depends on cover contours");
                }
            }
            using(var cover=new Bitmap(72,48))using(var dark=new Bitmap(72,48)){
                using(var g=Graphics.FromImage(cover))g.Clear(Color.Orange);
                for(int y=0;y<48;y++)for(int x=0;x<72;x++)dark.SetPixel(x,y,Color.FromArgb(x*255/71,y*255/47,220));
                foreach(bool color in new[]{true,false}){
                    BackgroundRevealAuto.Profile p;
                    using(var image=BackgroundRevealAuto.Compose(cover,dark,0,color,70,CancellationToken.None,out p)){
                        Require(color?p.Theme:!p.Theme,"automatic color tradeoff not reported");
                        byte[] bytes=BackgroundRevealAuto.Encode(image,color,p,CancellationToken.None);var chunks=Codec.Parse(bytes);
                        Require(chunks.Count(c=>c.Type=="rvAO")==1,"auto metadata missing");Require(!chunks.Any(c=>c.Type=="tEXt"||c.Type=="iTXt"||c.Type=="acTL"),"auto added text or animation");
                        using(var decoded=Codec.Decode(bytes))using(var white=DualBlend.Render(decoded,true,CancellationToken.None))using(var black=DualBlend.Render(decoded,false,CancellationToken.None)){
                            var initial=white.GetPixel(0,0);for(int y=0;y<48;y++)for(int x=0;x<72;x++){var q=white.GetPixel(x,y);Require(Math.Abs(q.R-initial.R)<=2&&Math.Abs(q.G-initial.G)<=2&&Math.Abs(q.B-initial.B)<=2,"encoded flat cover reveals dark shapes");Require(decoded.GetPixel(x,y).ToArgb()==image.GetPixel(x,y).ToArgb(),"auto PNG RGBA changed");}
                            Require(Math.Abs(black.GetPixel(0,0).R-black.GetPixel(71,47).R)>60,"hidden image lost tonal detail");var c=black.GetPixel(36,24);Require(color?c.R!=c.B:c.R==c.G&&c.G==c.B,"auto color/gray output");
                            Codec.SaveAtomic(Path.Combine(dir,color?"auto-on-white.png":"auto-gray-white.png"),Codec.StaticPng(white));Codec.SaveAtomic(Path.Combine(dir,color?"auto-on-black.png":"auto-gray-black.png"),Codec.StaticPng(black));
                        }
                        string path=Path.Combine(dir,color?"auto-color.png":"auto-gray.png");Codec.SaveAtomic(path,bytes);DualBlend.RestorePair(path,dir,CancellationToken.None);
                    }
                }
                BackgroundRevealAuto.Profile blue,red;
                using(var g=Graphics.FromImage(dark))g.Clear(Color.Blue);
                using(var image=BackgroundRevealAuto.Compose(null,dark,36,true,70,CancellationToken.None,out blue)){Require(image.Width==36&&image.Height==24&&blue.Pattern,"auto aspect/pattern");}
                using(var g=Graphics.FromImage(dark))g.Clear(Color.Red);
                using(var image=BackgroundRevealAuto.Compose(null,dark,36,true,70,CancellationToken.None,out red)){Require(red.Pattern&&red.Theme&&blue.Theme,"flat color theme");Require(red.TintR!=blue.TintR&&red.TintB!=blue.TintB,"cover palette ignores source");}
                int previous=0;for(int preset=0;preset<4;preset++){using(var image=BackgroundRevealAuto.Compose(null,dark,36,true,70,CancellationToken.None,out red,preset)){Require(red.Theme,"colored patterned cover lost tint");int tint=red.TintR<<16|red.TintG<<8|red.TintB;Require(preset==0||tint!=previous,"cover preset ignored");previous=tint;using(var decoded=Codec.Decode(BackgroundRevealAuto.Encode(image,true,red,CancellationToken.None)))using(var white=DualBlend.Render(decoded,true,CancellationToken.None)){var q=white.GetPixel(10,10);Require(q.R!=q.B,"color cover exported gray");Codec.SaveAtomic(Path.Combine(dir,"cover-preset-"+preset+".png"),Codec.StaticPng(white));}}}
                bool cancelled=false;try{BackgroundRevealAuto.Compose(null,dark,0,true,70,new CancellationToken(true),out red);}catch(OperationCanceledException){cancelled=true;}Require(cancelled,"auto cancellation ignored");
            }
            File.WriteAllText(Path.Combine(dir,"background-auto-tests.md"),"PASS: 6000 fixed-profile cover/original independence pairs (<=1.51 code-value error), actual PNG roundtrip flat-cover contour test (<=2 levels), source-based colored patterns, grayscale output, restored appearances, aspect ratios and cancellation. 640 vectors exported for JVM comparison.");
        }

        static void ManualSourceTests(string dir)
        {
            using(var dark=SelfTest.Art(144,96,true))using(var first=SelfTest.Art(144,96,false))using(var other=new Bitmap(144,96))
            {
                using(var g=Graphics.FromImage(other))g.Clear(Color.White);
                foreach(int level in new[]{0,25,75,100}){
                    BackgroundRevealAuto.Profile a,b;
                    using(var one=BackgroundRevealAuto.Compose(first,dark,0,true,85,CancellationToken.None,out a,0,true,level))
                    using(var two=BackgroundRevealAuto.Compose(other,dark,0,true,85,CancellationToken.None,out b,0,true,level))
                    using(var blackOne=DualBlend.Render(one,false,CancellationToken.None))
                    using(var blackTwo=DualBlend.Render(two,false,CancellationToken.None)){
                        Require(Math.Abs(a.Strength-level/100.0)<.00001,"manual strength ignored");
                        for(int y=0;y<dark.Height;y++)for(int x=0;x<dark.Width;x++){Color p=blackOne.GetPixel(x,y),q=blackTwo.GetPixel(x,y);Require(Math.Abs(p.R-q.R)<=1&&Math.Abs(p.G-q.G)<=1&&Math.Abs(p.B-q.B)<=1,"cover contaminated manual source");if(level==0)Require(p.R==p.G&&p.G==p.B,"zero manual strength is not grayscale");}
                        using(var restored=Codec.Decode(BackgroundRevealAuto.Encode(one,true,a,CancellationToken.None)))Require(restored.GetPixel(72,48).ToArgb()==one.GetPixel(72,48).ToArgb(),"manual PNG restoration lost pixels");
                    }
                }
            }
        }
        public static void Run(string dir)
        {
            ManualSourceTests(dir);
            SourceColorTests(dir);
            AutoTests(dir);
            var random=new Random(8173);
            using(var vectors=new StreamWriter(Path.Combine(dir,"background-pixels.csv")))for(int i=0;i<180;i++){int w=unchecked((int)0xff000000)|((i*9767+1337)&0xffffff),b=unchecked((int)0xff000000)|((i*144667+7349)&0xffffff);bool color=i%2==0;int strength=i%101;vectors.WriteLine(((uint)w).ToString("x8")+","+((uint)b).ToString("x8")+","+strength+","+(color?1:0)+","+((uint)BackgroundReveal.Pixel(w,b,color,strength)).ToString("x8"));}
            for(int i=0;i<16000;i++)
            {
                int w=unchecked((int)0xff000000)|random.Next(0x1000000),b=unchecked((int)0xff000000)|random.Next(0x1000000),v=BackgroundReveal.Pixel(w,b,false,70),a=(int)((uint)v>>24),c=v&255;
                Require(((v>>16)&255)==c&&((v>>8)&255)==c,"grayscale contains chroma");
                Require(Math.Abs(c*a/255.0-.5*Gray(b))<=1.01,"black appearance contaminated");
                Require(Math.Abs(c*a/255.0+255-a-(127.5+.5*Gray(w)))<=1.01,"white appearance contaminated");
                if(i<500){v=BackgroundReveal.Pixel(w,b,true,70);Require(Error(v,w,b,70)<=ReferenceMinimum(w,b,70)+1.6,"color fit is not the least-error solution");}
                Require(BackgroundReveal.Pixel(w,b,true,0)==BackgroundReveal.Pixel(w,b,false,100),"zero saturation differs from grayscale");
            }
            using(var vectors=new StreamWriter(Path.Combine(dir,"priority-pixels.csv")))for(int i=0;i<360;i++){int w=unchecked((int)((uint)(i*133713)*2246822519u)),b=unchecked((int)((uint)(i*914433)*3266489917u));if(i%3==0){w|=unchecked((int)0xff000000);b|=unchecked((int)0xff000000);}int strength=i%101;vectors.WriteLine(((uint)w).ToString("x8")+","+((uint)b).ToString("x8")+","+strength+",1,"+((uint)BackgroundReveal.Pixel(w,b,true,strength,true)).ToString("x8"));}
            for(int i=0;i<4000;i++)
            {
                int first=unchecked((int)0xff000000)|random.Next(0x1000000),other=unchecked((int)0xff000000)|random.Next(0x1000000),dark=unchecked((int)0xff000000)|random.Next(0x1000000),sat=i%101;
                int v=BackgroundReveal.Pixel(first,dark,true,sat,true),v2=BackgroundReveal.Pixel(other,dark,true,sat,true),a=(int)((uint)v>>24),a2=(int)((uint)v2>>24);double l=Gray(dark);
                foreach(int shift in new[]{16,8,0}){double target=.5*(l+(((dark>>shift)&255)-l)*sat/100.0),actual=((v>>shift)&255)*a/255.0,otherActual=((v2>>shift)&255)*a2/255.0;Require(Math.Abs(actual-target)<=.501,"priority black target differs");Require(Math.Abs(actual-otherActual)<=1.001,"priority cover leaked into black");}
            }
            using(var dark=new Bitmap(80,120))using(var auto=BackgroundReveal.Compose(null,dark,60,true,70,CancellationToken.None,true)){Require(auto.Width==40&&auto.Height==60,"automatic cover dimensions");var png=BackgroundReveal.Encode(auto,true,70,CancellationToken.None);Require(!Codec.Parse(png).Any(c=>c.Type.EndsWith("EXt")),"automatic cover inserted text");}
            using(var white=new Bitmap(96,96))using(var black=new Bitmap(128,80))
            {
                for(int y=0;y<white.Height;y++)for(int x=0;x<white.Width;x++)white.SetPixel(x,y,Color.FromArgb(x*255/95,y*255/95,80));
                for(int y=0;y<black.Height;y++)for(int x=0;x<black.Width;x++)black.SetPixel(x,y,Color.FromArgb(30,x*255/127,y*255/79));
                foreach(bool color in new[]{false,true})using(var result=BackgroundReveal.Compose(white,black,100,color,70,CancellationToken.None))
                {
                    Require(result.Width==100&&result.Height==62,"output aspect ratio");var png=BackgroundReveal.Encode(result,color,70,CancellationToken.None);var chunks=Codec.Parse(png);Require(!chunks.Any(c=>c.Type=="acTL"||c.Type=="tEXt"||c.Type=="iTXt"||c.Type=="zTXt"),"unexpected animation or text");Require(DualBlend.IsLegacy(png)&&DualBlend.Background(png)==0,"restoration recognition");
                    using(var decoded=Codec.Decode(png))for(int y=0;y<result.Height;y++)for(int x=0;x<result.Width;x++)Require(decoded.GetPixel(x,y).ToArgb()==result.GetPixel(x,y).ToArgb(),"RGBA changed in export");
                    string path=Path.Combine(dir,color?"color-reveal.png":"gray-reveal.png");Codec.SaveAtomic(path,png);DualBlend.RestorePair(path,dir,CancellationToken.None);DualBlend.RestorePair(path,dir,CancellationToken.None);
                    using(var w=DualBlend.Render(result,true,CancellationToken.None))using(var b=DualBlend.Render(result,false,CancellationToken.None)){Codec.SaveAtomic(Path.Combine(dir,color?"color-on-white.png":"gray-on-white.png"),Codec.StaticPng(w));Codec.SaveAtomic(Path.Combine(dir,color?"color-on-black.png":"gray-on-black.png"),Codec.StaticPng(b));if(color){bool chroma=false;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);chroma|=p.R!=p.G||p.G!=p.B;}Require(chroma,"color mode silently became grayscale");}}
                }
                bool cancelled=false;try{BackgroundReveal.Compose(white,black,1200,true,70,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}Require(cancelled,"cancellation ignored");
            }
            using(var a=new Bitmap(1,1))using(var b=new Bitmap(1,1)){a.SetPixel(0,0,Color.FromArgb(0,17,24,31));b.SetPixel(0,0,Color.FromArgb(0,111,122,133));using(var result=BackgroundReveal.Compose(a,b,0,true,70,CancellationToken.None)){Require(result.GetPixel(0,0).A==0,"transparent inputs were not flattened correctly");}}
            File.WriteAllText(Path.Combine(dir,"background-reveal-tests.md"),"PASS: 16000 grayscale pairs (<=1.01 code-value error per view), 500 independent color-search comparisons, 4000 priority cover-independence pairs, automatic cover, actual color output, RGBA PNG round trip, restoration, collision protection, letterboxing, alpha inputs and cancellation.");
        }
    }
}
