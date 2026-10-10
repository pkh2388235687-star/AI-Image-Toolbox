using System;using System.Drawing;using System.IO;using System.Linq;using System.Threading;
namespace QQImageSwitch {
    static class DualBlendTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Legacy dual: "+message);}
        internal static readonly string[] Samples={"iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAQBBbnGecQAAACtJREFUeJxj1AxJydbnkmLW5ZJg1uGSZGK6r/zn7nd1jrvcmsJ3pbWl7gEAhvsJiQIsXQUAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBATBBsTeoggAAACtJREFUeJxjDCxpzFbnFGVW4RBhVuYQYWJ6Kvv37g959rucivx3xZVE7wEAjpMJmuuBZh0AAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAYBBVfIGOgAAAChJREFUeJxjnLJqU7YEKy+zGCsPCDMxfRD8d/e3MMtdNhHOu3yivPcAm0EJuBoYAuoAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAgBBbDcgKAAAACtJREFUeJxj9PX1bZeRkWGRlpZmkZCQYGH6////FR4enqvi4uJXFBUVrwIAjS0Jhoe78boAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAjBBs3EW2wAAAChJREFUeJxjzM/PbxcTE2MBYRERERamf//+XeHi4roqJCR0RUpK6ioAlIYJkon2KxsAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAABGR1QUwBAoBBV7S4YwAAACtJREFUeJxjXL58eTsvLy8LHx8fCw8PDwvT////r7CxsV3l5ua+IiAgcBUAoS4Js9Gzqw8AAAAASUVORK5CYII="};
        static readonly string[] White={"iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJklEQVR4nGNcun3ffyFmTgYhJk4GYWZuBsadNy/9B3GEmLgYQBIAnUcHNDyrTu4AAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJklEQVR4nGPcdvTUf34mDgY+JnYGfmZOBsZjD24DBdiBAhwMIBoAolIHWh74f1AAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJElEQVR4nGO8dPv+f05GFgZOJlYGTkZWBsY7b15CBBhBAiwMAKprB5Y3wxU9AAAAAElFTkSuQmCC",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAHUlEQVR4nGNcuHDhfx4eHgZeXl4GEM24f/9+FAEAlo4G/1VxBk0AAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAIklEQVR4nGPcvHnzf05OTgZubm4GLi4uBsbTp0//BzFgGACc0wcvy8tnLAAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAHUlEQVR4nGO8cOHCfzY2NgZ2dnYGEM14//59FAEAp0QHgJP9t1YAAAAASUVORK5CYII="},Dark={"iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAI0lEQVR4nGMUVNb6L8rGwyDKCsK8DEwf/39k+PgPhD8xgNgAnIENWPGzxGIAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJUlEQVR4nGO08w36L8jCxSDIysUgAKSZvvz7wvDlPxAD6a9ADACrCw3XxDsA9QAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAIUlEQVR4nGPsmDT1PzcTGwMXMxsDNxAz/fz/k+EHEMNoAMNpDq76ARFRAAAAAElFTkSuQmCC",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAIklEQVR4nGPU1NT8LyAgwCAoKMgAopk+ffrE8PnzZwYYDQChsg1vTBZHnQAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAI0lEQVR4nGMMDAz8z8vLy8DHx8cAopm+fv3K8O3bNwYQDcIAr2UN7ZfRHSMAAAAASUVORK5CYII=",
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAACCAYAAAB/qH1jAAAAJklEQVR4nGOcOnXqf3Z2dgZOTk4GDg4OBqafP38y/Pr1iwFEgzAAxgkOuaoU1IEAAAAASUVORK5CYII="};
        static void Same(Bitmap actual,byte[] expected){using(var b=Codec.Decode(expected)){Check(actual.Size==b.Size,"dimensions changed");for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)Check(actual.GetPixel(x,y)==b.GetPixel(x,y),"appearance pixels changed");}}
        public static void Run(string directory){
            string folder=Path.Combine(directory,"legacy-dual");Directory.CreateDirectory(folder);
            for(int i=0;i<Samples.Length;i++){
                byte[] bytes=Convert.FromBase64String(Samples[i]);int dark=new[]{0,48,128}[i%3];string path=Path.Combine(folder,"old-"+i+".png");File.WriteAllBytes(path,bytes);
                Check(DualBlend.IsLegacy(bytes)&&DualBlend.Background(bytes)==dark&&DualBlend.Background(path)==dark,"old profile not recognized");
                using(var white=DualBlend.DecodeView(bytes,false,CancellationToken.None))Same(white,Convert.FromBase64String(White[i]));
                using(var hidden=DualBlend.DecodeView(bytes,true,CancellationToken.None))Same(hidden,Convert.FromBase64String(Dark[i]));
                string output=Path.Combine(folder,"restore-"+i);Directory.CreateDirectory(output);DualBlend.RestorePair(path,output,CancellationToken.None);DualBlend.RestorePair(path,output,CancellationToken.None);
                Check(Directory.GetFiles(output).Length==4,"collision protection failed");
                foreach(string file in Directory.GetFiles(output))using(var actual=Codec.Load(file))Same(actual,Convert.FromBase64String(file.Contains("白底")?White[i]:Dark[i]));
                byte[] bad=(byte[])bytes.Clone();bad[43]^=1;bool rejected=false;try{DualBlend.IsLegacy(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"bad CRC accepted");
            }
            byte[] sample=Convert.FromBase64String(Samples[0]);byte[] noProfile=sample.Take(33).Concat(sample.Skip(49)).ToArray();Check(DualBlend.IsLegacy(noProfile)&&DualBlend.Background(noProfile)==0,"unprofiled old PNG failed");
            using(var view=DualBlend.DecodeView(noProfile,true,CancellationToken.None))Same(view,Convert.FromBase64String(Dark[0]));
            using(var a=Codec.Decode(Convert.FromBase64String(White[0])))using(var b=Codec.Decode(Convert.FromBase64String(Dark[0]))){
                byte[] apng=Codec.Encode(a,b);Check(!DualBlend.IsLegacy(apng),"dynamic PNG misidentified");
                using(var restored=DualBlend.DecodeView(apng,true,CancellationToken.None))Same(restored,Codec.StaticPng(b));
                using(var cover=DualBlend.DecodeView(apng,false,CancellationToken.None))Same(cover,Codec.StaticPng(a));
                bool rejected=false;try{using(var result=DualBlend.DecodeView(Codec.StaticPng(a),true,CancellationToken.None)){} }catch(InvalidDataException){rejected=true;}Check(rejected,"opaque PNG accepted as old transparent dual");
                var cancel=new CancellationTokenSource();cancel.Cancel();bool stopped=false;try{using(var result=DualBlend.Extract(a,false,cancel.Token)){} }catch(OperationCanceledException){stopped=true;}finally{cancel.Dispose();}Check(stopped,"cancellation ignored");
            }
            string prefsPath=Path.Combine(folder,"old-settings");File.WriteAllText(prefsPath+".options","dualmode=2\ndualbackground=48\ndualcolor=65\nlanguage=en\nresolution=3");var prefs=new ToolPreferences(prefsPath);Check(prefs.Language=="en"&&prefs.Resolution==3,"unrelated old settings lost");prefs.Save();Check(!File.ReadAllText(prefsPath+".options").Contains("dualmode="),"removed setting persisted");
        }
    }
}
