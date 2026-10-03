using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

namespace QQImageSwitch
{
    static class BatchTests
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static bool PixelsEqual(Bitmap a,Bitmap b)
        {
            if(a.Size!=b.Size)return false;
            for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb())return false;
            return true;
        }
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            string shared=Path.Combine(dir,"batch-cover.png"),custom=Path.Combine(dir,"batch-custom-cover.png"),original=Path.Combine(dir,"batch-original.png");
            using(var a=SelfTest.Art(240,160,false))using(var b=SelfTest.Art(240,160,true))
            {
                Codec.SaveAtomic(shared,Codec.StaticPng(a));Codec.SaveAtomic(custom,Codec.StaticPng(b));Codec.SaveAtomic(original,Codec.StaticPng(b));
                var items=new List<BatchItem>{new BatchItem {Path=original,Name="相同文件名.png"},
                    new BatchItem {Path=original,Name="相同文件名.png",OwnCover=true,CoverPath=custom,CoverName="独立封面"},
                    new BatchItem {Path=original,Name="最后一张.png"}};
                string folder=Path.Combine(dir,"batch-export");
                BatchResult result=Batch.Export(items,a,folder,0,true,CancellationToken.None,null);
                Check(result.Files.Count==3&&result.Errors.Count==0,"Batch export did not export all pairs");
                File.WriteAllLines(Path.Combine(dir,"batch-manifest.txt"),result.Files);
                for(int i=0;i<3;i++)
                {
                    using(var restored=Codec.Decode(Codec.Restore(File.ReadAllBytes(result.Files[i]))))Check(PixelsEqual(restored,b),"Batch changed hidden image pixels");
                    using(var front=Codec.Decode(File.ReadAllBytes(result.Files[i])))
                    {
                        Bitmap expectedBase=i==1?b:a;
                        bool changed=false;
                        for(int y=0;y<front.Height;y++)for(int x=0;x<front.Width;x++)
                        {
                            bool different=front.GetPixel(x,y).ToArgb()!=expectedBase.GetPixel(x,y).ToArgb();
                            if(different){Check(x>=front.Width-30&&y>=front.Height-30,"Number stamp changed pixels outside bottom-right corner");changed=true;}
                        }
                        Check(changed,"Number was not stamped on cover");
                    }
                }
                // Independently check that different sequence numbers yield different cover pixels.
                using(var first=Codec.Decode(File.ReadAllBytes(result.Files[0])))using(var third=Codec.Decode(File.ReadAllBytes(result.Files[2])))
                    Check(!PixelsEqual(first,third),"Different sequence numbers produced identical covers");
                string noNumbers=Path.Combine(dir,"batch-no-numbers");
                var plain=Batch.Export(items,a,noNumbers,0,false,CancellationToken.None,null);
                File.WriteAllLines(Path.Combine(dir,"batch-plain-manifest.txt"),plain.Files);
                using(var first=Codec.Decode(File.ReadAllBytes(plain.Files[0])))using(var second=Codec.Decode(File.ReadAllBytes(plain.Files[1])))
                {Check(PixelsEqual(first,a),"Shared cover mismatch without numbers");Check(PixelsEqual(second,b),"Per-image custom cover was lost");}
                byte[] firstBytes=File.ReadAllBytes(result.Files[0]);
                var repeated=Batch.Export(items,a,folder,0,true,CancellationToken.None,null);
                Check(repeated.Files[0]!=result.Files[0],"Repeated export overwrote an existing output");
                Check(Convert.ToBase64String(firstBytes)==Convert.ToBase64String(File.ReadAllBytes(result.Files[0])),"Existing file was modified");
                items[1].CoverPath=Path.Combine(dir,"missing-cover.png");
                var partial=Batch.Export(items,a,Path.Combine(dir,"batch-partial"),0,true,CancellationToken.None,null);
                Check(partial.Files.Count==2&&partial.Errors.Count==1,"Missing custom cover must fail only that pair");
                var cancellation=new CancellationTokenSource();cancellation.Cancel();
                var stopped=Batch.Export(items,a,Path.Combine(dir,"batch-cancelled"),0,true,cancellation.Token,null);
                Check(stopped.Cancelled&&stopped.Files.Count==0,"Cancellation ignored");cancellation.Dispose();
                using(var tiny=new Bitmap(1,1))Batch.StampNumber(tiny,1);
                using(var longNumber=new Bitmap(240,160))Batch.StampNumber(longNumber,12345);
                Check(Batch.SequenceLabel(1)=="①"&&Batch.SequenceLabel(21)=="㉑"&&Batch.SequenceLabel(51)=="51","Sequence labels failed");
            }
            File.WriteAllText(Path.Combine(dir,"batch-tests.txt"),"PASS: shared and per-image covers, sequence numbers on exported bottom-right, hidden pixels unchanged, no-number option, collision handling, partial failures, cancellation, tiny image and long number.");
        }
    }
}
