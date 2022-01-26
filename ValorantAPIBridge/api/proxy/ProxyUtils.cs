using System.Collections.Specialized;
using System.Net;
using System.Net.Http;

namespace ValorantAPIBridge.api.proxy;

public class ProxyUtils
{
    public static readonly StringCollection AllowedRegions = new();
    public static readonly StringCollection AllowedShards = new();
    public static readonly StringCollection AvoidProxyHeaders = new();

    static ProxyUtils()
    {
        AllowedRegions.Add("na");
        AllowedRegions.Add("eu");
        AllowedRegions.Add("ap");
        AllowedRegions.Add("kr");
        
        AllowedShards.Add("na");
        AllowedShards.Add("eu");
        AllowedShards.Add("ap");
        AllowedShards.Add("kr");
        
        AllowedRegions.Add("br");
        AllowedRegions.Add("latam");

        AvoidProxyHeaders.Add("Access-Control-Allow-Origin");
        AvoidProxyHeaders.Add("Access-Control-Expose-Headers");
    }
    
    public static async void ProxyToUrl(HttpListenerContext context, string url, string? authString = null, bool ignoreSSL = false)
    {
        var httpMethod = new HttpMethod(context.Request.HttpMethod);
        var request = new HttpRequestMessage(httpMethod, url);
        
        var copyHeaders = new [] {"Authorization", "Content-Type", "X-Riot-Entitlements-JWT", "X-Riot-ClientVersion"};
        foreach (var headerName in copyHeaders)
        {
            var headerValue = context.Request.Headers.Get(headerName);
            if (headerValue != null)
            {
                request.Headers.Add(headerName, headerValue);
            }
        }

        if (authString != null)
        {
            request.Headers.Add("Authorization", authString);
        }

        if (httpMethod != HttpMethod.Get &&
            httpMethod != HttpMethod.Head &&
            httpMethod != HttpMethod.Delete &&
            httpMethod != HttpMethod.Trace)
        {
            request.Content = new StreamContent(context.Request.InputStream);
        }
        
        var handler = new HttpClientHandler();
        if (ignoreSSL)
        {
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true;
        }
        var httpClient = new HttpClient(handler);
        
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead); 
                
        context.Response.StatusCode = (int) response.StatusCode;
        context.Response.Headers.Clear();
        foreach (var header in response.Headers)
        {
            if (WebHeaderCollection.IsRestricted(header.Key) || AvoidProxyHeaders.Contains(header.Key)) continue;
            foreach(var headerValue in header.Value)
            {
                context.Response.Headers.Add(header.Key, headerValue);
            }
        }
        foreach (var header in response.Content.Headers)
        {
            if (WebHeaderCollection.IsRestricted(header.Key) || AvoidProxyHeaders.Contains(header.Key)) continue;
            foreach(var headerValue in header.Value)
            {
                context.Response.Headers.Add(header.Key, headerValue);
            }
        }
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Expose-Headers", "*");
        await response.Content.CopyToAsync(context.Response.OutputStream);
        context.Response.Close();
    }
}