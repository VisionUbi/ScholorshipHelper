using System.Text.RegularExpressions;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface IOfficialSourceSearchService
{
    Task<IReadOnlyList<ResearchSourceResult>> FindSourcesAsync(ScholarshipAnalyzeRequest request, CancellationToken cancellationToken);
}

public sealed class OfficialSourceSearchService(HttpClient httpClient, ILogger<OfficialSourceSearchService> logger) : IOfficialSourceSearchService
{
    public async Task<IReadOnlyList<ResearchSourceResult>> FindSourcesAsync(ScholarshipAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var input = request.Url?.Trim() ?? "";
        var sources = new List<ResearchSourceResult>();

        if (Uri.TryCreate(input, UriKind.Absolute, out var inputUri))
            sources.Add(new ResearchSourceResult(inputUri.ToString(), "User provided source", Classify(inputUri.ToString(), "User provided source")));

        foreach (var query in BuildQueries(request))
        {
            foreach (var result in await SearchDuckDuckGoAsync(query, cancellationToken))
            {
                if (!LooksUsefulOfficialResult(result.Url, result.Title, input)) continue;
                sources.Add(new ResearchSourceResult(result.Url, result.Title, Classify(result.Url, result.Title)));
            }
        }

        var officialHosts = sources
            .Select(x => TryGetHost(x.Url))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        foreach (var host in officialHosts)
        {
            foreach (var query in BuildOfficialHostQueries(request, host!))
            {
                foreach (var result in await SearchDuckDuckGoAsync(query, cancellationToken))
                {
                    if (!LooksUsefulOfficialResult(result.Url, result.Title, input, host)) continue;
                    sources.Add(new ResearchSourceResult(result.Url, result.Title, Classify(result.Url, result.Title)));
                }
            }
        }

        return sources
            .GroupBy(x => NormalizeUrl(x.Url), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(y => SourcePriority(y.SourceType)).First())
            .OrderByDescending(x => SourcePriority(x.SourceType))
            .ThenBy(x => x.Url.Length)
            .Take(20)
            .ToArray();
    }

    private static IEnumerable<string> BuildQueries(ScholarshipAnalyzeRequest request)
    {
        var input = request.Url?.Trim() ?? "";
        var identity = ExtractSearchIdentity(input);
        var field = string.IsNullOrWhiteSpace(request.FieldPreference)
            ? "computer science software engineering artificial intelligence data science cybersecurity"
            : request.FieldPreference;
        var level = string.IsNullOrWhiteSpace(request.DegreeLevel) ? "master" : request.DegreeLevel;
        var country = request.CountryPreference ?? "";

        yield return $"{identity} official {level} programs {field}";
        yield return $"{identity} official {level} admission deadline international students";
        yield return $"{identity} official english requirements IELTS B2 MOI";
        yield return $"{identity} official scholarships tuition fees international students";
        if (!string.IsNullOrWhiteSpace(country))
            yield return $"{identity} {country} university official {level} {field}";
    }

    private static IEnumerable<string> BuildOfficialHostQueries(ScholarshipAnalyzeRequest request, string host)
    {
        var level = string.IsNullOrWhiteSpace(request.DegreeLevel) ? "master" : request.DegreeLevel;
        string[] topics = string.IsNullOrWhiteSpace(request.FieldPreference)
            ? ["computer science", "software engineering", "artificial intelligence", "data science", "cybersecurity", "informatics", "computer engineering", "automation engineering"]
            : [request.FieldPreference];

        foreach (var topic in topics)
            yield return $"site:{host} official {level} \"{topic}\"";

        yield return $"site:{host} official {level} programme deadline international students";
        yield return $"site:{host} official english language requirements IELTS B2 MOI";
        yield return $"site:{host} official scholarships tuition fees international students";
    }

