using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace QQImageSwitch
{
    [ComVisible(true),Guid("deec8d99-fa1d-4d82-84c2-2c8969944867"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICameraReaderCallback
    {
        [PreserveSig]int OnReadSample(int status,uint stream,uint flags,long time,IntPtr sample);
        [PreserveSig]int OnFlush(uint stream);
        [PreserveSig]int OnEvent(uint stream,IntPtr mediaEvent);
    }

    // The system Source Reader converts negotiated camera video to RGB32.
    // No driver DLL, recording file, audio stream or network service is used.
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
    public sealed class CameraCapture : IDisposable,ICameraReaderCallback
    {
        public sealed class Device
        {
            public string Name,Link;
            public override string ToString(){return Name;}
        }
        readonly object gate=new object();
        IntPtr source,reader;
        Bitmap latest;
        bool stopped,pending;
        Exception error;
        int width,height,stride;
        Task start,cleanup;
        public bool Ready {get{lock(gate)return reader!=IntPtr.Zero&&!stopped;}}
        public Exception Error {get{lock(gate)return error;}}
        public Task Completion {get{lock(gate)return cleanup??Task.FromResult(0);}}
        public CameraCapture(Device device)
        {
            Native.Acquire();
            start=Task.Run(()=>Open(device));
        }
        public static Device[] Devices()
        {
            Native.Acquire();IntPtr attributes=IntPtr.Zero,array=IntPtr.Zero;uint count=0;bool com=false;
            try
            {
                com=Native.InitializeCom();
                Native.Check(Native.MFCreateAttributes(out attributes,1));
                Native.SetGuid(attributes,Native.SourceType,Native.VideoCapture);
                Native.Check(Native.MFEnumDeviceSources(attributes,out array,out count));
                var result=new List<Device>();
                for(uint i=0;i<count;i++)
                {
                    IntPtr item=Marshal.ReadIntPtr(array,checked((int)i*IntPtr.Size));
                    result.Add(new Device{Name=Native.String(item,Native.FriendlyName),Link=Native.String(item,Native.SymbolicLink)});
                }
                result.Sort((a,b)=>{int rank=DeviceRank(a.Name).CompareTo(DeviceRank(b.Name));return rank!=0?rank:string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase);});
                return result.ToArray();
            }
            finally
            {
                if(array!=IntPtr.Zero){for(uint i=0;i<count;i++){IntPtr item=Marshal.ReadIntPtr(array,checked((int)i*IntPtr.Size));if(item!=IntPtr.Zero)Marshal.Release(item);}Marshal.FreeCoTaskMem(array);}
                Native.Release(ref attributes);Native.ReleaseRuntime();if(com)Native.UninitializeCom();
            }
        }
        internal static int DeviceRank(string name)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(name??"",@"(?i)(\bIR\b|infrared|depth|红外|深度)")?1:0;
        }
        void Open(Device device)
        {
            IntPtr attrs=IntPtr.Zero,s=IntPtr.Zero,r=IntPtr.Zero,output=IntPtr.Zero,callback=IntPtr.Zero;bool com=false;
            try
            {
                com=Native.InitializeCom();
                lock(gate)if(stopped)return;
                Native.Check(Native.MFCreateAttributes(out attrs,3));
                Native.SetGuid(attrs,Native.SourceType,Native.VideoCapture);
                Native.Check(Native.Call<Native.SetText>(attrs,25)(attrs,ref Native.SymbolicLink,device.Link));
                Native.Check(Native.MFCreateDeviceSource(attrs,out s));
                callback=Marshal.GetComInterfaceForObject(this,typeof(ICameraReaderCallback));
                Native.Release(ref attrs);Native.Check(Native.MFCreateAttributes(out attrs,2));
                Native.Check(Native.Call<Native.SetObject>(attrs,27)(attrs,ref Native.AsyncCallback,callback));
                Native.SetUInt(attrs,Native.VideoProcessing,1);
                Native.Check(Native.MFCreateSourceReaderFromMediaSource(s,attrs,out r));
                // Capture video only. A microphone is never selected.
                Native.Check(Native.Call<Native.SelectStream>(r,4)(r,Native.AllStreams,0));
                Native.Check(Native.Call<Native.SelectStream>(r,4)(r,Native.VideoStream,1));
                SelectNativeSize(r);
                Native.Check(Native.MFCreateMediaType(out output));
                Native.SetGuid(output,Native.MajorType,Native.Video);
                Native.SetGuid(output,Native.Subtype,Native.Rgb32);
                Native.Check(Native.Call<Native.SetType>(r,7)(r,Native.VideoStream,IntPtr.Zero,output));
                lock(gate)
                {
                    if(stopped)return;
                    ReadFormat(r);source=s;reader=r;s=r=IntPtr.Zero;
                }
                Request();
            }
            catch(Exception ex){lock(gate)if(!stopped)error=ex;}
            finally
            {
                Native.Release(ref output);Native.Release(ref attrs);if(callback!=IntPtr.Zero)Marshal.Release(callback);
                Native.Release(ref r);Native.Shutdown(ref s);if(com)Native.UninitializeCom();
            }
        }
        static void SelectNativeSize(IntPtr reader)
        {
            // Choose an advertised mode close to 640x480; never force an unsupported size.
            var candidates=new List<Tuple<double,uint>>();
            for(uint i=0;i<512;i++)
            {
                IntPtr type=IntPtr.Zero;int hr=Native.Call<Native.NativeType>(reader,5)(reader,Native.VideoStream,i,out type);
                if(hr<0){Native.Release(ref type);break;}
                try{ulong size; if(Native.Call<Native.GetLong>(type,8)(type,ref Native.FrameSize,out size)>=0){int w=(int)(size>>32),h=(int)(size&uint.MaxValue);if(w>0&&h>0)candidates.Add(Tuple.Create(SizeScore(w,h),i));}}
                finally{Native.Release(ref type);}
            }
            candidates.Sort((a,b)=>a.Item1.CompareTo(b.Item1));
            foreach(var item in candidates)
            {
                IntPtr type=IntPtr.Zero;
                try{if(Native.Call<Native.NativeType>(reader,5)(reader,Native.VideoStream,item.Item2,out type)>=0&&Native.Call<Native.SetType>(reader,7)(reader,Native.VideoStream,IntPtr.Zero,type)>=0)return;}
                finally{Native.Release(ref type);}
            }
            // Preserve the driver's default if it does not expose a selectable native mode.
        }
        internal static double SizeScore(int w,int h){return Math.Abs(Math.Log((double)w*h/(640*480)))+(Math.Max(w,h)>1280?8:0)+(Math.Min(w,h)<240?4:0);}
        void ReadFormat(IntPtr r)
        {
            IntPtr type=IntPtr.Zero;
            try
            {
                Native.Check(Native.Call<Native.CurrentType>(r,6)(r,Native.VideoStream,out type));
                ulong size;Native.Check(Native.Call<Native.GetLong>(type,8)(type,ref Native.FrameSize,out size));
                width=(int)(size>>32);height=(int)(size&uint.MaxValue);
                ValidateDimensions(width,height);
                uint pitch;stride=Native.Call<Native.GetUInt>(type,7)(type,ref Native.DefaultStride,out pitch)>=0?unchecked((int)pitch):width*4;
                if(Math.Abs((long)stride)<width*4L||Math.Abs((long)stride)>1024*1024)throw new InvalidOperationException("Invalid camera stride");
            }
            finally{Native.Release(ref type);}
        }
        public void Request()
        {
            lock(gate)
            {
                if(stopped||pending||reader==IntPtr.Zero||error!=null)return;
                pending=true;
                int hr=Native.Call<Native.Read>(reader,9)(reader,Native.VideoStream,0,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
                if(hr<0){pending=false;error=Marshal.GetExceptionForHR(hr);}
            }
        }
        public Bitmap TakeFrame(){lock(gate){var image=latest;latest=null;return image;}}
        public int OnReadSample(int status,uint stream,uint flags,long time,IntPtr sample)
        {
            lock(gate)
            {
                try
                {
                    if(stopped)return 0;
                    Native.Check(status);
                    if((flags&1)!=0)throw new InvalidOperationException("Camera stream error");
                    if((flags&2)!=0)throw new InvalidOperationException("Camera stream ended");
                    if((flags&32)!=0)ReadFormat(reader);
                    if(sample!=IntPtr.Zero)
                    {
                        var image=CopySample(sample);
                        var old=latest;latest=image;if(old!=null)old.Dispose();
                    }
                }
                catch(Exception ex){if(!stopped)error=ex;}
                finally{pending=false;}
            }
            return 0;
        }
        public int OnFlush(uint stream){lock(gate)pending=false;return 0;}
        public int OnEvent(uint stream,IntPtr mediaEvent){return 0;}
        Bitmap CopySample(IntPtr sample)
        {
            IntPtr buffer=IntPtr.Zero,twoD=IntPtr.Zero;
            bool locked=false,isTwoD=false;
            try
            {
                Native.Check(Native.Call<Native.GetPointer>(sample,41)(sample,out buffer));
                Guid iid=new Guid("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb");
                IntPtr data;int pitch;
                if(Marshal.QueryInterface(buffer,ref iid,out twoD)>=0)
                {
                    Native.Check(Native.Call<Native.Lock2D>(twoD,3)(twoD,out data,out pitch));locked=isTwoD=true;
                }
                else
                {
                    uint max,length;Native.Check(Native.Call<Native.LockBuffer>(buffer,3)(buffer,out data,out max,out length));locked=true;pitch=stride;
                    long needed=(height-1)*Math.Abs((long)pitch)+width*4L;
                    if(length<needed)throw new InvalidOperationException("Truncated camera frame");
                    if(pitch<0)data=IntPtr.Add(data,checked((height-1)*-pitch));
                }
                return CopyRgb(data,width,height,pitch);
            }
            finally
            {
                if(locked)Native.Call<Native.NoArgs>(isTwoD?twoD:buffer,4)(isTwoD?twoD:buffer);
                Native.Release(ref twoD);Native.Release(ref buffer);
            }
        }
        static void ValidateDimensions(int w,int h)
        {
            if(w<=0||h<=0||w>8192||h>8192||(long)w*h>32*1024*1024)throw new InvalidOperationException("Unsupported camera dimensions");
        }
        internal static Bitmap CopyRgb(IntPtr top,int w,int h,int pitch)
        {
            ValidateDimensions(w,h);if(top==IntPtr.Zero||Math.Abs((long)pitch)<w*4L||Math.Abs((long)pitch)>1024*1024)throw new InvalidOperationException("Invalid camera frame");
            var image=new Bitmap(w,h,PixelFormat.Format32bppRgb);
            try
            {
                var bits=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppRgb);
                try{byte[] row=new byte[w*4];for(int y=0;y<h;y++){Marshal.Copy(IntPtr.Add(top,checked(y*pitch)),row,0,row.Length);Marshal.Copy(row,0,IntPtr.Add(bits.Scan0,y*bits.Stride),row.Length);}}
                finally{image.UnlockBits(bits);}
                return image;
            }
            catch{image.Dispose();throw;}
        }
        public void Dispose()
        {
            lock(gate)
            {
                if(stopped)return;stopped=true;
                if(latest!=null){latest.Dispose();latest=null;}
                cleanup=Task.Run(()=>{
                    try{start.GetAwaiter().GetResult();}catch{}
                    IntPtr r,s;lock(gate){r=reader;s=source;reader=source=IntPtr.Zero;}
                    bool com=false;
                    try{com=Native.InitializeCom();if(r!=IntPtr.Zero)Native.Call<Native.Flush>(r,10)(r,Native.AllStreams);}
                    finally{try{Native.Shutdown(ref s);}finally{Native.Release(ref r);Native.ReleaseRuntime();if(com)Native.UninitializeCom();}}
                });
            }
        }

        // Slots are defined by mfobjects.h/mfidl.h/mfreadwrite.h, including the
        // three IUnknown slots and the 30 IMFAttributes methods.
        static class Native
        {
            static readonly object runtimeGate=new object();static int users;
            public const uint VideoStream=0xfffffffc,AllStreams=0xfffffffe;
            public static Guid SourceType=new Guid("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3"),VideoCapture=new Guid("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
            public static Guid FriendlyName=new Guid("60d0e559-52f8-4fa2-bbce-acdb34a8ec01"),SymbolicLink=new Guid("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f");
            public static Guid AsyncCallback=new Guid("1e3dbeac-bb43-4c35-b507-cd644464c965"),VideoProcessing=new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
            public static Guid MajorType=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),Subtype=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
            public static Guid Video=new Guid("73646976-0000-0010-8000-00aa00389b71"),Rgb32=new Guid("00000016-0000-0010-8000-00aa00389b71");
            public static Guid FrameSize=new Guid("1652c33d-d6b2-4012-b834-72030849a37d"),DefaultStride=new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
            [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFStartup(uint version,uint flags);
            [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint flags);
            [DllImport("ole32.dll")]static extern void CoUninitialize();
            [DllImport("mfplat.dll",ExactSpelling=true)]static extern int MFShutdown();
            [DllImport("mfplat.dll",ExactSpelling=true)]public static extern int MFCreateAttributes(out IntPtr value,uint size);
            [DllImport("mfplat.dll",ExactSpelling=true)]public static extern int MFCreateMediaType(out IntPtr value);
            [DllImport("mf.dll",ExactSpelling=true)]public static extern int MFEnumDeviceSources(IntPtr attributes,out IntPtr devices,out uint count);
            [DllImport("mf.dll",ExactSpelling=true)]public static extern int MFCreateDeviceSource(IntPtr attributes,out IntPtr source);
            [DllImport("mfreadwrite.dll",ExactSpelling=true)]public static extern int MFCreateSourceReaderFromMediaSource(IntPtr source,IntPtr attributes,out IntPtr reader);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int SetGuidDelegate(IntPtr self,ref Guid key,ref Guid value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int SetUIntDelegate(IntPtr self,ref Guid key,uint value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int SetText(IntPtr self,ref Guid key,[MarshalAs(UnmanagedType.LPWStr)]string value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int SetObject(IntPtr self,ref Guid key,IntPtr value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int GetText(IntPtr self,ref Guid key,out IntPtr text,out uint length);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int GetLong(IntPtr self,ref Guid key,out ulong value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int GetUInt(IntPtr self,ref Guid key,out uint value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int SelectStream(IntPtr self,uint stream,int selected);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int NativeType(IntPtr self,uint stream,uint index,out IntPtr type);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int CurrentType(IntPtr self,uint stream,out IntPtr type);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int SetType(IntPtr self,uint stream,IntPtr reserved,IntPtr type);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int Read(IntPtr self,uint stream,uint flags,IntPtr actualStream,IntPtr actualFlags,IntPtr time,IntPtr sample);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int Flush(IntPtr self,uint stream);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int GetPointer(IntPtr self,out IntPtr value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int LockBuffer(IntPtr self,out IntPtr data,out uint max,out uint length);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int Lock2D(IntPtr self,out IntPtr top,out int pitch);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int NoArgs(IntPtr self);
            public static T Call<T>(IntPtr instance,int slot)where T:class
            {
                return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size),typeof(T)) as T;
            }
            public static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
            public static void SetGuid(IntPtr attrs,Guid key,Guid value){Check(Call<SetGuidDelegate>(attrs,24)(attrs,ref key,ref value));}
            public static void SetUInt(IntPtr attrs,Guid key,uint value){Check(Call<SetUIntDelegate>(attrs,21)(attrs,ref key,value));}
            public static string String(IntPtr attrs,Guid key)
            {
                IntPtr text=IntPtr.Zero;uint length;
                try{Check(Call<GetText>(attrs,13)(attrs,ref key,out text,out length));return Marshal.PtrToStringUni(text,checked((int)length));}
                finally{if(text!=IntPtr.Zero)Marshal.FreeCoTaskMem(text);}
            }
            public static void Acquire(){lock(runtimeGate){if(users==0)Check(MFStartup(0x20070,0));users++;}}
            public static bool InitializeCom(){int hr=CoInitializeEx(IntPtr.Zero,0);if(hr==unchecked((int)0x80010106))return false;Check(hr);return true;}
            public static void UninitializeCom(){CoUninitialize();}
            public static void ReleaseRuntime(){lock(runtimeGate){if(--users==0)MFShutdown();}}
            public static void Release(ref IntPtr p){if(p!=IntPtr.Zero){Marshal.Release(p);p=IntPtr.Zero;}}
            public static void Shutdown(ref IntPtr p){if(p!=IntPtr.Zero){try{Call<NoArgs>(p,12)(p);}finally{Release(ref p);}}}
        }
    }
}

