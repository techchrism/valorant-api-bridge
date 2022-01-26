using System.Collections.Specialized;
using System.Net;

namespace ValorantAPIBridge.api.proxy.proxies;

public class PDAPIProxy : APIProxy
{
    public void ProcessContext(HttpListenerContext context, int pathNameArg)
    {
        string[] segments = context.Request.Url.Segments;
        
        if (segments.Length == pathNameArg + 1)
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        string shard = segments[pathNameArg + 1].TrimEnd('/');

        if (!ProxyUtils.AllowedShards.Contains(shard))
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        int removeLen = 0;
        for (int i = 0; i <= pathNameArg + 1; i++)
        {
            removeLen += segments[i].Length;
        }
        
        string url = $"https://pd.{shard}.a.pvp.net/{context.Request.Url.PathAndQuery.Substring(removeLen)}";
        ProxyUtils.ProxyToUrl(context, url);
    }

    public string GetPathName()
    {
        return "pd";
    }
}