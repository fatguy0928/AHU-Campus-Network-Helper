using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using AhuCampus;

public static class StartupUiTests
{
    static int failures;
    static T Find<T>(Control form,string name) where T:Control {return (T)form.Controls.Find(name,true)[0];}
    static void Assert(bool value,string message) {if(!value) throw new Exception(message);}
    static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline=DateTime.UtcNow.AddSeconds(4);
        while(!condition() && DateTime.UtcNow<deadline) await Task.Delay(15);
        Assert(condition(),"Timed out waiting for startup behavior");
        await Task.Delay(40);
    }

    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e) {
            Console.WriteLine("STARTUP UI THREAD FAIL: "+e.Exception);
            Environment.Exit(1);
        };

        RunCase("startup options default off and persist independently",null,async delegate(Harness h) {
            var start=Find<CheckBox>(h.Form,"StartWithWindows");
            var auto=Find<CheckBox>(h.Form,"AutoConnectOnLaunch");
            Assert(!start.Checked && !auto.Checked,"Both startup options must default off");
            Assert(h.Startup.Writes==0 && h.Portal.Requests==0,"Opening the app must not opt in or connect");
            auto.Checked=true;
            Assert(h.Settings.LoadAutoConnect() && !start.Checked,"Auto-connect must persist independently");
            start.Checked=true;
            Assert(h.Startup.IsEnabled && auto.Checked,"Windows startup must not change auto-connect");
            start.Checked=false;
            Assert(!h.Startup.IsEnabled && h.Settings.LoadAutoConnect(),"Disabling startup must preserve auto-connect");
            await Task.Delay(80);
            Assert(h.Portal.Requests==0,"Changing options must not immediately connect");
            using(var reopened=new MainForm(new PortalClient(h.Portal,delegate{return "10.22.8.10";}),h.Credentials,h.Settings,h.Startup))
            {
                Assert(Find<CheckBox>(reopened,"AutoConnectOnLaunch").Checked
                    && !Find<CheckBox>(reopened,"StartWithWindows").Checked,"Reopening must restore both options");
            }
        });

        RunCase("saved credentials stay idle when auto-connect is off",delegate(Harness h) {
            h.Credentials.Save("startup-user","startup-secret");
            h.Startup.Enabled=true;
        },async delegate(Harness h) {
            await Task.Delay(80);
            Assert(h.Portal.Requests==0,"Saved credentials alone must not trigger login");
            Assert(Find<CheckBox>(h.Form,"StartWithWindows").Checked && h.Startup.Writes==0,
                "Reading an existing startup registration must not rewrite it");
        });

        RunCase("auto-connect uses saved credentials once after showing the window",delegate(Harness h) {
            h.Credentials.Save("startup-user","startup-secret");
            h.Settings.SaveAutoConnect(true);
        },async delegate(Harness h) {
            await WaitUntil(()=>Find<Label>(h.Form,"Status").Text=="已连接");
            Assert(h.Portal.Logins==1,"One app launch must submit exactly one authentication");
            var query=HttpUtility.ParseQueryString(h.Portal.LoginUri.Query);
            Assert(query["user_account"]=="startup-user" && query["user_password"]=="startup-secret",
                "Automatic authentication must use saved credentials");
            Assert(!Find<TextBox>(h.Form,"Log").Text.Contains("startup-secret"),"Automatic login must redact the password");
            h.Form.Hide(); h.Form.Show();
            await Task.Delay(80);
            Assert(h.Portal.Logins==1,"Showing the existing window must not start a second authentication");
        });

        RunCase("missing saved credentials show guidance without network requests",delegate(Harness h) {
            h.Settings.SaveAutoConnect(true);
        },async delegate(Harness h) {
            await Task.Delay(60);
            Assert(h.Portal.Requests==0,"Missing credentials must prevent automatic network requests");
            Assert(Find<Label>(h.Form,"Detail").Text.Contains("保存")
                || Find<Label>(h.Form,"Detail").Text.Contains("记住"),"Explain how to enable automatic connection");
            Find<TextBox>(h.Form,"Account").Text="typed-user";
            Find<TextBox>(h.Form,"Password").Text="typed-secret";
            await Task.Delay(60);
            Assert(h.Portal.Requests==0,"Typing later must not silently trigger automatic authentication");
        });

        RunCase("failed automatic authentication waits for manual retry",delegate(Harness h) {
            h.Credentials.Save("startup-user","startup-secret");
            h.Settings.SaveAutoConnect(true);
            h.Portal.Fail=true;
        },async delegate(Harness h) {
            await WaitUntil(()=>Find<Button>(h.Form,"Retry").Enabled);
            Assert(!Find<Button>(h.Form,"Connect").Enabled && h.Portal.Logins==1,"Failure must switch to manual retry");
            await Task.Delay(120);
            Assert(h.Portal.Logins==1,"Automatic authentication must not loop on failure");
            h.Portal.Fail=false;
            Find<Button>(h.Form,"Retry").PerformClick();
            await WaitUntil(()=>Find<Label>(h.Form,"Status").Text=="已连接");
            Assert(h.Portal.Logins==2,"Manual retry must remain usable");
        });

        RunCase("automatic connection retains cancellation and concurrency guard",delegate(Harness h) {
            h.Credentials.Save("startup-user","startup-secret");
            h.Settings.SaveAutoConnect(true);
            h.Portal.Slow=true;
        },async delegate(Harness h) {
            await WaitUntil(()=>Find<Button>(h.Form,"Cancel").Enabled);
            int requests=h.Portal.Requests;
            Find<Button>(h.Form,"Connect").PerformClick();
            Find<Button>(h.Form,"Retry").PerformClick();
            Assert(h.Portal.Requests==requests,"Busy buttons must not launch parallel requests");
            Find<Button>(h.Form,"Cancel").PerformClick();
            await WaitUntil(()=>Find<Button>(h.Form,"Retry").Enabled);
            Assert(Find<Label>(h.Form,"Status").Text=="已取消" && !Find<Button>(h.Form,"Cancel").Enabled,
                "Automatic requests must use the existing cancellation flow");
        });

        RunCase("startup registration errors rollback the checkbox",delegate(Harness h) {
            h.Startup.FailWrites=true;
        },async delegate(Harness h) {
            Find<CheckBox>(h.Form,"StartWithWindows").Checked=true;
            Assert(!Find<CheckBox>(h.Form,"StartWithWindows").Checked,"Rejected registration must rollback the UI");
            Assert(Find<TextBox>(h.Form,"Log").Text.Contains("自启动"),"Registration failure must be explained");
            await Task.Yield();
        });

        RunCase("settings errors rollback auto-connect and preserve manual controls",delegate(Harness h) {
            h.SettingsOverride=new BrokenSettings();
        },async delegate(Harness h) {
            Find<CheckBox>(h.Form,"AutoConnectOnLaunch").Checked=true;
            Assert(!Find<CheckBox>(h.Form,"AutoConnectOnLaunch").Checked,"Rejected save must rollback auto-connect");
            Assert(Find<Button>(h.Form,"Connect").Enabled && h.Portal.Requests==0,"Settings errors must not break manual connection");
            await Task.Yield();
        });

        RunCase("startup read errors do not crash or create startup entries",delegate(Harness h) {
            h.Startup.FailReads=true;
        },async delegate(Harness h) {
            Assert(Find<Button>(h.Form,"Connect").Enabled && h.Startup.Writes==0,"A failed registry read must not mutate startup entries");
            Assert(!Find<CheckBox>(h.Form,"StartWithWindows").Checked,"An unreadable entry must not appear enabled");
            await Task.Yield();
        });

        RunCase("closing during a queued automatic start does not connect",delegate(Harness h) {
            h.Credentials.Save("startup-user","startup-secret");
            h.Settings.SaveAutoConnect(true);
            h.CloseOnShown=true;
        },null);

        Console.WriteLine("Startup UI failures: "+failures);
        return failures==0 ? 0 : 1;
    }

    static void RunCase(string name,Action<Harness> setup,Func<Harness,Task> test)
    {
        using(var h=new Harness())
        {
            bool passed=false;
            try
            {
                if(setup!=null) setup(h);
                h.Form=new MainForm(new PortalClient(h.Portal,delegate{return "10.22.8.10";}),
                    h.Credentials,h.SettingsOverride ?? h.Settings,h.Startup);
                h.Form.StartPosition=FormStartPosition.Manual;
                h.Form.Location=new Point(-2400,-2400);
                h.Form.ShowInTaskbar=false;
                h.Form.Shown+=async delegate {
                    if(h.CloseOnShown) {h.Form.Close();passed=true;return;}
                    try {await Task.Yield();await test(h);passed=true;}
                    catch(Exception e) {Console.WriteLine("STARTUP UI FAIL "+name+": "+e.Message);}
                    finally {h.Form.Close();}
                };
                Application.Run(h.Form);
                if(h.CloseOnShown) Assert(h.Portal.Requests==0,"Closing before the queued start must prevent authentication");
            }
            catch(Exception e) {Console.WriteLine("STARTUP UI FAIL "+name+": "+e.Message);passed=false;}
            if(passed) Console.WriteLine("PASS "+name); else failures++;
        }
    }

    sealed class Harness:IDisposable
    {
        readonly string directory=Path.Combine(Path.GetTempPath(),"AhuStartupUi-"+Guid.NewGuid().ToString("N"));
        public MainForm Form;
        public readonly CredentialStore Credentials;
        public readonly AppSettingsStore Settings;
        public readonly TestPortal Portal=new TestPortal();
        public readonly FakeStartup Startup=new FakeStartup();
        public IAppSettingsStore SettingsOverride;
        public bool CloseOnShown;
        public Harness()
        {
            Credentials=new CredentialStore(Path.Combine(directory,"credentials.dat"),new TestProtector());
            Settings=new AppSettingsStore(Path.Combine(directory,"settings.json"));
        }
        public void Dispose()
        {
            if(Form!=null) Form.Dispose();
            if(Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }

    public sealed class FakeStartup:IStartupRegistration
    {
        public bool Enabled,FailReads,FailWrites;
        public int Writes;
        public bool IsEnabled {get {if(FailReads) throw new UnauthorizedAccessException();return Enabled;}}
        public void SetEnabled(bool enabled) {Writes++;if(FailWrites) throw new UnauthorizedAccessException();Enabled=enabled;}
    }
    sealed class BrokenSettings:IAppSettingsStore
    {
        public bool LoadAutoConnect() {throw new IOException();}
        public void SaveAutoConnect(bool enabled) {throw new IOException();}
    }
    sealed class TestPortal:IPortalTransport
    {
        public int Requests,Logins;
        public bool Fail,Slow;
        public Uri LoginUri;
        public async Task<HttpResult> GetAsync(Uri uri,CancellationToken token)
        {
            Requests++;
            await Task.Delay(Slow ? 30000 : 10,token);
            if(uri.Port==801) {Logins++;LoginUri=uri;}
            return new HttpResult {Uri=uri,Status=200,Body=uri.Port==801
                ? (Fail ? "dr1003({\"result\":\"0\",\"msg\":\"bad account\"})" : "dr1003({\"result\":\"1\"})")
                : uri.Host=="172.16.253.3" ? "v46ip='10.22.8.10'" : "Microsoft Connect Test"};
        }
    }
    sealed class TestProtector:IDataProtector
    {
        public byte[] Protect(byte[] value) {return Transform(value);}
        public byte[] Unprotect(byte[] value) {return Transform(value);}
        static byte[] Transform(byte[] value)
        {
            byte[] result=new byte[value.Length];
            for(int i=0;i<value.Length;i++) result[i]=(byte)(value[i]^0x6D);
            return result;
        }
    }
}
