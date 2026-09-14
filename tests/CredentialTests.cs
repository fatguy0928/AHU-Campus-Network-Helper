using System;
using System.IO;
using AhuCampus;

public static class CredentialTests
{
    public static void Run()
    {
        ProtocolTests.Check("saved credentials round-trip without plaintext on disk", delegate {
            string dir=Path.Combine(Path.GetTempPath(),"AhuCampusTests-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(dir,"credentials.dat");
            try
            {
                var store=new CredentialStore(path,new TestProtector());
                store.Save("20260001","P@ss 密码 & +");
                var loaded=store.Load();
                ProtocolTests.Equal("20260001",loaded.Account);
                ProtocolTests.Equal("P@ss 密码 & +",loaded.Password);
                byte[] raw=File.ReadAllBytes(path);
                ProtocolTests.Assert(!Contains(raw,System.Text.Encoding.UTF8.GetBytes("20260001")));
                ProtocolTests.Assert(!Contains(raw,System.Text.Encoding.UTF8.GetBytes("P@ss 密码 & +")));
            }
            finally {if(Directory.Exists(dir)) Directory.Delete(dir,true);}
        });
        ProtocolTests.Check("clearing saved credentials removes the file", delegate {
            string dir=Path.Combine(Path.GetTempPath(),"AhuCampusTests-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(dir,"credentials.dat");
            try
            {
                var store=new CredentialStore(path,new TestProtector());
                store.Save("user","secret"); store.Clear();
                ProtocolTests.Assert(!File.Exists(path));
                ProtocolTests.Assert(store.Load()==null);
            }
            finally {if(Directory.Exists(dir)) Directory.Delete(dir,true);}
        });
        ProtocolTests.Check("corrupt credential data is discarded without crashing", delegate {
            string dir=Path.Combine(Path.GetTempPath(),"AhuCampusTests-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(dir,"credentials.dat");
            try
            {
                Directory.CreateDirectory(dir); File.WriteAllBytes(path,new byte[]{1,2,3,4,5});
                var loaded=new CredentialStore(path,new TestProtector()).Load();
                ProtocolTests.Assert(loaded==null && !File.Exists(path));
            }
            finally {if(Directory.Exists(dir)) Directory.Delete(dir,true);}
        });
        ProtocolTests.Check("empty credentials cannot be persisted", delegate {
            string path=Path.Combine(Path.GetTempPath(),"AhuCampusTests-"+Guid.NewGuid().ToString("N"),"credentials.dat");
            bool threw=false; try {new CredentialStore(path,new TestProtector()).Save("","secret");} catch(InvalidOperationException) {threw=true;}
            ProtocolTests.Assert(threw && !File.Exists(path));
        });
    }

    static bool Contains(byte[] haystack,byte[] needle)
    {
        for(int i=0;i<=haystack.Length-needle.Length;i++)
        {
            bool match=true;
            for(int j=0;j<needle.Length;j++) if(haystack[i+j]!=needle[j]) {match=false;break;}
            if(match) return true;
        }
        return false;
    }

    sealed class TestProtector:IDataProtector
    {
        public byte[] Protect(byte[] value) {return Transform(value);}
        public byte[] Unprotect(byte[] value) {return Transform(value);}
        static byte[] Transform(byte[] value)
        {
            byte[] result=new byte[value.Length];
            for(int i=0;i<value.Length;i++) result[i]=(byte)(value[i]^0xA7);
            return result;
        }
    }
}
