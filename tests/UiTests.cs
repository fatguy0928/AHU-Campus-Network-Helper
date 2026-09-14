using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using AhuCampus;

public static class UiTests
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern uint SetErrorMode(uint mode);
    static T Find<T>(Control form,string name) where T:Control { return (T)form.Controls.Find(name,true)[0]; }
    static void Assert(bool condition,string message) {if(!condition) throw new Exception(message);}
    static void Activate(LinkLabel link)
    {
        var method=typeof(LinkLabel).GetMethod("OnLinkClicked",BindingFlags.Instance|BindingFlags.NonPublic);
        method.Invoke(link,new object[]{new LinkLabelLinkClickedEventArgs(link.Links[0])});
    }
    static async Task WaitUntil(Func<bool> done)
    {
        var stop=DateTime.UtcNow.AddSeconds(4);
        while(!done() && DateTime.UtcNow<stop) await Task.Delay(20);
        Assert(done(),"Timed out waiting for UI state");
        await Task.Delay(40);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        SetErrorMode(0x0002);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e) {
            Console.WriteLine("UI THREAD EXCEPTION "+e.Exception.GetType().FullName+" "+e.Exception.Message);
            Environment.Exit(1);
        };
        var fake=new UiPortal(); int result=1;
        string credentialDir=Path.Combine(Path.GetTempPath(),"AhuCampusUiTests-"+Guid.NewGuid().ToString("N"));
        string credentialPath=Path.Combine(credentialDir,"credentials.dat");
        var store=new CredentialStore(credentialPath,new UiProtector());
        store.Save("loaded-user","loaded-secret");
        int routeReads=0;
        using(var form=new MainForm(new PortalClient(fake,delegate {routeReads++; return "10.22.8.10";}),store))
        {
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-2400,-2400); form.ShowInTaskbar=false;
            form.Shown+=async delegate {
                try
                {
                    await Task.Yield();
                    string screenshot=Path.GetFullPath(args[0]);
                    var account=Find<TextBox>(form,"Account"); var pass=Find<TextBox>(form,"Password");
                    var connect=Find<Button>(form,"Connect"); var retry=Find<Button>(form,"Retry"); var cancel=Find<Button>(form,"Cancel");
                    var status=Find<Label>(form,"Status");
                    Assert(connect.Enabled && !retry.Enabled,"Retry must be unavailable before a failed connection attempt");
                    Console.WriteLine("PASS retry starts unavailable");
                    using(var embedded=Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                    using(var actual=form.Icon.ToBitmap())
                    using(var expected=embedded.ToBitmap())
                    {
                        Assert(SameImage(actual,expected),"Window must use the executable's embedded AHU icon");
                    }
                    Console.WriteLine("PASS window uses the embedded application icon");
                    Assert(pass.UseSystemPasswordChar,"Password must be masked");
                    var remember=Find<CheckBox>(form,"RememberCredentials");
                    var clear=Find<LinkLabel>(form,"ClearCredentials");
                    Assert(account.Text=="loaded-user" && pass.Text=="loaded-secret" && remember.Checked,"Saved credentials must load at startup");
                    Console.WriteLine("PASS saved credentials populate the form");
                    Activate(clear); await Task.Delay(40);
                    Assert(account.Text=="" && pass.Text=="" && !remember.Checked && store.Load()==null,"Clear must remove saved credentials and form values");
                    Assert(Occurrences(Find<TextBox>(form,"Log").Text,"已清除保存的账号密码")==1,"Clear action must log once");
                    Console.WriteLine("PASS clear saved credentials action");
                    remember.Checked=true;
                    status.Text="等待连接";
                    Find<Label>(form,"Detail").Text="输入校园网账号和密码，点击「连接」。";
                    Find<TextBox>(form,"Log").Clear();
                    using(var bitmap=new Bitmap(form.Width,form.Height))
                    {form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(screenshot);}
                    connect.PerformClick();
                    Assert(fake.Count==0 && status.Text.Contains("填写"),"Empty input must not reach network");
                    Console.WriteLine("PASS empty input validation and password masking");
                    account.Text="sample-user"; pass.Text="sample-secret"; remember.Checked=true;
                    connect.PerformClick(); await WaitUntil(()=>connect.Enabled);
                    Assert(status.Text=="已连接","Expected verified connected state");
                    Assert(!retry.Enabled,"Successful connection must not offer retry");
                    Assert(!Find<TextBox>(form,"Log").Text.Contains("sample-secret"),"Password leaked to log");
                    Assert(store.Load().Account=="sample-user" && store.Load().Password=="sample-secret","Connect must save checked credentials");
                    Console.WriteLine("PASS native connect button and verified status");
                    remember.Checked=false;
                    Assert(store.Load()==null,"Unchecking remember must delete saved credentials immediately");
                    Console.WriteLine("PASS unchecking remember deletes saved credentials");
                    fake.Fail=true; connect.PerformClick(); await WaitUntil(()=>retry.Enabled);
                    Assert(status.Text=="连接未成功" && !connect.Enabled,"A failed connection must switch from Connect to Retry");
                    int readsBeforeRetry=routeReads;
                    fake.Fail=false; retry.PerformClick(); await WaitUntil(()=>connect.Enabled);
                    Assert(status.Text=="已连接" && !retry.Enabled,"Successful retry must switch back to Connect");
                    Assert(routeReads==readsBeforeRetry+1,"Retry must re-read the current campus-network IP");
                    Assert(Find<TextBox>(form,"Log").Text.Contains("开始重试"),"Retry must be identified separately in the connection log");
                    Console.WriteLine("PASS connect and retry have distinct states and retry refreshes network context");
                    fake.Slow=true; connect.PerformClick(); await Task.Delay(40);
                    Assert(!connect.Enabled && !retry.Enabled && cancel.Enabled,"Busy controls must prevent concurrent login");
                    cancel.PerformClick(); await WaitUntil(()=>retry.Enabled);
                    Assert(status.Text=="已取消" && !connect.Enabled && retry.Enabled && !cancel.Enabled,"Cancelled state must switch to retry");
                    Console.WriteLine("PASS busy state and cancellation");
                    result=0;
                }
                catch(Exception e) {Console.WriteLine("UI FAIL: "+e.GetType().Name+" "+e.Message);}
                finally {form.Close();}
            };
            Application.Run(form);
        }
        if(Directory.Exists(credentialDir)) Directory.Delete(credentialDir,true);
        Console.WriteLine("UI failures: "+result);
        return result;
    }
    static int Occurrences(string text,string value)
    {
        int count=0,index=0;
        while((index=text.IndexOf(value,index,StringComparison.Ordinal))>=0) {count++;index+=value.Length;}
        return count;
    }
    static bool SameImage(Bitmap left,Bitmap right)
    {
        if(left.Width!=right.Width || left.Height!=right.Height) return false;
        for(int y=0;y<left.Height;y++) for(int x=0;x<left.Width;x++)
            if(left.GetPixel(x,y).ToArgb()!=right.GetPixel(x,y).ToArgb()) return false;
        return true;
    }
    sealed class UiPortal:IPortalTransport
    {
        public bool Fail,Slow;
        public int Count;
        public async Task<HttpResult> GetAsync(Uri u,CancellationToken t)
        {
            Count++;
            if(Slow) await Task.Delay(30000,t);
            return FakePortal.Result(u,u.Port==801 ? (Fail ? "dr1003({\"result\":\"0\",\"msg\":\"account or password error\"})" : "dr1003({\"result\":\"1\"})") : u.Host=="172.16.253.3" ? "v46ip='10.22.8.10'" : "Microsoft Connect Test");
        }
    }
    sealed class UiProtector:IDataProtector
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
