using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AhuCampus
{
    public interface IDataProtector
    {
        byte[] Protect(byte[] plain);
        byte[] Unprotect(byte[] encrypted);
    }

    public sealed class WindowsUserDataProtector : IDataProtector
    {
        static readonly byte[] Entropy=Encoding.UTF8.GetBytes("AHU Campus Helper credentials v1");
        public byte[] Protect(byte[] plain) {return ProtectedData.Protect(plain,Entropy,DataProtectionScope.CurrentUser);}
        public byte[] Unprotect(byte[] encrypted) {return ProtectedData.Unprotect(encrypted,Entropy,DataProtectionScope.CurrentUser);}
    }

    public sealed class SavedCredentials
    {
        public string Account;
        public string Password;
    }

    public sealed class CredentialStore
    {
        readonly string path;
        readonly IDataProtector protector;

        public CredentialStore(string path) : this(path,new WindowsUserDataProtector()) { }
        public CredentialStore(string path,IDataProtector protector) { this.path=path; this.protector=protector; }

        public static CredentialStore CreateDefault()
        {
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AHU Campus Helper");
            return new CredentialStore(Path.Combine(directory,"credentials.dat"));
        }

        public void Save(string account,string password)
        {
            if(String.IsNullOrWhiteSpace(account) || String.IsNullOrEmpty(password))
                throw new InvalidOperationException("账号或密码为空，无法保存。");
            byte[] plain;
            using(var buffer=new MemoryStream())
            using(var writer=new BinaryWriter(buffer,new UTF8Encoding(false),true))
            {
                writer.Write(1);
                writer.Write(account.Trim());
                writer.Write(password);
                writer.Flush();
                plain=buffer.ToArray();
            }
            byte[] encrypted=protector.Protect(plain);
            Array.Clear(plain,0,plain.Length);
            string directory=Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllBytes(temporary,encrypted);
                if(File.Exists(path)) File.Replace(temporary,path,null,true);
                else File.Move(temporary,path);
            }
            finally {if(File.Exists(temporary)) File.Delete(temporary);}
        }

        public SavedCredentials Load()
        {
            if(!File.Exists(path)) return null;
            try
            {
                var info=new FileInfo(path);
                if(info.Length<=0 || info.Length>32768) throw new InvalidDataException();
                byte[] encrypted=File.ReadAllBytes(path);
                byte[] plain=protector.Unprotect(encrypted);
                try
                {
                    if(plain.Length>16384) throw new InvalidDataException();
                    using(var buffer=new MemoryStream(plain,false))
                    using(var reader=new BinaryReader(buffer,Encoding.UTF8))
                    {
                        if(reader.ReadInt32()!=1) throw new InvalidDataException();
                        string account=reader.ReadString();
                        string password=reader.ReadString();
                        if(buffer.Position!=buffer.Length || String.IsNullOrWhiteSpace(account) || String.IsNullOrEmpty(password)) throw new InvalidDataException();
                        return new SavedCredentials {Account=account,Password=password};
                    }
                }
                finally {Array.Clear(plain,0,plain.Length);}
            }
            catch(Exception e)
            {
                if(!(e is CryptographicException) && !(e is IOException) && !(e is UnauthorizedAccessException) && !(e is InvalidDataException) && !(e is ArgumentException)) throw;
                try {Clear();} catch(IOException) { } catch(UnauthorizedAccessException) { }
                return null;
            }
        }

        public void Clear()
        {
            if(File.Exists(path)) File.Delete(path);
        }
    }
}
