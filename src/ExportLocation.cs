using System;
using System.IO;
using System.Text;

namespace QQImageSwitch
{
    public class ExportLocation
    {
        readonly string settingsFile;
        public string SettingsFile {get{return settingsFile;}}
        public ExportLocation(string file=null)
        {
            settingsFile=file??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QQImageSwitch","export-folder.txt");
        }
        public string Load()
        {
            try{return File.Exists(settingsFile)?Normalize(File.ReadAllText(settingsFile,Encoding.UTF8)):"";}
            catch(IOException){return "";}
            catch(UnauthorizedAccessException){return "";}
            catch(ArgumentException){return "";}
            catch(NotSupportedException){return "";}
        }
        public static string Normalize(string folder)
        {
            string path=Environment.ExpandEnvironmentVariables((folder??"").Trim().Trim('"'));
            if(path.Length==0)return "";
            string root=Path.GetPathRoot(path);
            if(!Path.IsPathRooted(path)||root=="\\"||root.EndsWith(":"))throw new ArgumentException("保存位置请填写完整路径，例如 D:\\图片导出。");
            path=Path.GetFullPath(path);
            if(File.Exists(path))throw new ArgumentException("保存位置需要是文件夹，不能是文件。");
            return path;
        }
        public void Save(string folder)
        {
            string path=Normalize(folder);
            if(path==Load())return;
            string parent=Path.GetDirectoryName(Path.GetFullPath(settingsFile));Directory.CreateDirectory(parent);
            Codec.SaveAtomic(settingsFile,Encoding.UTF8.GetBytes(path));
        }
        public static string FunctionPath(string folder,string feature)
        {
            if(feature=="还原")feature="双图切换"; // Keep older callers on the merged page's destination.
            string root=Normalize(folder);
            if(root.Length==0)throw new ArgumentException("请先选择保存位置。");
            if(feature!="双图切换"&&feature!="图片混淆"&&feature!="合成GIF"&&feature!="清信息"&&feature!="打码"&&feature!="tag读取"&&feature!="文件伪装")throw new ArgumentException("未知功能文件夹。");
            return Path.Combine(root,feature);
        }
        public static string FunctionFolder(string folder,string feature)
        {
            string target=FunctionPath(folder,feature);Directory.CreateDirectory(target);
            if(feature=="双图切换"||feature=="还原")RemoveEmptyLegacyRestoreFolder(Path.GetDirectoryName(target));
            return target;
        }
        internal static void RemoveEmptyLegacyRestoreFolder(string root)
        {
            string legacy=Path.Combine(root,"还原");
            try
            {
                if(!Directory.Exists(legacy)||(File.GetAttributes(legacy)&FileAttributes.ReparsePoint)!=0)return;
                // Nonrecursive deletion rejects nonempty directories, including concurrent additions.
                Directory.Delete(legacy,false);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
