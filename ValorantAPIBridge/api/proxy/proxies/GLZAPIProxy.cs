using System.Net;

namespace ValorantAPIBridge.api.proxy.proxies;

public class GLZAPIProxy : APIProxy
{
    public void ProcessContext(HttpListenerContext context, int pathNameArg)
    {
        string[] segments = context.Request.Url.Segments;
        
        if (segments.Length == pathNameArg + 2)
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        string region = segments[pathNameArg + 1].TrimEnd('/');
        string shard = segments[pathNameArg + 2].TrimEnd('/');

        if (!ProxyUtils.AllowedRegions.Contains(region) || !ProxyUtils.AllowedShards.Contains(shard))
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        int removeLen = 0;
        for (int i = 0; i <= pathNameArg + 2; i++)
        {
            removeLen += segments[i].Length;
        }
        
        string url = $"https://glz-{region}-1.{shard}.a.pvp.net/{context.Request.Url.PathAndQuery.Substring(removeLen)}";
        ProxyUtils.ProxyToUrl(context, url);
    }

    public string GetPathName()
    {
        return "glz";
    }
}