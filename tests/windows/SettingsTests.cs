using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace QQImageSwitch
{
    static class SettingsTests
    {
        public static void Run(string directory)
        {
            string dir=Path.GetFullPath(directory);Directory.CreateDirectory(dir);
            string file=Path.Combine(dir,"preferences","export-folder.txt");
            var settings=new ExportLocation(file);settings.Save("");
            string first=Path.Combine(dir,"图片导出 第一处"),second=Path.Combine(dir,"图片导出 第二处");
            using(var form=new MainForm(file))
            {form.Show();Application.DoEvents();form.CheckExportLocation("",first);form.Close();}
            using(var form=new MainForm(file))
            {form.Show();Application.DoEvents();form.CheckExportLocation(first,second);form.Close();}
            if(new ExportLocation(file).Load()!=second)throw new Exception("Folder changed in UI did not persist across restart");
            string input=Path.Combine(dir,"original.png");
            using(var real=SelfTest.Art(80,60,true))Codec.SaveAtomic(input,Codec.StaticPng(real));
            var pairs=new List<BatchItem>{new BatchItem {Path=input,Name="原图.png"}};
            var result=Batch.Export(pairs,null,new ExportLocation(file).Load(),0,false,CancellationToken.None,null);
            if(result.Files.Count!=1||Path.GetDirectoryName(result.Files[0])!=second)throw new Exception("Export did not use persisted location");
            foreach(string invalid in new[]{"relative-folder","C:relative-folder","\\relative-folder",input})
            {
                bool rejected=false;try{settings.Save(invalid);}catch(ArgumentException){rejected=true;}
                if(!rejected||settings.Load()!=second)throw new Exception("Invalid path replaced remembered location");
            }
            File.WriteAllText(file,"invalid-relative-folder");
            if(settings.Load()!="")throw new Exception("Malformed preference should not prevent startup");
            settings.Save(second);
            foreach(string feature in new[]{"双图切换","图片混淆","合成GIF","清信息","打码","tag读取","文件伪装"})
            {
                string child=ExportLocation.FunctionFolder(second,feature);
                if(child!=Path.Combine(second,feature)||!Directory.Exists(child)||settings.Load()!=second)throw new Exception("Function folder changed root preference or used wrong child folder");
                using(var picker=new FolderPicker(settings,feature))if(picker.Destination!=child)throw new Exception("Page export destination ignores feature folder");
            }
            string untouched=Path.Combine(dir,"路径检查-"+Guid.NewGuid().ToString("N"));settings.Save(untouched);
            foreach(string feature in new[]{"双图切换","图片混淆","合成GIF","清信息","打码","tag读取","文件伪装","还原"})
            {
                using(var picker=new FolderPicker(settings,feature))
                {
                    string expected=Path.Combine(untouched,feature=="还原"?"双图切换":feature);
                    if(picker.Destination!=expected||picker.ExistingDestination!=null||Directory.Exists(untouched))throw new Exception("Reading export location created a directory");
                }
            }
            bool invalidFeature=false;try{ExportLocation.FunctionFolder(untouched,"../unexpected");}catch(ArgumentException){invalidFeature=true;}
            if(!invalidFeature||Directory.Exists(untouched))throw new Exception("Invalid feature created a directory");
            Directory.CreateDirectory(Path.Combine(untouched,"还原"));
            using(var picker=new FolderPicker(settings,"还原"))
            {
                if(!picker.Ready()||picker.ExistingDestination!=Path.Combine(untouched,"双图切换")||Directory.Exists(Path.Combine(untouched,"还原")))throw new Exception("Legacy restore created obsolete output folder");
            }
            string retained=Path.Combine(untouched,"还原");Directory.CreateDirectory(retained);string sentinel=Path.Combine(retained,"保留.txt");File.WriteAllText(sentinel,"Keep existing files");
            ExportLocation.FunctionFolder(untouched,"双图切换");
            if(!File.Exists(sentinel)||File.ReadAllText(sentinel)!="Keep existing files")throw new Exception("Legacy cleanup removed existing user files");
            settings.Save(second);
            var prefs=new ToolPreferences(file);prefs.CoverText=new CoverTextOptions {Text="任意位置=测试\n文字",Position=3,X=73,Y=29,SizePercent=9,Color=System.Drawing.Color.Red};prefs.Save();
            var read=new ToolPreferences(file);
            if(read.CoverText.Text!=prefs.CoverText.Text||read.CoverText.Position!=3||read.CoverText.X!=73||read.CoverText.Y!=29||read.CoverText.SizePercent!=9||read.CoverText.Color.ToArgb()!=System.Drawing.Color.Red.ToArgb())throw new Exception("Cover text preferences not persisted");
            using(var form=new MainForm(file))
            {form.Show();Application.DoEvents();form.CheckExportLocation(second,"");form.Close();}
            if(settings.Load()!="")throw new Exception("Clearing folder failed to clear preference");
            File.WriteAllText(Path.Combine(dir,"settings-tests.txt"),"PASS: UI remembers Unicode/spaced paths across restart, path changes persist, batch uses remembered folder, invalid paths preserve previous setting, corrupt preferences load safely, clearing field clears preference, seven function subfolders created without nesting or altering root; path queries have no filesystem side effects, invalid features create no directory, legacy restore resolves to merged double PNG folder; old empty restore folder removed and nonempty folder preserved, free text content/color/size/position persists.");
        }
    }
}
