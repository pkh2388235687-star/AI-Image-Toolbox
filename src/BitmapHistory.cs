using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace QQImageSwitch
{
    public sealed class BitmapHistory : IDisposable
    {
        readonly string directory=Path.Combine(Path.GetTempPath(),"AI-Image-Editing-Tools","history-"+Guid.NewGuid().ToString("N"));
        readonly List<string> undo=new List<string>(),redo=new List<string>();public int Count{get{return undo.Count;}}public int RedoCount{get{return redo.Count;}}
        string Save(Bitmap b){Directory.CreateDirectory(directory);string p=Path.Combine(directory,Guid.NewGuid().ToString("N")+".png");try{Codec.SaveAtomic(p,Codec.StaticPng(b));return p;}catch{if(File.Exists(p))File.Delete(p);throw;}}
        public void Push(Bitmap b){string p=Save(b);Delete(redo);undo.Add(p);Trim();}
        void Trim(){while(undo.Count+redo.Count>50||undo.Concat(redo).Sum(p=>new FileInfo(p).Length)>256L*1024*1024){var list=undo.Count>1?undo:redo.Count>1?redo:null;if(list==null)break;File.Delete(list[0]);list.RemoveAt(0);}}
        public Bitmap Move(Bitmap current,bool forward){var from=forward?redo:undo;var to=forward?undo:redo;if(from.Count==0)return null;string p=from[from.Count-1];Bitmap b=Codec.Decode(File.ReadAllBytes(p));try{string saved=Save(current);to.Add(saved);from.RemoveAt(from.Count-1);File.Delete(p);Trim();return b;}catch{b.Dispose();throw;}}
        static void Delete(List<string> files){foreach(string p in files)try{File.Delete(p);}catch(IOException){}files.Clear();}
        public void Clear(){Delete(undo);Delete(redo);try{if(Directory.Exists(directory))Directory.Delete(directory);}catch(IOException){} }
        public void Dispose(){Clear();}
    }
}
