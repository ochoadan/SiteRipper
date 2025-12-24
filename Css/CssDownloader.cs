namespace SiteRipper.Css;

public class CssDownloader
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _cache = new();
    private readonly int _maxFileSizeBytes;

    public CssDownloader(int maxFileSizeBytes = 5 * 1024 * 1024)  // 5MB default
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _maxFileSizeBytes = maxFileSizeBytes;
    }

    public async Task<string?> DownloadAsync(string url, Uri baseUri)
    {
        try
        {
            var absoluteUrl = MakeAbsolute(url, baseUri);
            if (string.IsNullOrEmpty(absoluteUrl)) return null;

            // Check cache
            if (_cache.TryGetValue(absoluteUrl, out var cached))
                return cached;

            // Download
            using var response = await _httpClient.GetAsync(absoluteUrl, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return null;

            // Check content length
            if (response.Content.Headers.ContentLength > _maxFileSizeBytes)
                return null;

            var content = await response.Content.ReadAsStringAsync();

            // Cache it
            _cache[absoluteUrl] = content;

            return content;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<CssFileContent>> DownloadAllAsync(
        IEnumerable<string> stylesheetUrls,
        Uri baseUri)
    {
        var results = new List<CssFileContent>();
        var tasks = new List<Task<CssFileContent?>>();

        foreach (var url in stylesheetUrls.Take(20))  // Limit to 20 stylesheets
        {
            tasks.Add(DownloadWithMetadataAsync(url, baseUri));
        }

        var completed = await Task.WhenAll(tasks);

        foreach (var result in completed)
        {
            if (result != null)
                results.Add(result);
        }

        return results;
    }

    private async Task<CssFileContent?> DownloadWithMetadataAsync(string url, Uri baseUri)
    {
        var content = await DownloadAsync(url, baseUri);
        if (content == null) return null;

        return new CssFileContent
        {
            Url = MakeAbsolute(url, baseUri) ?? url,
            FileName = GetFileName(url),
            Content = content
        };
    }

    private string? MakeAbsolute(string url, Uri baseUri)
    {
        try
        {
            if (url.StartsWith("data:")) return null;
            if (url.StartsWith("//")) return $"https:{url}";

            var uri = new Uri(baseUri, url);
            return uri.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string GetFileName(string url)
    {
        try
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            var path = uri.IsAbsoluteUri ? uri.AbsolutePath : url;
            return Path.GetFileName(path);
        }
        catch
        {
            return "unknown.css";
        }
    }
}

public class CssFileContent
{
    public string Url { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
}
