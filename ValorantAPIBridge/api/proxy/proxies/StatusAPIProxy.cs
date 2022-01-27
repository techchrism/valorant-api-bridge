using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ValorantAPIBridge.api.proxy;

namespace ValorantAPITest.api.proxy.proxies;

public class StatusAPIProxy : APIProxy
{
    private readonly LockfileHandler _lockfileHandler;
    private readonly List<WebSocket> _listeningWebsockets = new();

    public StatusAPIProxy(LockfileHandler lockfileHandler)
    {
        _lockfileHandler = lockfileHandler;
        _lockfileHandler.LockfileUpdate += HandleLockfileUpdate;
        _lockfileHandler.LockfileRemove += () => { HandleLockfileUpdate(null); };
    }

    public void ProcessContext(HttpListenerContext context, int pathNameArg)
    {
        // 404 if any extra path data added
        if (context.Request.Url.Segments.Length > pathNameArg + 1)
        {
            context.Response.StatusCode = (int) HttpStatusCode.NotFound;
            context.Response.Close();
            return;
        }
        
        // Process websocket requests to stream updates
        if (context.Request.IsWebSocketRequest)
        {
            ProcessWebsocketRequest(context);
            return;
        }
        
        var logInfo = new FileInfo(Environment.GetEnvironmentVariable("LOCALAPPDATA") + @"\VALORANT\Saved\Logs\ShooterGame.log");
        long logSize = logInfo.Exists ? logInfo.Length : 0;
        string lockfileReady = _lockfileHandler.LockfileData != null ? "true" : "false";
        
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int) HttpStatusCode.OK;
        context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(
            $"{{\"lockfileReady\": {lockfileReady}, \"logSize\": {logSize}}}"));
        context.Response.Close();
    }

    public string GetPathName()
    {
        return "status";
    }

    private void HandleLockfileUpdate(LockfileData? data)
    {
        string lockfileReady = (data == null) ? "false" : "true";
        var buffer = Encoding.UTF8.GetBytes($"{{\"lockfileReady\": {lockfileReady}}}");
        
        // Send to all connected websockets
        List<Task> sendTasks = new();
        foreach (var socket in _listeningWebsockets)
        {
            if (socket.State == WebSocketState.Open)
            {
                sendTasks.Add(socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None));
            }
        }

        Task.WaitAll(sendTasks.ToArray());
    }
    
    private async void ProcessWebsocketRequest(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        _listeningWebsockets.Add(wsContext.WebSocket);
        
        // Wait for websocket closure
        var buffer = new byte[1024 * 8];
        while (true)
        {
            try
            {
                var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
                var result = await wsContext.WebSocket.ReceiveAsync(segment, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                break;
            }
        }

        _listeningWebsockets.Remove(wsContext.WebSocket);
    }
}