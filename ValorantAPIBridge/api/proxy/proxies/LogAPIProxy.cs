using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using ValorantAPIBridge.api.proxy;

namespace ValorantAPITest.api.proxy.proxies;

public class LogAPIProxy : APIProxy
{
    private static readonly string LogPath;

    static LogAPIProxy()
    {
        LogPath = Environment.GetEnvironmentVariable("LOCALAPPDATA") + @"\VALORANT\Saved\Logs\ShooterGame.log";
    }

    private List<WebSocket> _listeningWebsockets = new();
    private CancellationTokenSource? _checkCancellationTokenSource;

    public async void ProcessContext(HttpListenerContext context, int pathNameArg)
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

        // Send the log file
        var fileInfo = new FileInfo(LogPath);
        if (!fileInfo.Exists)
        {
            context.Response.StatusCode = (int) HttpStatusCode.NoContent;
            context.Response.Close();
            return;
        }
        
        using (var fileStream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            context.Response.StatusCode = (int) HttpStatusCode.OK;
            fileStream.CopyTo(context.Response.OutputStream);
            context.Response.Close();
        }
    }

    private async void ProcessWebsocketRequest(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        _listeningWebsockets.Add(wsContext.WebSocket);

        // Start the log watcher if this is the first connection
        if (_listeningWebsockets.Count == 1)
        {
            _checkCancellationTokenSource = new();
            Task.Factory.StartNew(() =>
                CheckLog(TimeSpan.FromMilliseconds(100), _checkCancellationTokenSource.Token));
        }
            
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
            
        // Stop the log watcher if this was the last connection
        if (_listeningWebsockets.Count == 0)
        {
            _checkCancellationTokenSource.Cancel();
            _checkCancellationTokenSource.Dispose();
        }
    }

    public string GetPathName()
    {
        return "log";
    }
    
    private async Task CheckLog(TimeSpan interval, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        long oldLength = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            await Task.Delay(interval, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            var fileInfo = new FileInfo(LogPath);
            if (!fileInfo.Exists)
            {
                oldLength = 0;
                continue;
            }
            long newLength = fileInfo.Length;
            if (oldLength == 0 || oldLength > newLength)
            {
                oldLength = newLength;
            }
            else
            {
                long diff = newLength - oldLength;
                if (diff == 0)
                {
                    continue;
                }
                
                using (var fileStream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Read the latest segment of the file
                    byte[] buffer = new byte[diff];
                    fileStream.Seek(oldLength, SeekOrigin.Begin);
                    fileStream.Read(buffer, 0, (int) diff);
                    
                    // Send to all connected websockets
                    List<Task> sendTasks = new();
                    foreach (var socket in _listeningWebsockets)
                    {
                        if (socket.State == WebSocketState.Open)
                        {
                            sendTasks.Add(socket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken));
                        }
                    }

                    Task.WaitAll(sendTasks.ToArray());
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
                oldLength = newLength;
            }
        }
    }
}