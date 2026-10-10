package com.guangyingruoshui.imagetools;

import android.app.*;
import android.graphics.ImageFormat;
import android.hardware.Camera;
import android.os.*;
import android.view.*;
import android.widget.*;
import java.util.*;
import java.util.concurrent.*;
import com.google.zxing.*;

@SuppressWarnings("deprecation")
final class QrCamera implements SurfaceHolder.Callback {
    interface Found {void accept(String value);}
    final MainActivity a;final Found found;
    Camera camera;Dialog dialog;SurfaceView surface;TextView hint;Spinner selector;
    volatile boolean closed,decoding;volatile int generation;long last,lastFocus;boolean focusing,surfaceReady;
    int selected,width,height,format;final ArrayList<Integer> ids=new ArrayList<>();
    final ExecutorService worker=Executors.newSingleThreadExecutor();
    QrCamera(MainActivity activity,Found found){a=activity;this.found=found;}
    void show(){
        dialog=new Dialog(a);dialog.setTitle(a.t("相机扫描","Camera scan"));
        LinearLayout body=a.column();hint=a.label(a.t("对准二维码；识别后返回网址，不自动打开。","Aim at a QR code. The URL is returned without opening it."),14,false);body.addView(hint);
        ArrayList<String> names=new ArrayList<>();
        try{
            Camera.CameraInfo info=new Camera.CameraInfo();
            int count=Camera.getNumberOfCameras();
            for(int pass=0;pass<2;pass++)for(int i=0;i<count;i++){
                Camera.getCameraInfo(i,info);boolean back=info.facing==Camera.CameraInfo.CAMERA_FACING_BACK;
                if(back!=(pass==0))continue;ids.add(i);names.add(a.t(back?"后置相机 ":"前置/其他相机 ",back?"Back camera ":"Front/other camera ")+(i+1));
            }
        }catch(Exception ex){hint.setText(a.t("无法列出相机，请检查设备和权限，或导入图片扫描。","Unable to list cameras. Check device/permission or import an image."));}
        selector=new Spinner(a);selector.setAdapter(new ArrayAdapter<String>(a,android.R.layout.simple_spinner_dropdown_item,names));body.addView(selector);
        selector.setOnItemSelectedListener(new android.widget.AdapterView.OnItemSelectedListener(){public void onNothingSelected(android.widget.AdapterView<?> parent){}public void onItemSelected(android.widget.AdapterView<?> parent,View view,int position,long id){if(selected!=position){selected=position;restart();}}});
        surface=new SurfaceView(a);body.addView(surface,new LinearLayout.LayoutParams(-1,a.dp(320)));
        a.buttons(body,a.button(a.t("重新连接","Reconnect"),this::restart),a.button(a.t("关闭相机","Close camera"),this::close));
        if(ids.isEmpty())hint.setText(a.t("未检测到可用相机，可导入图片扫描。","No compatible camera detected. Import an image instead."));
        dialog.setContentView(body);dialog.setOnDismissListener(d->release());surface.getHolder().addCallback(this);dialog.show();dialog.getWindow().setLayout(-1,-2);
    }
    public void surfaceCreated(SurfaceHolder holder){surfaceReady=true;restart();}
    public void surfaceChanged(SurfaceHolder holder,int f,int w,int h){if(camera!=null)try{orient(ids.get(selected));}catch(Exception ignored){}}
    public void surfaceDestroyed(SurfaceHolder holder){surfaceReady=false;releaseCamera();}
    void restart(){
        releaseCamera();if(closed||!surfaceReady||ids.isEmpty())return;
        try{
            int id=ids.get(selected);camera=Camera.open(id);if(camera==null)throw new IllegalStateException("No camera");
            configure();orient(id);camera.setPreviewDisplay(surface.getHolder());
            final int epoch=generation,frameWidth=width,frameHeight=height,frameFormat=format;
            camera.setPreviewCallback((data,cam)->{
                long now=SystemClock.elapsedRealtime();if(closed||epoch!=generation||decoding||now-last<300)return;
                last=now;byte[] luma;
                try{luma=plane(data,frameWidth,frameHeight,frameFormat==ImageFormat.YV12?((frameWidth+15)/16)*16:frameWidth);}catch(Exception ex){return;}
                decoding=true;
                try{worker.execute(()->{
                    String value=null;
                    try{
                        value=QrService.decode(new PlanarYUVLuminanceSource(luma,frameWidth,frameHeight,0,0,frameWidth,frameHeight,false));
                        if(value==null){byte[] rotated=new byte[luma.length];for(int y=0;y<frameHeight;y++)for(int x=0;x<frameWidth;x++)rotated[x*frameHeight+frameHeight-1-y]=luma[y*frameWidth+x];value=QrService.decode(new PlanarYUVLuminanceSource(rotated,frameHeight,frameWidth,0,0,frameHeight,frameWidth,false));}
                    }catch(Exception ignored){}finally{decoding=false;}
                    final String result=value;if(result!=null)a.ui.post(()->{if(!closed&&epoch==generation){found.accept(result);close();}});
                });}catch(RejectedExecutionException ex){decoding=false;}
                if(!focusing&&now-lastFocus>1800)try{if(Camera.Parameters.FOCUS_MODE_AUTO.equals(cam.getParameters().getFocusMode())){lastFocus=now;focusing=true;cam.autoFocus((ok,c)->{if(epoch==generation)focusing=false;});}}catch(Exception ex){focusing=false;}
            });
            camera.setErrorCallback((code,cam)->{if(!closed){releaseCamera();hint.setText(a.t("相机设备异常，请重新连接或选择其他相机。代码：","Camera device error. Reconnect or select another camera. Code: ")+code);}});
            camera.startPreview();last=lastFocus=0;
            hint.setText(a.t("对准二维码；可切换相机或重新连接，识别后不自动打开。","Aim at a QR code. Switch cameras or reconnect if needed. No automatic website launch."));
        }catch(SecurityException ex){releaseCamera();hint.setText(a.t("相机权限被拒绝，请在系统设置中允许相机，或导入图片扫描。","Camera permission denied. Allow camera access in system settings or import an image."));}
        catch(Exception ex){releaseCamera();hint.setText(a.t("相机启动失败，可能是格式、设备或驱动限制；请重新连接或选择其他相机，也可导入图片。","Camera start failed: format, device or driver may be unavailable. Reconnect, choose another camera or import an image."));}
    }
    void configure(){
        Camera.Parameters initial=camera.getParameters();
        List<Integer> formats=initial.getSupportedPreviewFormats();int desired=formats!=null&&formats.contains(ImageFormat.NV21)?ImageFormat.NV21:formats!=null&&formats.contains(ImageFormat.YV12)?ImageFormat.YV12:initial.getPreviewFormat();
        if(desired!=ImageFormat.NV21&&desired!=ImageFormat.YV12)throw new IllegalStateException("Unsupported preview format");
        List<Camera.Size> sizes=initial.getSupportedPreviewSizes();ArrayList<Camera.Size> candidates=new ArrayList<>();if(sizes!=null)candidates.addAll(sizes);if(candidates.isEmpty())candidates.add(initial.getPreviewSize());
        Collections.sort(candidates,(x,y)->Double.compare(score(x.width,x.height),score(y.width,y.height)));
        boolean configured=false;
        for(Camera.Size size:candidates){if(size==null)continue;for(int focusAttempt=0;focusAttempt<2&&!configured;focusAttempt++)try{
            Camera.Parameters p=camera.getParameters();p.setPreviewSize(size.width,size.height);p.setPreviewFormat(desired);
            List<String> focus=p.getSupportedFocusModes();if(focusAttempt==0&&focus!=null){if(focus.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE))p.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);else if(focus.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO))p.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO);else if(focus.contains(Camera.Parameters.FOCUS_MODE_AUTO))p.setFocusMode(Camera.Parameters.FOCUS_MODE_AUTO);}
            camera.setParameters(p);configured=true;
        }catch(RuntimeException ignored){}if(configured)break;}
        if(!configured)throw new IllegalStateException("Camera rejected advertised formats");
        Camera.Parameters actual=camera.getParameters();Camera.Size size=actual.getPreviewSize();width=size.width;height=size.height;format=actual.getPreviewFormat();
        if(width<=0||height<=0||(long)width*height>16*1024*1024||(format!=ImageFormat.NV21&&format!=ImageFormat.YV12))throw new IllegalStateException("Unsupported preview output");
    }
    static double score(int width,int height){return Math.abs(Math.log((double)width*height/(1280*720)))+(Math.max(width,height)>1280?8:0)+(Math.min(width,height)<240?4:0);}
    static byte[] plane(byte[] input,int width,int height,int stride){
        if(input==null||width<=0||height<=0||stride<width||(long)width*height>16*1024*1024||(long)(height-1)*stride+width>input.length)throw new IllegalArgumentException("Invalid luminance frame");
        byte[] output=new byte[width*height];for(int y=0;y<height;y++)System.arraycopy(input,y*stride,output,y*width,width);return output;
    }
    void orient(int id){Camera.CameraInfo info=new Camera.CameraInfo();Camera.getCameraInfo(id,info);int rotation=a.getWindowManager().getDefaultDisplay().getRotation()*90;int angle=info.facing==Camera.CameraInfo.CAMERA_FACING_FRONT?(360-(info.orientation+rotation)%360)%360:(info.orientation-rotation+360)%360;camera.setDisplayOrientation(angle);}
    void releaseCamera(){
        generation++;focusing=false;Camera previous=camera;camera=null;
        if(previous!=null){try{previous.setPreviewCallback(null);previous.setErrorCallback(null);}catch(Exception ignored){}try{previous.stopPreview();}catch(Exception ignored){}finally{try{previous.release();}catch(Exception ignored){}}}
    }
    void release(){closed=true;releaseCamera();worker.shutdownNow();}
    void close(){release();if(dialog!=null&&dialog.isShowing())dialog.dismiss();}
}

