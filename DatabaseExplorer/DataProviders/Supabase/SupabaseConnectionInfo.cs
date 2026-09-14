namespace DatabaseExplorer.DataProviders.Supabase;

public sealed record SupabaseConnectionInfo(string Url, string ApiKey)
{
    public static bool TryParse(string connectionString, out SupabaseConnectionInfo? info, out string? error)
    {
        info = null;
        error = null;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = "Connection string cannot be empty.";
            return false;
        }

        string? url = null;
        string? apiKey = null;

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = segment[..idx].Trim().ToLowerInvariant();
            var value = segment[(idx + 1)..].Trim();

            switch (key)
            {
                case "url":
                case "project":
                case "projecturl":
                    url = value;
                    break;
                case "apikey":
                case "key":
                case "anonkey":
                    apiKey = value;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Missing 'Url' (e.g. Url=https://your-project.supabase.co;).";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            error = "The Supabase Url is not a valid http(s) address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            error = "Missing 'ApiKey' (your Supabase publishable or secret key).";
            return false;
        }

        info = new SupabaseConnectionInfo(url.TrimEnd('/'), apiKey);
        return true;
    }

    public static (string Url, string ApiKey) ParseLenient(string connectionString)
    {
        var url = string.Empty;
        var apiKey = string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (url, apiKey);
        }

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = segment[..idx].Trim().ToLowerInvariant();
            var value = segment[(idx + 1)..].Trim();

            switch (key)
            {
                case "url":
                case "project":
                case "projecturl":
                    url = value;
                    break;
                case "apikey":
                case "key":
                case "anonkey":
                    apiKey = value;
                    break;
            }
        }

        return (url, apiKey);
    }
}