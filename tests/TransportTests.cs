using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AhuCampus;

public static class TransportTests
{
    public static int Main()
    {
        try {CancelStalledBody().GetAwaiter().GetResult(); Console.WriteLine("PASS real HTTP cancellation while response body stalls"); return 0;}
        catch(Exception e) {Console.WriteLine("FAIL real HTTP cancellation: "+e.Message);return 1;}
    }
    static async Task CancelStalledBody()
    {
        var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        var address=new Uri("http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/");
        TcpClient peer=null;
        var headersSent=new TaskCompletionSource<bool>();
        var server=Task.Run(async delegate {
            peer=await listener.AcceptTcpClientAsync();
            byte[] bytes=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 100\r\nConnection: close\r\n\r\nx");
            await peer.GetStream().WriteAsync(bytes,0,bytes.Length);
            headersSent.SetResult(true);
        });
        bool timely=false,cancelled=false;
        using(var transport=new HttpPortalTransport())
        using(var cancel=new CancellationTokenSource())
        {
            try
            {
                var request=transport.GetAsync(address,cancel.Token);
                await headersSent.Task;
                await Task.Delay(100);
                cancel.Cancel();
                timely=await Task.WhenAny(request,Task.Delay(1500))==request;
                if(peer!=null) peer.Close();
                try {await request;} catch(OperationCanceledException) {cancelled=true;} catch { }
                await server;
            }
            finally {if(peer!=null) peer.Close(); listener.Stop();}
        }
        if(!timely || !cancelled) throw new Exception("Cancel must interrupt the pending body read within 1.5 seconds; timely="+timely+", cancelled="+cancelled);
    }
}
