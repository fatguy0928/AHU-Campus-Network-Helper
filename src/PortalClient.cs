using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AhuCampus
{
    public sealed class HttpResult
    {
        public int Status;
        public string Body;
        public Uri Uri;
        public string Location;
    }
    public interface IPortalTransport
    {
        Task<HttpResult> GetAsync(Uri uri, CancellationToken token);
    }
    public sealed class PortalContentException : InvalidOperationException
    {
        public PortalContentException(string message) : base(message) { }
    }
    public sealed class LoginOutcome
    {
        public bool Accepted;
        public bool InternetVerified;
        public string Message;
        public string Ip;
    }
    public sealed class PortalClient
    {
        readonly IPortalTransport transport;
        readonly Func<string> getRouteIp;
        public PortalClient(IPortalTransport transport, Func<string> getRouteIp) { this.transport=transport; this.getRouteIp=getRouteIp; }
        public async Task<LoginOutcome> LoginAsync(string account, string password, IProgress<string> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(account) || String.IsNullOrEmpty(password)) throw new InvalidOperationException("请输入校园网账号和密码。");
            Report(progress, "正在读取校园网认证页面…");
            HttpResult page = null;
            Uri pageUri = new Uri("http://172.16.253.3/a79.htm?_=" + DateTime.UtcNow.Ticks);
            for (int redirects=0; redirects<4; redirects++)
            {
                page = await transport.GetAsync(pageUri,token).ConfigureAwait(false);
                if (page.Status<300 || page.Status>=400) break;
                Uri next;
                if (!Uri.TryCreate(pageUri, page.Location ?? "", out next) || next.Host!="172.16.253.3" || next.Scheme!="http" || (next.Port!=80 && next.Port!=801))
                    throw new InvalidOperationException("认证页跳转到非预期地址，已停止。请打开学校网页登录检查。");
                pageUri = next;
            }
            if (page == null || page.Status!=200) throw new InvalidOperationException("无法读取学校认证页，请连接校园网后重试。");
            string html=page.Body ?? "";
            if (html.IndexOf("v46ip",StringComparison.OrdinalIgnoreCase)<0 && html.IndexOf("Dr.COM",StringComparison.OrdinalIgnoreCase)<0 && html.IndexOf("wlanuserip",StringComparison.OrdinalIgnoreCase)<0)
                throw new InvalidOperationException("当前返回的页面不是预期的校园网认证页，请打开网页登录检查。");
            string routeIp="";
            try { routeIp=getRouteIp(); } catch (SocketException) { }
            var context=PortalProtocol.ReadContext(html,page.Uri ?? pageUri,routeIp);
            Report(progress,"本机校园网 IP：" + context.Ip);
            token.ThrowIfCancellationRequested();
            Report(progress,"正在提交认证…");
            var response=await transport.GetAsync(PortalProtocol.BuildLoginUri(account,password,context),token).ConfigureAwait(false);
            if (response.Status!=200)
                return new LoginOutcome {Ip=context.Ip,Message="认证接口返回 HTTP " + response.Status + "，请打开学校网页登录检查。"};
            var reply=PortalProtocol.ParseReply(response.Body,account,password);
            var outcome=new LoginOutcome {Accepted=reply.Accepted,Ip=context.Ip,Message=reply.Message};
            if (!reply.Accepted) return outcome;
            Report(progress,reply.Message);
            Report(progress,"正在检测外网连通性…");
            foreach (string address in new[] {"http://www.msftconnecttest.com/connecttest.txt","https://www.msftconnecttest.com/connecttest.txt"})
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var probe=await transport.GetAsync(new Uri(address),token).ConfigureAwait(false);
                    if (probe.Status==200 && (probe.Body ?? "").Trim()=="Microsoft Connect Test")
                    { outcome.InternetVerified=true; break; }
                }
                catch (HttpRequestException) { }
                catch (IOException) { }
                catch (PortalContentException) { }
                catch (OperationCanceledException) { token.ThrowIfCancellationRequested(); }
            }
            outcome.Message=outcome.InternetVerified ? "已连接，外网检测通过。" : reply.Message + " 外网检测未通过，可尝试打开网页确认。";
            return outcome;
        }
        static void Report(IProgress<string> progress, string message) { if (progress!=null) progress.Report(message); }
    }

    public sealed class HttpPortalTransport : IPortalTransport, IDisposable
    {
        readonly HttpClient client;
        public HttpPortalTransport()
        {
            var handler=new HttpClientHandler {UseProxy=false, AllowAutoRedirect=false, UseCookies=false};
            client=new HttpClient(handler);
            client.Timeout=TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AHUCampusHelper/1.0");
            client.DefaultRequestHeaders.CacheControl=new System.Net.Http.Headers.CacheControlHeaderValue {NoCache=true,NoStore=true};
        }
        public async Task<HttpResult> GetAsync(Uri uri, CancellationToken token)
        {
            using (var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var request=new HttpRequestMessage(HttpMethod.Get,uri))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                if (uri.Host=="172.16.253.3") request.Headers.Referrer=new Uri("http://172.16.253.3/");
                using (var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false))
                using (timeout.Token.Register(delegate { response.Dispose(); }))
                try
                {
                using (var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var buffer=new MemoryStream())
                {
                    byte[] chunk=new byte[8192];
                    int read;
                    while ((read=await stream.ReadAsync(chunk,0,chunk.Length,timeout.Token).ConfigureAwait(false))>0)
                    {
                        if (buffer.Length+read>512*1024) throw new PortalContentException("认证页内容异常，已停止读取。");
                        buffer.Write(chunk,0,read);
                    }
                    byte[] bytes=buffer.ToArray();
                    Encoding encoding=new UTF8Encoding(false,false);
                    string charset=response.Content.Headers.ContentType==null ? "" : response.Content.Headers.ContentType.CharSet;
                    if (!String.IsNullOrEmpty(charset))
                    { try { encoding=Encoding.GetEncoding(charset.Trim('"','\'')); } catch (ArgumentException) { } }
                    else
                    {
                        string preview=Encoding.ASCII.GetString(bytes,0,Math.Min(4096,bytes.Length));
                        if (preview.IndexOf("gb2312",StringComparison.OrdinalIgnoreCase)>=0 || preview.IndexOf("gbk",StringComparison.OrdinalIgnoreCase)>=0)
                            encoding=Encoding.GetEncoding(936);
                    }
                    return new HttpResult {Status=(int)response.StatusCode,Body=encoding.GetString(bytes),Uri=uri,
                        Location=response.Headers.Location==null ? null : response.Headers.Location.ToString()};
                }
                }
                catch
                {
                    // Disposing a stalled response can surface as IOException/ObjectDisposedException.
                    // Normalize those to cancellation so the UI reports timeout/cancel accurately.
                    timeout.Token.ThrowIfCancellationRequested();
                    throw;
                }
            }
        }
        public static string GetRouteIp()
        {
            using (var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp))
            {
                // UDP Connect selects a route; no application packet is sent.
                socket.Connect(new IPEndPoint(IPAddress.Parse("172.16.253.3"),801));
                return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
        }
        public void Dispose() { client.Dispose(); }
    }
}
