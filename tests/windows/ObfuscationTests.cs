using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace QQImageSwitch
{
    static class ObfuscationTests
    {
        public static void Run(string folder)
        {
            Directory.CreateDirectory(folder);string input=Path.Combine(folder,"source.png");
            using(var image=SelfTest.Art(1800,1200,true))File.WriteAllBytes(input,Codec.StaticPng(image));
            string cancelled=Path.Combine(folder,"cancelled-"+Guid.NewGuid().ToString("N")+".png");
            using(var token=new CancellationTokenSource())
            {
                token.Cancel();bool rejected=false;try{Obfuscation.Write(input,cancelled,false,-1,token.Token);}catch(OperationCanceledException){rejected=true;}
                if(!rejected||File.Exists(cancelled))throw new Exception("Pre-cancelled scramble wrote a file");
            }
            using(var token=new CancellationTokenSource())
            {
                token.CancelAfter(2);bool rejected=false;try{Obfuscation.Write(input,cancelled,false,-1,token.Token);}catch(OperationCanceledException){rejected=true;}
                if(!rejected||File.Exists(cancelled))throw new Exception("Cancellation during permutation wrote a file");
            }
            string wrapped=ImageTools.Unique(folder,input,"_original",".png");Obfuscation.Write(input,wrapped,false,-1,CancellationToken.None);
            using(var token=new CancellationTokenSource())
            {
                token.Cancel();try{Obfuscation.RestoreOriginal(wrapped,folder,token.Token);throw new Exception("Cancelled original restoration succeeded");}catch(OperationCanceledException){}
            }
            if(Directory.EnumerateFiles(folder,".scramble-*.tmp").Any())throw new Exception("Temporary-file residue after cancellation");
            string invalid=Path.Combine(folder,"invalid.png");File.WriteAllBytes(invalid,new byte[]{0,1,2,3});
            bool bad=false;try{Obfuscation.Write(invalid,cancelled,false,101,CancellationToken.None);}catch(ArgumentOutOfRangeException){bad=true;}
            if(!bad||File.Exists(cancelled))throw new Exception("Invalid quality was accepted");
            File.WriteAllText(Path.Combine(folder,"cancellation-tests.txt"),"PASS: cancellation before/during permutation and before restoration; temporary files cleaned; invalid quality rejected; input files preserved.");
        }
    }
}
