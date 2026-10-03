using System;
using System.IO;
using System.Text;
using Dicebound.Tactics;
namespace Dicebound.Persistence
{
    public sealed class TacticalStore
    {
        public string SavePath {get;}
        public string BackupPath {get{return SavePath+".previous";}}
        public TacticalStore(string directory){SavePath=Path.Combine(directory,"tactical-journey.json");}
        public bool TrySave(TacticalState state,out string error)
        {
            error=null;
            try {
                string text=TacticalCodec.Encode(state);Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string stage=SavePath+".pending";
                using(var stream=new FileStream(stage,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=new UTF8Encoding(false).GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(SavePath))File.Replace(stage,SavePath,BackupPath);else File.Move(stage,SavePath);return true;
            }catch(Exception e) when(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException){error="战棋档案写入失败，最后一次可读档案仍已保留。";return false;}
        }
        public bool TryLoad(out TacticalState state,out string error){return Read(SavePath,out state,out error);}
        public bool TryRecoverPrevious(out TacticalState state,out string error){return Read(BackupPath,out state,out error);}
        static bool Read(string path,out TacticalState state,out string error)
        {
            state=null;error=null;if(!File.Exists(path))return false;
            try {if(new FileInfo(path).Length>1024000){error="战棋档案过大，原文件已保留。";return false;}return TacticalCodec.TryDecode(File.ReadAllText(path),out state,out error);}
            catch(IOException){error="战棋档案无法读取，原文件已保留。";return false;}
            catch(UnauthorizedAccessException){error="战棋档案没有读取权限，原文件已保留。";return false;}
        }
    }
}
