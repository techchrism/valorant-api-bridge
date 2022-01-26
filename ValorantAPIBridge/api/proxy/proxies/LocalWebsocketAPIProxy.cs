using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ValorantAPIBridge.api.proxy;

namespace ValorantAPITest.api.proxy.proxies;

public class LocalWebsocketAPIProxy : APIProxy
{
    private readonly LockfileHandler _lockfileHandler;

    public LocalWebsocketAPIProxy(LockfileHandler lockfileHandler)
    {
        _lockfileHandler = lockfileHandler;
    }

    public async void ProcessContext(HttpListenerContext context, int pathNameArg)
    {
        // 404 if any extra path data added
        if (context.Request.Url.Segments.Length > pathNameArg + 1)
        {
            context.Response.StatusCode = (int) HttpStatusCode.NotFound;
            context.Response.Close();
            return;
        }
        
        LockfileData? lockData = _lockfileHandler.LockfileData;
        if (lockData == null)
        {
            context.Response.StatusCode = (int) HttpStatusCode.BadGateway;
            context.Response.Close();
            return;
        }

        // Only accept websocket requests
        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int) HttpStatusCode.Forbidden;
            context.Response.Close();
            return;
        }
        
        var wsContext = await context.AcceptWebSocketAsync(null);
        var sourceWS = new ClientWebSocket();
        sourceWS.Options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;
        
        string url = $"wss://localhost:{lockData.Port}";
        sourceWS.Options.SetRequestHeader("Authorization", 
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("riot:" + lockData.Password)));
        await sourceWS.ConnectAsync(new Uri(url), CancellationToken.None);
        
        var sources = new[] {wsContext.WebSocket, sourceWS};
        var tasks = new Task<WebSocketData>[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            tasks[i] = WaitForData(sources[i]);
        }
        
        while (true)
        {
            try
            {
                int which = Task.WaitAny(tasks);
                var data = tasks[which].Result;
                    
                for (var i = 0; i < sources.Length; i++)
                {
                    if (data.result.MessageType == WebSocketMessageType.Close)
                    {
                        await sources[i].CloseAsync(data.result.CloseStatus.Value, data.result.CloseStatusDescription, CancellationToken.None);
                    }
                    else if (i != which)
                    {
                        await sources[i].SendAsync(data.segment, data.result.MessageType, data.result.EndOfMessage, CancellationToken.None);
                    }
                }

                if (data.result.MessageType != WebSocketMessageType.Close)
                {
                    tasks[which] = WaitForData(sources[which]);
                }
                else
                {
                    break;
                }
            }
            catch (Exception e)
            {
                foreach (var source in sources)
                {
                    if (source.State == WebSocketState.Open)
                    {
                        await source.CloseAsync(WebSocketCloseStatus.InternalServerError, "Internal exception",
                            CancellationToken.None);
                    }
                }
                break;
            }
        }
    }

    public string GetPathName()
    {
        return "websocket";
    }
    
    private async Task<WebSocketData> WaitForData(WebSocket socket)
    {
        WebSocketData data = new WebSocketData();
        var buffer = new byte[1024 * 8];
        data.segment = new ArraySegment<byte>(buffer, 0, buffer.Length);

        data.result = await socket.ReceiveAsync(data.segment, CancellationToken.None);
        data.segment = new ArraySegment<byte>(buffer, 0, data.result.Count);

        return data;
    }
    
    private class WebSocketData
    {
        public WebSocketReceiveResult result;
        public ArraySegment<byte> segment;
    }
}