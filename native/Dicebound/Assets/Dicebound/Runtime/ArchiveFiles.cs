using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Dicebound.Presentation
{
    public static class ArchiveFiles
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public int size;public IntPtr owner,instance;
            public IntPtr filter,customFilter;
            public int maxCustomFilter,filterIndex;public IntPtr file;public int maxFile;
            public IntPtr fileTitle;public int maxFileTitle;public IntPtr initialDirectory,title;
            public int flags;public short fileOffset,fileExtension;public IntPtr defaultExtension;
            public IntPtr customData,hook,template,reserved;public int reservedInt,flagsEx;
        }
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetOpenFileNameW(ref OpenFileName data);
        [DllImport("user32.dll")]private static extern IntPtr GetActiveWindow();
        public static string ChooseImport()
        {
            if(Application.platform!=RuntimePlatform.WindowsPlayer&&Application.platform!=RuntimePlatform.WindowsEditor)return null;
            var data=new OpenFileName{size=Marshal.SizeOf(typeof(OpenFileName)),owner=GetActiveWindow(),filterIndex=1,maxFile=32768,flags=0x1000|0x800|0x8};
            data.file=Marshal.AllocHGlobal(data.maxFile*2);Marshal.WriteInt16(data.file,0);
            data.filter=Marshal.StringToHGlobalUni("Dicebound 档案 (*.json)\0*.json\0所有文件\0*.*\0\0");
            data.title=Marshal.StringToHGlobalUni("导入 Dicebound 旅程档案");data.defaultExtension=Marshal.StringToHGlobalUni("json");
            data.initialDirectory=Marshal.StringToHGlobalUni(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            try{return GetOpenFileNameW(ref data)?Marshal.PtrToStringUni(data.file):null;}
            finally{foreach(var pointer in new[]{data.file,data.filter,data.title,data.defaultExtension,data.initialDirectory})Marshal.FreeHGlobal(pointer);}
        }
        public static string Export(string encoded,string directory)
        {
            Directory.CreateDirectory(directory);string file=Path.Combine(directory,"Dicebound-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json");File.WriteAllText(file,encoded,new UTF8Encoding(false));return file;
        }
    }
}