    private async Task<IReadOnlyList<SearchResult>> SearchDuckDuckGoAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = "https://duckduckgo.com/html/?q=" + Uri.EscapeDataString(query);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseSearchResults(html).Take(10).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Search failed for query {Query}", query);
            return [];
        }
    }

    private static IEnumerable<SearchResult> ParseSearchResults(string html)
    {
        foreach (Match match in Regex.Matches(html, "<a[^>]+class=\"result__a\"[^>]+href=\"(?<href>[^\"]+)\"[^>]*>(?<title>[\\s\\S]*?)</a>", RegexOptions.IgnoreCase))
        {
            var href = DecodeSearchUrl(match.Groups["href"].Value);
            var title = CleanHtml(match.Groups["title"].Value);
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                yield return new SearchResult(uri.ToString(), title);
        }

        foreach (Match match in Regex.Matches(html, "uddg=(?<url>https?%3A%2F%2F[^&\"']+)", RegexOptions.IgnoreCase))
        {
            var href = Uri.UnescapeDataString(match.Groups["url"].Value);
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                yield return new SearchResult(uri.ToString(), uri.Host);
        }
    }

    private static string DecodeSearchUrl(string href)
    {
        href = System.Net.WebUtility.HtmlDecode(href);
        if (href.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(href, "uddg=([^&]+)", RegexOptions.IgnoreCase);
            if (match.Success) return Uri.UnescapeDataString(match.Groups[1].Value);
        }
        return href;
    }

    private static bool LooksUsefulOfficialResult(string url, string title, string input, string? trustedHost = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var combined = $"{url} {title}".ToLowerInvariant();
        string[] blocked =
        [
            "facebook", "linkedin", "youtube", "instagram", "wikipedia", "reddit", "quora",
            "mastersportal", "scholarshiproar", "studyportals", "topuniversities", "timeshighereducation",
            "educations.com", "gotouniversity", "ambitio", "daad.de", "scholarshipdb", "wemakescholars",
            "globalscholarships", "opportunitiescorners", "applyboard"
        ];
        if (blocked.Any(combined.Contains)) return false;

        var identity = ExtractSearchIdentity(input).ToLowerInvariant();
        var identityTokens = Regex.Split(identity, @"[^a-z0-9]+").Where(x => x.Length >= 4).ToArray();
        var identityMatch = identityTokens.Length == 0 || identityTokens.Any(t => combined.Contains(t));
        var trustedHostMatch = !string.IsNullOrWhiteSpace(trustedHost) &&
                               (uri.Host.Equals(trustedHost, StringComparison.OrdinalIgnoreCase) ||
                                uri.Host.EndsWith("." + trustedHost, StringComparison.OrdinalIgnoreCase));
        var officialWords = trustedHostMatch || combined.Contains(".edu") || combined.Contains(".ac.") || combined.Contains("university") || combined.Contains("universit") || combined.Contains("admission") || combined.Contains("apply") || identityMatch;
        var usefulWords = combined.Contains("master") || combined.Contains("programme") || combined.Contains("program") || combined.Contains("course") || combined.Contains("scholarship") || combined.Contains("tuition") || combined.Contains("fee") || combined.Contains("english") || combined.Contains("ielts") || combined.Contains("deadline") || combined.Contains("admission");
        return officialWords && usefulWords;
    }

    private static string Classify(string url, string title)
    {
        var value = $"{url} {title}".ToLowerInvariant();
        if (Regex.IsMatch(value, @"/(course|corso)/\d+", RegexOptions.IgnoreCase)) return "PROGRAM";
        if (value.Contains("course catalogue") || value.Contains("course catalog") || value.Contains("study programmes") || value.Contains("study programs")) return "PROGRAM_CATALOG";
        if (value.Contains("scholarship") || value.Contains("financial") || value.Contains("tuition") || value.Contains("fees")) return "SCHOLARSHIP_FEE";
        if (value.Contains("english") || value.Contains("ielts") || value.Contains("language") || value.Contains("moi")) return "LANGUAGE_REQUIREMENT";
        if (value.Contains("deadline") || value.Contains("admission") || value.Contains("international")) return "INTERNATIONAL";
        if (value.Contains("master") || value.Contains("programme") || value.Contains("program") || value.Contains("course") || value.Contains("degree")) return "PROGRAM";
        if (value.Contains("apply") || value.Contains("portal")) return "APPLICATION_PORTAL";
        return "GENERAL";
    }

    private static int SourcePriority(string sourceType) => sourceType switch
    {
        "PROGRAM" => 100,
        "PROGRAM_CATALOG" => 90,
        "SCHOLARSHIP_FEE" => 80,
        "LANGUAGE_REQUIREMENT" => 70,
        "INTERNATIONAL" => 60,
        "APPLICATION_PORTAL" => 50,
        _ => 10
    };

    private static string ExtractSearchIdentity(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) return string.IsNullOrWhiteSpace(input) ? "university" : input;
        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return host;
        var first = parts[0];
        if (first is "apply" or "admission" or "portal" && parts.Length > 1) first = parts[1];
        return $"{first} university";
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url.Trim();
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static string? TryGetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        return uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanHtml(string value)
    {
        value = Regex.Replace(value ?? "", "<[^>]+>", " ");
        value = System.Net.WebUtility.HtmlDecode(value);
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private sealed record SearchResult(string Url, string Title);
}
