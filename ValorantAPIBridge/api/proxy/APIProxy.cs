using System.Net;

namespace ValorantAPIBridge.api.proxy;

public interface APIProxy
{
    public void ProcessContext(HttpListenerContext context, int pathNameArg);
    public string GetPathName();
}