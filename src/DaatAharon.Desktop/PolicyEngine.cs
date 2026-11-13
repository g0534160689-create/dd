using System.Globalization;

namespace DaatAharon.Desktop;

/// <summary>Centralized, fail-closed URL policy evaluation.</summary>
public static class PolicyEngine
{
    public static bool IsAllowed(Uri uri, LocalPolicy policy)
    {
        if (uri is null || policy is null || !uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        string host;
        try { host = new IdnMapping().GetAscii(uri.IdnHost.TrimEnd('.')).ToLowerInvariant(); }
        catch { return false; }

        if (host == "pay.google.com" || host.EndsWith(".pay.google.com", StringComparison.Ordinal) ||
            host == "payments.google.com" || host.EndsWith(".payments.google.com", StringComparison.Ordinal))
            return false;

        string? path = NormalizePath(uri.AbsolutePath);
        if (path is null) return false;

        var matching = policy.Routes
            .Where(rule => HostMatches(host, rule.Host) && PathMatches(path, rule.Path))
            .OrderByDescending(rule => HostSpecificity(rule.Host))
            .ThenByDescending(rule => NormalizePath(rule.Path.TrimEnd('*'))?.Length ?? 0)
            .ThenBy(rule => rule.Allowed) // deny wins when specificity is identical
            .FirstOrDefault();
        if (matching is not null) return matching.Allowed;

        if (host == "ravkavonline.co.il")
            return path.Equals("/he/store/connect", StringComparison.OrdinalIgnoreCase);

        if (host == "hopon.co.il" || host == "www.hopon.co.il")
        {
            string[] blocked = ["/charging-stations-map", "/about", "/ravkav", "/freeMonthly",
                "/freeMonthlyOtef", "/accessibility", "/map", "/student"];
            return !blocked.Any(item => PathMatches(path, item));
        }

        return policy.AllowedHosts.Any(allowed => HostMatches(host, allowed));
    }

    public static string? NormalizePath(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 8192 || value.Contains('\\'))
                return null;
            string path = value;
            for (int i = 0; i < 8 && path.Contains('%'); i++)
            {
                string decoded = Uri.UnescapeDataString(path);
                if (decoded == path) break;
                path = decoded;
            }
            if (path.Contains('%') || path.Any(char.IsControl)) return null;
            var parts = new List<string>();
            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0) continue;
                if (part == "." || part == "..") return null;
                parts.Add(part);
            }
            return "/" + string.Join("/", parts);
        }
        catch { return null; }
    }

    private static bool HostMatches(string actual, string configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return false;
        string rule = configured.Trim().TrimEnd('.').ToLowerInvariant();
        if (rule.StartsWith("*.", StringComparison.Ordinal))
        {
            string suffix = rule[2..];
            return actual.Length > suffix.Length && actual.EndsWith("." + suffix, StringComparison.Ordinal);
        }
        return actual.Equals(rule, StringComparison.OrdinalIgnoreCase);
    }

    private static int HostSpecificity(string host) => host.StartsWith("*.", StringComparison.Ordinal) ? host.Length - 2 : host.Length + 10000;

    private static bool PathMatches(string actual, string configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return false;
        bool wildcard = configured.EndsWith('*');
        string root = NormalizePath(wildcard ? configured[..^1] : configured) ?? "/";
        if (root == "/") return true;
        return actual.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               actual.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) ||
               (wildcard && actual.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }
}
