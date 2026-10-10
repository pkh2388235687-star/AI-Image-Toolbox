using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace QQImageSwitch
{
    static class CameraCaptureTests
    {
        static void Check(bool value,string message){if(!value)throw new Exception("Camera: "+message);}
        public static void Run()
        {
            Check(CameraCapture.DeviceRank("ASUS IR camera")>CameraCapture.DeviceRank("USB FHD webcam"),"IR camera preferred over color");
            Check(CameraCapture.SizeScore(640,480)<CameraCapture.SizeScore(3840,2160),"4K mode preferred for QR preview");
            IntPtr memory=Marshal.AllocHGlobal(24);
            try
            {
                byte[] bytes={0,0,255,0,0,255,0,0,99,99,99,99,255,0,0,0,255,255,255,0,99,99,99,99};Marshal.Copy(bytes,0,memory,bytes.Length);
                using(var top=CameraCapture.CopyRgb(memory,2,2,12))using(var bottom=CameraCapture.CopyRgb(IntPtr.Add(memory,12),2,2,-12))
                {
                    Check(top.GetPixel(0,0).ToArgb()==Color.Red.ToArgb()&&top.GetPixel(0,1).ToArgb()==Color.Blue.ToArgb(),"padded RGB rows");
                    Check(bottom.GetPixel(0,0).ToArgb()==Color.Blue.ToArgb()&&bottom.GetPixel(0,1).ToArgb()==Color.Red.ToArgb(),"negative stride");
                }
                bool bad=false;try{CameraCapture.CopyRgb(memory,2,2,4);}catch(InvalidOperationException){bad=true;}Check(bad,"invalid pitch accepted");
            }
            finally{Marshal.FreeHGlobal(memory);}
            var denied=new COMException("denied",unchecked((int)0x80070005));Check(QrCamera.Failure(denied).Contains("80070005"),"lost error code");
            Check(QrCamera.Failure(new InvalidOperationException()).Length>0,"generic camera failure missing");
        }
    }
}

