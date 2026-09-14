using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AhuCampus;

public sealed class FakePortal : IPortalTransport
{
    public readonly List<Uri> Requests = new List<Uri>();
    public Func<Uri, HttpResult> Respond;
    public Task<HttpResult> GetAsync(Uri uri, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Requests.Add(uri);
        return Task.FromResult(Respond(uri));
    }
    public static HttpResult Result(Uri u, string body) { return new HttpResult {Uri=u,Status=200,Body=body}; }
}
public static class ClientTests
{
    public static void Run()
    {
        ProtocolTests.Check("a retry reloads the IP and uses new credentials", delegate {
            var t=new FakePortal(); int pages=0;
            t.Respond=delegate(Uri u) {
                if (u.Port==80 && u.Host=="172.16.253.3") return FakePortal.Result(u, "v46ip='10.22.2."+(++pages+60)+"';");
                if (u.Port==801) return FakePortal.Result(u, "dr1003({\"result\":\"1\"})");
                return FakePortal.Result(u, "Microsoft Connect Test");
            };
            var client=new PortalClient(t, delegate {return "198.18.0.1";});
            var a=client.LoginAsync("old","first",null,CancellationToken.None).GetAwaiter().GetResult();
            var b=client.LoginAsync("new","second",null,CancellationToken.None).GetAwaiter().GetResult();
            ProtocolTests.Assert(a.Accepted && b.Accepted && b.InternetVerified);
            var logins=t.Requests.FindAll(u=>u.Port==801);
            ProtocolTests.Equal(2, logins.Count);
            var q=HttpUtility.ParseQueryString(logins[1].Query);
            ProtocolTests.Equal("10.22.2.62",q["wlan_user_ip"]); ProtocolTests.Equal("new",q["user_account"]); ProtocolTests.Equal("second",q["user_password"]);
            ProtocolTests.Assert(t.Requests.FindAll(u=>u.Host!="172.16.253.3").TrueForAll(u=>u.Query==""));
        });
        ProtocolTests.Check("wrong password submits once and returns a failure", delegate {
            var t=new FakePortal {Respond=u=>FakePortal.Result(u,u.Port==801 ? "dr1003({\"result\":\"0\",\"msg\":\"bad password\"})" : "v46ip='10.2.3.4'")};
            var r=new PortalClient(t,()=> "10.2.3.4").LoginAsync("test","wrong",null,CancellationToken.None).GetAwaiter().GetResult();
            ProtocolTests.Assert(!r.Accepted); ProtocolTests.Equal(2,t.Requests.Count);
        });
        ProtocolTests.Check("captive page must not pass the internet check", delegate {
            var t=new FakePortal {Respond=u=>FakePortal.Result(u,u.Port==801 ? "dr1003({\"result\":\"0\",\"ret_code\":2})" : u.Host=="172.16.253.3" ? "v46ip='10.2.3.4'" : "<html>登录</html>")};
            var r=new PortalClient(t,()=> "10.2.3.4").LoginAsync("test","wrong",null,CancellationToken.None).GetAwaiter().GetResult();
            ProtocolTests.Assert(r.Accepted && !r.InternetVerified);
        });
        ProtocolTests.Check("cancellation prevents any credential request", delegate {
            var t=new FakePortal(); var cancel=new CancellationTokenSource(); cancel.Cancel();
            bool caught=false; try { new PortalClient(t,()=> "10.2.3.4").LoginAsync("test","wrong",null,cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) {caught=true;}
            ProtocolTests.Assert(caught); ProtocolTests.Equal(0,t.Requests.Count);
        });
        ProtocolTests.Check("portal redirects cannot carry credentials to another host", delegate {
            var t=new FakePortal {Respond=u=>new HttpResult {Uri=u,Status=302,Location="http://example.com/",Body=""}};
            bool caught=false; try { new PortalClient(t,()=> "10.2.3.4").LoginAsync("test","wrong",null,CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) {caught=true;}
            ProtocolTests.Assert(caught); ProtocolTests.Equal(1,t.Requests.Count);
        });
        ProtocolTests.Check("a rejected probe body must preserve accepted authentication", delegate {
            var t=new FakePortal {Respond=delegate(Uri u) {
                if(u.Host!="172.16.253.3") throw new PortalContentException("认证页内容异常，已停止读取。");
                return FakePortal.Result(u,u.Port==801 ? "dr1003({\"result\":\"1\"})" : "v46ip='10.2.3.4'");
            }};
            var r=new PortalClient(t,()=> "10.2.3.4").LoginAsync("test","wrong",null,CancellationToken.None).GetAwaiter().GetResult();
            ProtocolTests.Assert(r.Accepted && !r.InternetVerified);
            ProtocolTests.Equal(4,t.Requests.Count);
        });
    }
}
