package com.guangyingruoshui.imagetools;

import android.content.*;
import android.database.*;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.*;

public final class ShareProvider extends ContentProvider {
    public boolean onCreate(){return true;}
    File resolve(Uri uri)throws FileNotFoundException{try{if(!uri.getAuthority().equals("com.guangyingruoshui.imagetools.files")||uri.getPathSegments().size()!=1)throw new IOException();File dir=new File(getContext().getCacheDir(),"share").getCanonicalFile();File file=new File(dir,Files.name(uri.getLastPathSegment())).getCanonicalFile();if(!file.getParentFile().equals(dir)||!file.isFile())throw new IOException();return file;}catch(Exception e){throw new FileNotFoundException("Invalid shared file");}}
    public ParcelFileDescriptor openFile(Uri uri,String mode)throws FileNotFoundException{if(!"r".equals(mode))throw new FileNotFoundException("Read only");return ParcelFileDescriptor.open(resolve(uri),ParcelFileDescriptor.MODE_READ_ONLY);}
    public String getType(Uri uri){String name=uri.getLastPathSegment();return Storage.mime(name==null?"":name);}
    public Cursor query(Uri uri,String[] projection,String selection,String[] args,String sort){try{File f=resolve(uri);String[] cols=projection==null?new String[]{OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE}:projection;MatrixCursor c=new MatrixCursor(cols);Object[] row=new Object[cols.length];for(int i=0;i<cols.length;i++){if(cols[i].equals(OpenableColumns.DISPLAY_NAME))row[i]=f.getName();else if(cols[i].equals(OpenableColumns.SIZE))row[i]=f.length();}c.addRow(row);return c;}catch(Exception e){return null;}}
    public Uri insert(Uri uri,ContentValues v){throw new UnsupportedOperationException("Read only");}
    public int delete(Uri uri,String s,String[] a){throw new UnsupportedOperationException("Read only");}
    public int update(Uri uri,ContentValues v,String s,String[] a){throw new UnsupportedOperationException("Read only");}
}
