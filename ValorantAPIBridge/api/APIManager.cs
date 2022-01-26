using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ValorantAPIBridge.api.proxy;
using ValorantAPIBridge.api.proxy.proxies;
using ValorantAPIBridge.whitelist;
using ValorantAPITest;
using ValorantAPITest.api.proxy.proxies;

namespace ValorantAPIBridge.api;

public class APIManager
{
    private readonly HttpListener _httpListener;
    private readonly ProxyManager _proxyManager;
    private readonly OriginWhitelist _originWhitelist;

    private static readonly string ApiVersion = "0.1.0";
    
    public APIManager(LockfileHandler lockfileHandler, OriginWhitelist originWhitelist)
    {
        _originWhitelist = originWhitelist;
        _httpListener = new HttpListener();
        _proxyManager = new ProxyManager();
        
        _proxyManager.RegisterProxy(new PDAPIProxy());
        _proxyManager.RegisterProxy(new SharedAPIProxy());
        _proxyManager.RegisterProxy(new GLZAPIProxy());
        _proxyManager.RegisterProxy(new LocalAPIProxy(lockfileHandler));
        _proxyManager.RegisterProxy(new LocalWebsocketAPIProxy(lockfileHandler));
        _proxyManager.RegisterProxy(new LogAPIProxy());
        _proxyManager.RegisterProxy(new StatusAPIProxy(lockfileHandler));
        
        _httpListener.Prefixes.Add("http://localhost:12151/");
        _httpListener.Start();
    }
    
    public async void startListening()
    {
        while (_httpListener.IsListening)
        {
            var context = await _httpListener.GetContextAsync();
            Task.Factory.StartNew(() => processContext(context));
        }
    }

    private async void processContext(HttpListenerContext context)
    {
        Console.WriteLine(context.Request.HttpMethod + " " + context.Request.RawUrl);
        
        if (context.Request.HttpMethod == "OPTIONS")
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "*");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "*, Authorization");
            context.Response.Headers.Add("Access-Control-Max-Age", "3600");
            context.Response.Close();
        }
        else
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Expose-Headers", "*");
            
            string[] segments = context.Request.Url.Segments;
            string origin = context.Request.Headers.Get("Origin");
            bool whitelisted = origin == null || _originWhitelist.BumpIfExists(origin);
            
            if (segments.Length <= 1)
            {
                if (context.Request.IsWebSocketRequest)
                {
                    HandleRootWebsocket(context);
                }
                else
                {
                    string whitelistedStr = whitelisted ? "true" : "false";
                    context.Response.ContentEncoding = Encoding.UTF8;
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = (int) HttpStatusCode.OK;
                    context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(
                        $"{{\"whitelisted\": {whitelistedStr}, \"version\": \"{ApiVersion}\"}}"));
                    context.Response.Close();
                }
            }
            else
            {
                if (segments[1] == "proxy/")
                {
                    if (whitelisted)
                    {
                        _proxyManager.ProcessContext(context, 1);
                    }
                    else
                    {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }
    }

    private async void HandleRootWebsocket(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        
        string origin = context.Request.Headers.Get("Origin");
        bool whitelisted = origin == null || _originWhitelist.BumpIfExists(origin);
        string whitelistedStr = whitelisted ? "true" : "false";
        await wsContext.WebSocket.SendAsync(Encoding.UTF8.GetBytes(
            $"{{\"action\": \"init\", \"whitelisted\": {whitelistedStr}, \"version\": \"{ApiVersion}\"}}"), WebSocketMessageType.Text, true, CancellationToken.None);
        
        var buffer = new byte[1024 * 32];
        while (true)
        {
            try
            {
                var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
                var result = await wsContext.WebSocket.ReceiveAsync(segment, CancellationToken.None);
                segment = new ArraySegment<byte>(buffer, 0, result.Count);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                
                if (!result.EndOfMessage)
                {
                    await wsContext.WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "passed 32kb max", CancellationToken.None);
                    break;
                }

                JsonObject obj = JsonNode.Parse(segment).AsObject();
                if (!obj.ContainsKey("action"))
                {
                    continue;
                }

                string action = (string) obj["action"];
                if (action == "request" &&
                    !_originWhitelist.IsWhitelisted(origin) &&
                    obj.ContainsKey("name"))
                {
                    _originWhitelist.RequestWhitelist(new WhitelistRequest
                    {
                        Name = (string)obj["name"],
                        Origin = origin
                    }, response =>
                    {
                        if (wsContext.WebSocket.State == WebSocketState.Open)
                        {
                            string responseStr = response ? "true" : "false";
                            wsContext.WebSocket.SendAsync(Encoding.UTF8.GetBytes(
                                $"{{\"action\": \"requestResponse\", \"response\": {responseStr}}}"), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    });
                }
            }
            catch (Exception e)
            {
                throw e;
                break;
            }
        }
    }
}