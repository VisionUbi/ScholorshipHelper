using System.Text.RegularExpressions;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface IContentAggregationService
{
    Task<IReadOnlyList<AggregatedSource>> FetchSourcesAsync(IReadOnlyList<ResearchSourceResult> sources, CancellationToken cancellationToken);
}

public sealed class ContentAggregationService(HttpClient httpClient, ILogger<ContentAggregationService> logger) : IContentAggregationService
{
    public async Task<IReadOnlyList<AggregatedSource>> FetchSourcesAsync(IReadOnlyList<ResearchSourceResult> sources, CancellationToken cancellationToken)
    {
        var results = new List<AggregatedSource>();
        foreach (var source in sources.Take(20))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0 Safari/537.36");
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(25));
                using var response = await httpClient.SendAsync(request, timeout.Token);
                var html = await response.Content.ReadAsStringAsync(timeout.Token);
                var text = CleanHtml(html);
                var blocked = LooksBlocked(text, html);
                results.Add(new AggregatedSource(
                    source.Url,
                    ExtractTitle(html, source.Title),
                    source.SourceType,
                    blocked ? "" : text,
                    response.IsSuccessStatusCode && !blocked,
                    blocked ? "Blocked or anti-bot page returned instead of official content." : response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}"));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not fetch source {Url}", source.Url);
                results.Add(new AggregatedSource(source.Url, source.Title, source.SourceType, "", false, ex.Message));
            }
        }

        return results;
    }

    private static string ExtractTitle(string html, string fallback)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return fallback;
        var title = Clean(match.Groups[1].Value);
        return IsGenericBlockedTitle(title) ? fallback : title;
    }

    private static string CleanHtml(string html)
    {
        html = Regex.Replace(html ?? "", "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<[^>]+>", " ");
        html = System.Net.WebUtility.HtmlDecode(html);
        return Clean(html);
    }

    private static string Clean(string value) => Regex.Replace(value ?? "", @"\s+", " ").Trim();

    private static bool LooksBlocked(string text, string html)
    {
        var value = $"{text} {html}".ToLowerInvariant();
        return value.Contains("just a moment") ||
               value.Contains("checking your browser") ||
               value.Contains("cloudflare") ||
               value.Contains("enable javascript and cookies");
    }

    private static bool IsGenericBlockedTitle(string title)
    {
        return title.Equals("Just a moment...", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Attention Required", StringComparison.OrdinalIgnoreCase);
    }
}
