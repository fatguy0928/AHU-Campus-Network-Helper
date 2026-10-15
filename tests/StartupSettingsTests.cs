using System;
using System.IO;
using System.Text;
using AhuCampus;

public static class StartupSettingsTests
{
    public static void Run()
    {
        ProtocolTests.Check("missing auto-connect settings default off without creating files", delegate {
            WithDirectory(delegate(string directory) {
                string missing=Path.Combine(directory,"missing","settings.json");
                ProtocolTests.Assert(!new AppSettingsStore(missing).LoadAutoConnect());
                ProtocolTests.Assert(!Directory.Exists(Path.GetDirectoryName(missing)));
            });
        });
        ProtocolTests.Check("auto-connect true and false round-trip as UTF-8 JSON", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                var store=new AppSettingsStore(path);
                store.SaveAutoConnect(true);
                ProtocolTests.Assert(new AppSettingsStore(path).LoadAutoConnect());
                ProtocolTests.Equal("{\"AutoConnect\":true}",File.ReadAllText(path,Encoding.UTF8));
                ProtocolTests.Equal((byte)'{',File.ReadAllBytes(path)[0]);
                store.SaveAutoConnect(false);
                ProtocolTests.Assert(!new AppSettingsStore(path).LoadAutoConnect());
                ProtocolTests.Equal("{\"AutoConnect\":false}",File.ReadAllText(path,Encoding.UTF8));
                ProtocolTests.Equal(1,Directory.GetFiles(directory).Length);
            });
        });
        ProtocolTests.Check("saving launch settings preserves the existing credential file", delegate {
            WithDirectory(delegate(string directory) {
                string credentials=Path.Combine(directory,"credentials.dat");
                byte[] original=new byte[]{0x12,0x00,0xFF,0x83,0x41,0x22};
                File.WriteAllBytes(credentials,original);
                var store=new AppSettingsStore(Path.Combine(directory,"settings.json"));
                store.SaveAutoConnect(true); store.SaveAutoConnect(false);
                ProtocolTests.Equal(Convert.ToBase64String(original),Convert.ToBase64String(File.ReadAllBytes(credentials)));
                ProtocolTests.Equal(2,Directory.GetFiles(directory).Length);
            });
        });
        string[] malformed=new string[]{"","broken","null","[]","{}","{\"AutoConnect\":\"true\"}","{\"AutoConnect\":1}","{\"AutoConnect\":null}","{\"AutoConnect\":true,\"AutoConnect\":false}","{\"AutoConnect\":true,\"Password\":\"secret\"}","{\"AutoConnect\":true} trailing"};
        for(int i=0;i<malformed.Length;i++)
        {
            string data=malformed[i];
            ProtocolTests.Check("invalid launch settings default off without rewriting: "+i,delegate {
                WithDirectory(delegate(string directory) {
                    string path=Path.Combine(directory,"settings.json");
                    File.WriteAllText(path,data,new UTF8Encoding(false));
                    ProtocolTests.Assert(!new AppSettingsStore(path).LoadAutoConnect());
                    ProtocolTests.Equal(data,File.ReadAllText(path,Encoding.UTF8));
                });
            });
        }
        ProtocolTests.Check("valid launch settings accept JSON whitespace", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                File.WriteAllText(path," \r\n{ \t\"AutoConnect\" \r\n: true }\t",new UTF8Encoding(false));
                ProtocolTests.Assert(new AppSettingsStore(path).LoadAutoConnect());
            });
        });
        ProtocolTests.Check("invalid UTF-8 launch settings default off without changing bytes", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                byte[] original=new byte[]{0xC3,0x28};
                File.WriteAllBytes(path,original);
                ProtocolTests.Assert(!new AppSettingsStore(path).LoadAutoConnect());
                ProtocolTests.Equal(Convert.ToBase64String(original),Convert.ToBase64String(File.ReadAllBytes(path)));
            });
        });
        ProtocolTests.Check("oversized launch settings default off without rewriting", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                byte[] oversized=new byte[1024*1024];
                File.WriteAllBytes(path,oversized);
                ProtocolTests.Assert(!new AppSettingsStore(path).LoadAutoConnect());
                ProtocolTests.Equal(1024L*1024L,new FileInfo(path).Length);
            });
        });
        ProtocolTests.Check("locked launch settings report the read error to the caller", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                File.WriteAllText(path,"{\"AutoConnect\":true}",new UTF8Encoding(false));
                bool threw=false;
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                {
                    try {new AppSettingsStore(path).LoadAutoConnect();}
                    catch(IOException) {threw=true;}
                }
                ProtocolTests.Assert(threw);
            });
        });
        ProtocolTests.Check("settings path that is a directory reports the access error", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                Directory.CreateDirectory(path);
                bool threw=false;
                try {new AppSettingsStore(path).LoadAutoConnect();}
                catch(UnauthorizedAccessException) {threw=true;}
                catch(IOException) {threw=true;}
                ProtocolTests.Assert(threw);
            });
        });
        ProtocolTests.Check("failed settings replacement preserves the old setting and cleans temporary files", delegate {
            WithDirectory(delegate(string directory) {
                string path=Path.Combine(directory,"settings.json");
                var store=new AppSettingsStore(path);
                store.SaveAutoConnect(true);
                bool threw=false;
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    try {store.SaveAutoConnect(false);}
                    catch(IOException) {threw=true;}
                    catch(UnauthorizedAccessException) {threw=true;}
                }
                ProtocolTests.Assert(threw);
                ProtocolTests.Assert(store.LoadAutoConnect());
                ProtocolTests.Equal(1,Directory.GetFiles(directory).Length);
            });
        });
        ProtocolTests.Check("Windows startup command quotes the full executable path only", delegate {
            ProtocolTests.Equal("\"C:\\Program Files\\安徽大学\\AHU校园网助手.exe\"",WindowsStartupRegistration.BuildCommand(@"C:\Program Files\安徽大学\AHU校园网助手.exe"));
        });
        ProtocolTests.Check("Windows startup command resolves relative executable paths", delegate {
            string expected="\""+Environment.CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar+"AHU.exe\"";
            ProtocolTests.Equal(expected,WindowsStartupRegistration.BuildCommand("AHU.exe"));
        });
        ProtocolTests.Check("Windows startup command rejects embedded quotes", delegate {
            bool threw=false;
            try {WindowsStartupRegistration.BuildCommand("C:\\AHU.exe\" --password secret");}
            catch(ArgumentException) {threw=true;}
            ProtocolTests.Assert(threw);
        });
    }

    static void WithDirectory(Action<string> test)
    {
        string directory=Path.Combine(Path.GetTempPath(),"AhuStartupTests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {test(directory);}
        finally {if(Directory.Exists(directory)) Directory.Delete(directory,true);}
    }
}
