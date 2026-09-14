using System;
using System.Web;
using AhuCampus;

public static class ProtocolTests
{
    static int failed;
    public static void Check(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    public static void Equal(object expected, object actual)
    {
        if (!Object.Equals(expected, actual)) throw new Exception("Expected " + expected + "; got " + actual);
    }
    public static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    public static void Run()
    {
        Check("uses fresh portal IP instead of stale URL or virtual route", delegate {
            var c = PortalProtocol.ReadContext("v46ip='10.22.2.60' ;", new Uri("http://172.16.253.3/a79.htm?wlanuserip=10.22.2.59&wlanacip=172.16.253.1"), "198.18.0.1");
            Equal("10.22.2.60", c.Ip); Equal("172.16.253.1", c.AcIp);
        });
        Check("uses fresh redirect context and decodes AC name", delegate {
            var c = PortalProtocol.ReadContext("", new Uri("http://172.16.253.3/a79.htm?wlanuserip=10.22.5.2&wlanacip=172.16.253.8&wlanacname=AC%20A"), "192.168.1.2");
            Equal("10.22.5.2", c.Ip); Equal("172.16.253.8", c.AcIp); Equal("AC A", c.AcName);
        });
        Check("falls back to valid campus route", delegate {
            Equal("172.21.4.2", PortalProtocol.ReadContext("v46ip='0.0.0.0'", new Uri("http://172.16.253.3/a79.htm"), "172.21.4.2").Ip);
        });
        Check("refuses loopback and proxy IP instead of sending credentials", delegate {
            bool threw = false; try { PortalProtocol.ReadContext("", new Uri("http://172.16.253.3/a79.htm"), "198.18.0.1"); } catch (InvalidOperationException) { threw = true; } Assert(threw);
        });
        Check("encodes credentials without query injection or trimming password", delegate {
            var uri = PortalProtocol.BuildLoginUri("U123@xyw", " A+&?#=中文 ", new PortalContext {Ip="10.22.2.60"});
            var q = HttpUtility.ParseQueryString(uri.Query);
            Equal("172.16.253.3", uri.Host); Equal(801, uri.Port); Equal("/eportal/", uri.AbsolutePath);
            Equal("Portal", q["c"]); Equal("login", q["a"]); Equal("U123@xyw", q["user_account"]); Equal(" A+&?#=中文 ", q["user_password"]); Equal("10.22.2.60", q["wlan_user_ip"]);
        });
        Check("accepts JSONP success", delegate { Assert(PortalProtocol.ParseReply("dr1003({\"result\":\"1\",\"msg\":\"登录成功\"});", "u", "p").Accepted); });
        Check("accepts numeric JSON success", delegate { Assert(PortalProtocol.ParseReply("{\"result\":1}", "u", "p").Accepted); });
        Check("tracks already-online separately", delegate { var r=PortalProtocol.ParseReply("dr1003({\"result\":\"0\",\"msg\":\"\",\"ret_code\":2})", "u", "p"); Assert(r.Accepted && r.AlreadyOnline); });
        Check("never infers success from an error message containing success", delegate { Assert(!PortalProtocol.ParseReply("dr1003({\"result\":\"0\",\"msg\":\"not success\"})", "u", "p").Accepted); });
        Check("rejects HTML instead of reporting connected", delegate { Assert(!PortalProtocol.ParseReply("<html>login required</html>", "u", "p").Accepted); });
        Check("redacts raw and encoded secrets from gateway messages", delegate {
            string s=PortalProtocol.Redact("abc123 s&+密 s%26%2B%E5%AF%86 s%26%2b%e5%af%86", "abc123", "s&+密");
            Assert(!s.Contains("abc123") && !s.Contains("s&+密") && s.IndexOf("%E5",StringComparison.OrdinalIgnoreCase)<0);
        });
    }
    public static int Main() { Run(); ClientTests.Run(); CredentialTests.Run(); Console.WriteLine("Failures: " + failed); return failed == 0 ? 0 : 1; }
}
