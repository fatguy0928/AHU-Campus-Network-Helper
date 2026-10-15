using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AhuCampus
{
    public interface IAppSettingsStore
    {
        bool LoadAutoConnect();
        void SaveAutoConnect(bool enabled);
    }

    public sealed class AppSettingsStore:IAppSettingsStore
    {
        const int MaximumSettingsBytes=4096;
        static readonly UTF8Encoding SettingsEncoding=new UTF8Encoding(false,true);
        static readonly Regex SettingsJson=new Regex("\\A[ \\t\\r\\n]*\\{[ \\t\\r\\n]*\"AutoConnect\"[ \\t\\r\\n]*:[ \\t\\r\\n]*(true|false)[ \\t\\r\\n]*\\}[ \\t\\r\\n]*\\z",RegexOptions.CultureInvariant);
        readonly string path;

        public AppSettingsStore(string path)
        {
            if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings file path is required.","path");
            this.path=Path.GetFullPath(path);
        }

        public static AppSettingsStore CreateDefault()
        {
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AHU Campus Helper");
            return new AppSettingsStore(Path.Combine(directory,"settings.json"));
        }

        public bool LoadAutoConnect()
        {
            byte[] bytes;
            try
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<=0 || stream.Length>MaximumSettingsBytes) return false;
                    bytes=new byte[(int)stream.Length];
                    int offset=0;
                    while(offset<bytes.Length)
                    {
                        int read=stream.Read(bytes,offset,bytes.Length-offset);
                        if(read==0) return false;
                        offset+=read;
                    }
                }
            }
            catch(FileNotFoundException) {return false;}
            catch(DirectoryNotFoundException) {return false;}

            string contents;
            try {contents=SettingsEncoding.GetString(bytes);}
            catch(DecoderFallbackException) {return false;}
            Match setting=SettingsJson.Match(contents);
            return setting.Success && setting.Groups[1].Value=="true";
        }

        public void SaveAutoConnect(bool enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                byte[] bytes=SettingsEncoding.GetBytes(enabled ? "{\"AutoConnect\":true}" : "{\"AutoConnect\":false}");
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    stream.Write(bytes,0,bytes.Length);
                    stream.Flush(true);
                }
                if(File.Exists(path)) File.Replace(temporary,path,null);
                else File.Move(temporary,path);
            }
            finally {if(File.Exists(temporary)) File.Delete(temporary);}
        }
    }

    public interface IStartupRegistration
    {
        bool IsEnabled {get;}
        void SetEnabled(bool enabled);
    }

    public sealed class WindowsStartupRegistration:IStartupRegistration
    {
        const string RunKeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName="AHU Campus Helper";
        readonly string command;

        public WindowsStartupRegistration(string executablePath) {command=BuildCommand(executablePath);}

        public bool IsEnabled
        {
            get
            {
                using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKeyPath,false))
                {
                    if(key==null) return false;
                    foreach(string name in key.GetValueNames())
                        if(String.Equals(name,ValueName,StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            if(enabled)
            {
                using(RegistryKey key=Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if(key==null) throw new IOException("The current user's startup registry key could not be opened.");
                    key.SetValue(ValueName,command,RegistryValueKind.String);
                }
            }
            else
            {
                using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKeyPath,true))
                {
                    if(key!=null) key.DeleteValue(ValueName,false);
                }
            }
        }

        internal static string BuildCommand(string executablePath)
        {
            if(String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("An executable path is required.","executablePath");
            if(executablePath.IndexOfAny(new char[]{'\"','\r','\n'})>=0) throw new ArgumentException("The executable path contains invalid characters.","executablePath");
            return "\""+Path.GetFullPath(executablePath)+"\"";
        }
    }
}
