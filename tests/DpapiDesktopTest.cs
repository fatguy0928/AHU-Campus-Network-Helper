using System;
using System.IO;
using System.Security.Principal;
using AhuCampus;

public static class DpapiDesktopTest
{
    [STAThread]
    public static void Main()
    {
        string directory=AppDomain.CurrentDomain.BaseDirectory;
        string data=Path.Combine(directory,"dpapi-desktop-credentials.dat");
        string result=Path.Combine(directory,"dpapi-desktop-result.txt");
        string message;
        try
        {
            var store=new CredentialStore(data);
            store.Save("desktop-test-user","desktop-test-password");
            var loaded=store.Load();
            bool encrypted=!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(data)).Contains("desktop-test-password");
            if(loaded==null || loaded.Account!="desktop-test-user" || loaded.Password!="desktop-test-password" || !encrypted) throw new Exception("round-trip mismatch");
            store.Clear();
            message="PASS Windows DPAPI current-user round-trip and clear | "+WindowsIdentity.GetCurrent().Name;
        }
        catch(Exception e) {message="FAIL "+e.GetType().Name+" "+e.Message;}
        File.WriteAllText(result,message);
    }
}
