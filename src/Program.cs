using System;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("安徽大学校园网助手")]
[assembly: AssemblyDescription("安徽大学校园网连接与重试工具")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]
namespace AhuCampus
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using(var transport=new HttpPortalTransport())
            using(var form=new MainForm(new PortalClient(transport,HttpPortalTransport.GetRouteIp)))
                Application.Run(form);
        }
    }
}
