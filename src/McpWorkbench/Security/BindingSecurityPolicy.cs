namespace A2G.McpWorkbench.Security;

internal enum BindingSecurityResult { Loopback, RemoteProtected, RemoteUnprotected, RemoteRejected }

internal static class BindingSecurityPolicy
{
    public static BindingSecurityResult Evaluate(IEnumerable<string> urls, bool loopbackOnly, bool hasApiKey)
    {
        var remote = urls.Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              (System.Net.IPAddress.TryParse(uri.Host, out var address) && System.Net.IPAddress.IsLoopback(address))));
        if (!remote) return BindingSecurityResult.Loopback;
        if (loopbackOnly) return BindingSecurityResult.RemoteRejected;
        return hasApiKey ? BindingSecurityResult.RemoteProtected : BindingSecurityResult.RemoteUnprotected;
    }
}
