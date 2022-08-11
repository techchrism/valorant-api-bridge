using System;
using System.Collections.Generic;
using System.Net;

namespace ValorantAPIBridge.api.proxy;

public class ProxyManager
{
    private Dictionary<String, APIProxy> _registeredProxies = new();

    public void RegisterProxy(APIProxy proxy)
    {
        _registeredProxies[proxy.GetPathName()] = proxy;
    }

    /// <summary>
    /// Processes a context to a proxy endpoint
    /// </summary>
    /// <param name="context"></param>
    /// <param name="proxyPathArg">The index of the segments array in which the proxy endpoint appears</param>
    public void ProcessContext(HttpListenerContext context, int proxyPathArg)
    {
        string[] segments = context.Request.Url.Segments;
        
        // Nothing on root proxy endpoint
        if (segments.Length <= proxyPathArg - 1)
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        string proxyName = segments[proxyPathArg + 1].TrimEnd('/');
        bool proxyExists = _registeredProxies.TryGetValue(proxyName, out APIProxy? proxy);
        
        if (proxyExists)
        {
            proxy?.ProcessContext(context, proxyPathArg + 1);
        }
        else
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }
}