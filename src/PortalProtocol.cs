using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;

namespace AhuCampus
{
    public sealed class PortalContext
    {
        public string Ip;
        public string AcIp = "172.16.253.1";
        public string AcName = "";
    }

    public sealed class PortalReply
    {
        public bool Accepted;
        public bool AlreadyOnline;
        public string Message;
    }

    public static class PortalProtocol
    {
        public static PortalContext ReadContext(string html, Uri uri, string routeIp)
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            var context = new PortalContext();
            foreach (string key in new[] { "v46ip", "wlanuserip", "wlan_user_ip", "ss5" })
            {
                var match = Regex.Match(html ?? "", @"\b" + key + @"\s*=\s*['""]\s*([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (match.Success && IsCampusIp(match.Groups[1].Value.Trim())) { context.Ip = match.Groups[1].Value.Trim(); break; }
            }
            if (context.Ip == null)
                foreach (string ip in new[] { query["wlanuserip"], query["wlan_user_ip"], routeIp })
                    if (IsCampusIp(ip)) { context.Ip = ip.Trim(); break; }
            if (context.Ip == null) throw new InvalidOperationException("未获取到校园网 IPv4，请先连接校园 Wi-Fi 或网线后重试。");
            string acIp = query["wlanacip"] ?? query["wlan_ac_ip"];
            IPAddress parsed;
            if (!String.IsNullOrWhiteSpace(acIp) && IPAddress.TryParse(acIp, out parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                context.AcIp = parsed.ToString();
            context.AcName = query["wlanacname"] ?? query["wlan_ac_name"] ?? "";
            return context;
        }

        public static bool IsCampusIp(string value)
        {
            IPAddress ip;
            if (String.IsNullOrWhiteSpace(value) || !IPAddress.TryParse(value.Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
        }

        public static Uri BuildLoginUri(string account, string password, PortalContext context)
        {
            if (String.IsNullOrWhiteSpace(account) || String.IsNullOrEmpty(password))
                throw new InvalidOperationException("请输入校园网账号和密码。");
            if (context == null || !IsCampusIp(context.Ip))
                throw new InvalidOperationException("校园网 IP 无效，请重试。");
            var fields = new Dictionary<string, string> {
                {"c","Portal"}, {"a","login"}, {"callback","dr1003"}, {"login_method","1"},
                {"user_account",account.Trim()}, {"user_password",password},
                {"wlan_user_ip",context.Ip}, {"wlan_user_ipv6",""}, {"wlan_user_mac","000000000000"},
                {"wlan_ac_ip",context.AcIp}, {"wlan_ac_name",context.AcName},
                {"jsVersion","3.3.2"}, {"v",DateTime.UtcNow.Ticks.ToString()}
            };
            var pairs = new List<string>();
            foreach (var field in fields) pairs.Add(field.Key + "=" + Uri.EscapeDataString(field.Value ?? ""));
            return new Uri("http://172.16.253.3:801/eportal/?" + String.Join("&", pairs));
        }

        public static PortalReply ParseReply(string body, string account, string password)
        {
            var reply = new PortalReply { Message = "认证服务器返回了无法识别的内容，请打开网页登录检查。" };
            string json = (body ?? "").Trim().TrimStart('\uFEFF');
            var jsonp = Regex.Match(json, @"^\s*[A-Za-z_$][\w$]*\s*\(\s*(\{[\s\S]*\})\s*\)\s*;?\s*$");
            if (jsonp.Success) json = jsonp.Groups[1].Value;
            if (!json.StartsWith("{") || json.Length > 65536) return reply;
            try
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                object result, code, msg;
                data.TryGetValue("result", out result);
                data.TryGetValue("ret_code", out code);
                data.TryGetValue("msg", out msg);
                reply.AlreadyOnline = Convert.ToString(code) == "2";
                reply.Accepted = Convert.ToString(result) == "1" || reply.AlreadyOnline;
                if (reply.Accepted) reply.Message = reply.AlreadyOnline ? "认证服务器报告当前 IP 已在线。" : "校园网认证成功。";
                else
                {
                    string message = Redact(HttpUtility.HtmlDecode(Convert.ToString(msg)), account, password);
                    message = Regex.Replace(message, @"<[^>]*>|[\x00-\x1F]", " ").Trim();
                    reply.Message = message.Length == 0 ? "认证未通过，请检查账号密码后重试。" : message.Substring(0, Math.Min(240, message.Length));
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            return reply;
        }

        public static string Redact(string text, string account, string password)
        {
            string value = text ?? "";
            // Replace longest secrets first so overlapping account/password values cannot leak a suffix.
            var secrets = new List<string>();
            foreach (var secret in new[] { account, password })
                if (!String.IsNullOrEmpty(secret)) { secrets.Add(secret); secrets.Add(Uri.EscapeDataString(secret)); secrets.Add(HttpUtility.UrlEncode(secret)); }
            secrets.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            foreach (string secret in secrets)
                value = Regex.Replace(value, Regex.Escape(secret), "***", RegexOptions.IgnoreCase);
            return value;
        }
    }
}
