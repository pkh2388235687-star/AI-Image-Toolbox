package com.guangyingruoshui.imagetools;

import java.util.Arrays;

// Pure frame-layout and compositing checks also run on a host JVM.
final class CameraQrMathTests {
    static void require(boolean value,String message){if(!value)throw new AssertionError(message);}
    static void run(){
        byte[] rows={1,2,3,9,9,4,5,6};
        require(Arrays.equals(QrCamera.plane(rows,3,2,5),new byte[]{1,2,3,4,5,6}),"Padded luminance rows");
        byte[] aligned=new byte[19];aligned[0]=1;aligned[1]=2;aligned[2]=3;aligned[16]=4;aligned[17]=5;aligned[18]=6;
        require(Arrays.equals(QrCamera.plane(aligned,3,2,16),new byte[]{1,2,3,4,5,6}),"YV12 aligned rows");
        boolean refused=false;try{QrCamera.plane(new byte[17],3,2,16);}catch(IllegalArgumentException e){refused=true;}
        require(refused,"Truncated frame must be rejected");
        require(QrCamera.score(1280,720)<QrCamera.score(3840,2160),"Prefer bounded camera size");
        BackgroundRevealAuto.Profile p=new BackgroundRevealAuto.Profile();p.whiteLow=0;p.whiteHigh=255;p.whiteFloor=190;
        for(int gray=0;gray<256;gray++){
            int cover=0xff000000|gray<<16|gray<<8|gray;
            int ink=QrService.optimizePixel(cover,true,p),paper=QrService.optimizePixel(cover,false,p);
            for(int bg:new int[]{245,250,255}){
                int a=ink>>>24,b=paper>>>24;
                int i=((ink&255)*a+bg*(255-a)+127)/255,s=((paper&255)*b+bg*(255-b)+127)/255;
                require(Math.abs(i-s)<=(bg==250?1:3),"Near-white QR contrast");
            }
        }
    }
}
