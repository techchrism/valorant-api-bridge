using System;
using System.Net;
using System.Text;
using ValorantAPITest;

namespace ValorantAPIBridge.api.proxy.proxies;

public class LocalAPIProxy : APIProxy
{
    private readonly LockfileHandler _lockfileHandler;

    public LocalAPIProxy(LockfileHandler lockfileHandler)
    {
        _lockfileHandler = lockfileHandler;
    }

    public void ProcessContext(HttpListenerContext context, int pathNameArg)
    {
        LockfileData? data = _lockfileHandler.LockfileData;
        if (data == null)
        {
            context.Response.StatusCode = (int) HttpStatusCode.BadGateway;
            context.Response.Close();
            return;
        }
        
        int removeLen = 0;
        for (int i = 0; i <= pathNameArg; i++)
        {
            removeLen += context.Request.Url.Segments[i].Length;
        }

        string url = $"https://127.0.0.1:{data.Port}/{context.Request.Url.PathAndQuery.Substring(removeLen)}";
        string authStr = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("riot:" + data.Password));
        
        ProxyUtils.ProxyToUrl(context, url, authStr, true);
    }

    public string GetPathName()
    {
        return "local";
    }
}