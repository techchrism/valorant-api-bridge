using System;
using System.IO;
using System.Net;
using System.Text;
using ValorantAPIBridge.api.proxy;

namespace ValorantAPITest.api.proxy.proxies;

public class StatusAPIProxy : APIProxy
{
    private readonly LockfileHandler _lockfileHandler;

    public StatusAPIProxy(LockfileHandler lockfileHandler)
    {
        _lockfileHandler = lockfileHandler;
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
}