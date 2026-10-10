package com.guangyingruoshui.imagetools;
import android.content.Context;
import android.graphics.*;
import java.io.*;
import java.util.*;

final class BitmapHistory {
    final File dir;final ArrayList<File> undo=new ArrayList<>(),redo=new ArrayList<>();
    BitmapHistory(Context c){dir=new File(c.getCacheDir(),"history-"+UUID.randomUUID());}
    File save(Bitmap b,Files.Job j)throws Exception{if(!dir.isDirectory()&&!dir.mkdirs())throw new IOException("Unable to create undo cache");File file=new File(dir,UUID.randomUUID()+".png");try{Images.encode(b,file,0,j);j.check();return file;}catch(Exception e){file.delete();throw e;}}
    void push(Bitmap b,Files.Job j)throws Exception{File f=save(b,j);clear(redo);undo.add(f);trim();}
    void trim(){long bytes=0;for(File f:undo)bytes+=f.length();for(File f:redo)bytes+=f.length();while(undo.size()+redo.size()>50||bytes>256L*1024*1024){ArrayList<File> list=undo.size()>1?undo:redo.size()>1?redo:null;if(list==null)break;File f=list.remove(0);bytes-=f.length();f.delete();}}
    Bitmap move(Bitmap current,boolean forward,Files.Job j)throws Exception{ArrayList<File> from=forward?redo:undo,to=forward?undo:redo;if(from.isEmpty())return null;File f=from.get(from.size()-1);Bitmap b=Images.load(f,0,true);try{File saved=save(current,j);to.add(saved);from.remove(from.size()-1);f.delete();trim();return b;}catch(Exception e){b.recycle();throw e;}}
    static void clear(ArrayList<File> files){for(File f:files)f.delete();files.clear();}
    void clear(){clear(undo);clear(redo);dir.delete();}
}
