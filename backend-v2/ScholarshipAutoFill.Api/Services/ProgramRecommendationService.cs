using System.Text.RegularExpressions;
using ScholarshipAutoFill.Api.Domain;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface IProgramRecommendationService
{
    RecommendedProgramResult[] BuildRecommendations(ApplicantProfile profile, IReadOnlyList<AggregatedSource> sources, ScholarshipAnalyzeRequest request);
}

public sealed class ProgramRecommendationService : IProgramRecommendationService
{
    private static readonly string[] RelevantStudyTerms =
    [
        "computer", "software", "informatics", "information systems", "data science", "data", "artificial intelligence",
        "machine learning", "ai", "cybersecurity", "cyber security", "security", "computer engineering",
        "automation", "robotics", "distributed systems", "cloud", "digital engineering"
    ];

    private static readonly string[] GenericPageTerms =
    [
        "apply online", "admission", "admissions", "international student", "student hub", "evaluation step",
        "documents", "finalising enrolment", "enrolment", "tuition", "fees", "deadline", "login",
        "faq", "how to apply", "scholarship", "scholarships", "portal", "home page", "privacy policy"
    ];

    public RecommendedProgramResult[] BuildRecommendations(ApplicantProfile profile, IReadOnlyList<AggregatedSource> sources, ScholarshipAnalyzeRequest request)
    {
        return sources
            .Where(source => IsCandidateProgramPage(source, request))
            .Select(source => BuildProgram(profile, source, sources, request))
            .Where(program => program is not null)
            .Select(program => program!)
            .GroupBy(program => NormalizeKey(program.ProgramName), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(program => program.EligibilityMatchScore).First())
            .OrderByDescending(program => program.EligibilityMatchScore)
            .Take(12)
            .ToArray();
    }

    private static RecommendedProgramResult? BuildProgram(ApplicantProfile profile, AggregatedSource source, IReadOnlyList<AggregatedSource> allSources, ScholarshipAnalyzeRequest request)
    {
        var name = NormalizeProgramName(source.Title, source.Url);
        if (!IsValidProgramName(name) || !IsRelevant($"{name} {source.Text}", request)) return null;

        var combinedProgramText = $"{source.Title}. {source.Text}";
        var scholarship = ExtractScholarship(allSources);
        var fee = ExtractFee(allSources);
        var deadline = ExtractDeadline(allSources.Prepend(source).ToArray());
        var english = ExtractEnglish(allSources.Prepend(source).ToArray());
        var moi = ExtractMoi(allSources.Prepend(source).ToArray());
        var score = Score(profile, name, combinedProgramText, english, moi);

        return new RecommendedProgramResult(
            name,
            string.IsNullOrWhiteSpace(request.DegreeLevel) ? ExtractDegreeLevel(combinedProgramText) : request.DegreeLevel,
            ExtractDepartment(combinedProgramText),
            source.Url,
            score,
            scholarship,
            fee,
            deadline,
            english,
            moi,
            BuildReason(profile, name, combinedProgramText),
            BuildRisks(profile, combinedProgramText, english, moi, deadline, fee),
            BuildSources(source, allSources));
    }

    private static bool IsCandidateProgramPage(AggregatedSource source, ScholarshipAnalyzeRequest request)
    {
        if (source.SourceType != "PROGRAM") return false;

        var title = source.Title.Trim();
        var url = source.Url.ToLowerInvariant();
        var text = source.Text;
        var combined = $"{title} {url} {text}".ToLowerInvariant();

        if (!source.FetchSucceeded && !IsRelevant($"{title} {url}", request)) return false;
        if (LooksLikeGenericPage(title, url) && !HasSpecificAcademicSignal(title, url, text)) return false;
        if (!IsRelevant(combined, request)) return false;

        var degreeSignal = ContainsAny(combined, ["master", "msc", "m.sc", "laurea magistrale", "graduate", "second cycle", "lm-", "postgraduate"]);
        var academicSignal = ContainsAny(combined, ["degree", "programme", "program", "course", "curriculum", "study plan", "ects", "learning outcomes", "admission requirements"]);

        return degreeSignal || academicSignal || HasSpecificAcademicSignal(title, url, text);
    }

    private static bool HasSpecificAcademicSignal(string title, string url, string text)
    {
        var combined = $"{title} {url} {text}".ToLowerInvariant();
        return RelevantStudyTerms.Count(term => combined.Contains(term)) >= 1 &&
               ContainsAny(combined, ["master", "msc", "degree", "programme", "program", "course", "laurea", "engineering"]);
    }

    private static bool LooksLikeGenericPage(string title, string url)
    {
        var combined = $"{title} {url}".ToLowerInvariant();
        return GenericPageTerms.Any(term => combined.Contains(term)) &&
               !RelevantStudyTerms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal Score(ApplicantProfile profile, string name, string text, string english, string moi)
    {
        var combined = $"{name} {text}".ToLowerInvariant();
        var score = 4.8m;

        foreach (var term in RelevantStudyTerms.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (combined.Contains(term)) score += term.Length > 8 ? 0.45m : 0.25m;
        }

        if (combined.Contains("software") || combined.Contains("computer") || combined.Contains("informatics")) score += 1.0m;
        if (combined.Contains("artificial intelligence") || combined.Contains("data science") || combined.Contains("cybersecurity")) score += 0.8m;
        if (combined.Contains("automation") || combined.Contains("robotics")) score += 0.45m;
        if (profile.Cgpa.Contains("3.46", StringComparison.OrdinalIgnoreCase)) score += 0.35m;
        if (profile.Skills.Contains("microservices", StringComparison.OrdinalIgnoreCase) || profile.ResearchInterests.Contains("distributed", StringComparison.OrdinalIgnoreCase)) score += 0.45m;

        var requirement = $"{english} {moi}".ToLowerInvariant();
        if (profile.IeltsOverall == "5.5" && ContainsAny(requirement, ["ielts 6", "ielts: 6", "6.0", "c1"])) score -= 1.0m;
        if (profile.IeltsOverall == "5.5" && ContainsAny(requirement, ["b2", "medium of instruction", "moi", "exemption"])) score += 0.25m;

        return Math.Clamp(decimal.Round(score, 1), 1, 10);
    }

    private static string BuildReason(ApplicantProfile profile, string name, string text)
    {
        var matched = RelevantStudyTerms
            .Where(term => $"{name} {text}".Contains(term, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        var focus = matched.Length == 0 ? "software engineering" : string.Join(", ", matched);
        return $"Matches your BS Software Engineering background and professional experience in .NET, Angular, microservices, distributed systems and cloud deployment. Official source text indicates relevance to {focus}.";
    }

    private static string[] BuildRisks(ApplicantProfile profile, string text, string english, string moi, string deadline, string fee)
    {
        var risks = new List<string>();
        if (english.StartsWith("Not found", StringComparison.OrdinalIgnoreCase))
            risks.Add("English requirement was not found on the official pages collected for this analysis.");
        else if (profile.IeltsOverall == "5.5" && ContainsAny($"{english} {moi}".ToLowerInvariant(), ["ielts 6", "6.0", "c1"]))
            risks.Add("Your IELTS 5.5 may be below the stated English requirement. Confirm if MOI, exemption, entry test, or updated IELTS is accepted.");

        if (moi.StartsWith("Not found", StringComparison.OrdinalIgnoreCase))
            risks.Add("MOI acceptance was not confirmed from the official pages.");
        if (deadline.StartsWith("Not found", StringComparison.OrdinalIgnoreCase))
            risks.Add("Deadline was not found in the official pages collected. Check programme call and non-EU visa applicant dates before applying.");
        if (fee.StartsWith("Not found", StringComparison.OrdinalIgnoreCase))
            risks.Add("Fee or application fee was not found in the official pages collected.");
        if (!ContainsAny(text.ToLowerInvariant(), ["admission requirement", "entry requirement", "requirements", "prerequisite"]))
            risks.Add("Programme prerequisites were not clearly extracted. Compare your transcript with official entry requirements.");

        return risks.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ExtractDegreeLevel(string text)
    {
        if (text.Contains("Laurea Magistrale", StringComparison.OrdinalIgnoreCase)) return "Laurea Magistrale / Master's";
        if (text.Contains("MSc", StringComparison.OrdinalIgnoreCase) || text.Contains("M.Sc", StringComparison.OrdinalIgnoreCase)) return "MSc / Master's";
        if (text.Contains("Master", StringComparison.OrdinalIgnoreCase)) return "Master's";
        return "Not found on official page";
    }

    private static string ExtractDepartment(string text)
    {
        var sentence = FindSentenceNear(text, ["department", "faculty", "school"], 180);
        return string.IsNullOrWhiteSpace(sentence) ? "Not found on official page" : sentence;
    }

    private static string ExtractScholarship(IReadOnlyList<AggregatedSource> sources)
    {
        var scholarshipSources = sources.Where(x => x.SourceType == "SCHOLARSHIP_FEE").ToArray();
        var sentence = FindBestSentence(scholarshipSources, ["scholarship", "grant", "financial aid", "tuition waiver", "benefit", "dsu", "excellence"], 260);
        if (!string.IsNullOrWhiteSpace(sentence)) return sentence;
        return scholarshipSources.Length == 0
            ? "Not found on official page"
            : "Official scholarship or fee source found, but exact scholarship details were not extracted from readable page text.";
    }

    private static string ExtractFee(IReadOnlyList<AggregatedSource> sources)
    {
        var feeSources = sources.Where(x => x.SourceType == "SCHOLARSHIP_FEE" || ContainsAny(x.Title.ToLowerInvariant(), ["fee", "tuition"])).ToArray();
        var sentence = FindBestSentence(feeSources, ["tuition", "fee", "fees", "application fee", "contribution", "isee", "eur", "€"], 260);
        if (!string.IsNullOrWhiteSpace(sentence) && !LooksLikeGarbage(sentence)) return sentence;
        return feeSources.Length == 0
            ? "Not found on official page"
            : "Official fee source found, but exact fee amount was not extracted from readable page text.";
    }

    private static string ExtractDeadline(IReadOnlyList<AggregatedSource> sources)
    {
        foreach (var source in sources)
        {
            var sentence = FindSentenceNear(source.Text, ["deadline", "application period", "apply by", "call for applications", "closing date"], 260);
            if (!string.IsNullOrWhiteSpace(sentence) && ContainsDateSignal(sentence) && !LooksLikeGarbage(sentence)) return sentence;
        }

        return "Not found on official page";
    }

    private static string ExtractEnglish(IReadOnlyList<AggregatedSource> sources)
    {
        var sentence = FindBestSentence(sources, ["ielts", "toefl", "english language", "b2", "c1", "language requirement", "language certificate"], 300);
        if (!string.IsNullOrWhiteSpace(sentence) && !LooksLikeGarbage(sentence)) return sentence;
        return "Not found on official page";
    }

    private static string ExtractMoi(IReadOnlyList<AggregatedSource> sources)
    {
        var sentence = FindBestSentence(sources, ["medium of instruction", "moi", "exemption", "english taught", "language exemption"], 260);
        if (!string.IsNullOrWhiteSpace(sentence) && !LooksLikeGarbage(sentence)) return sentence;
        return "Not found on official page";
    }

    private static string[] BuildSources(AggregatedSource source, IReadOnlyList<AggregatedSource> allSources)
    {
        return allSources
            .Where(x => x.Url == source.Url || x.SourceType is "SCHOLARSHIP_FEE" or "LANGUAGE_REQUIREMENT" or "INTERNATIONAL" or "APPLICATION_PORTAL")
            .Select(x => x.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static string NormalizeProgramName(string title, string url)
    {
        var cleaned = Regex.Replace(title ?? "", @"\s+", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s*[\|\-–—]\s*(official.*|home.*|admissions?.*|universit.*|university.*)$", "", RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"\b(master'?s?\s+degree|master'?s?\s+programme|master'?s?\s+program|laurea magistrale)\b", "", RegexOptions.IgnoreCase).Trim(' ', '-', '|', ':');

        if (IsValidProgramName(cleaned) && cleaned.Length <= 140) return cleaned;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return cleaned;
        var slug = uri.Segments.LastOrDefault()?.Trim('/') ?? "";
        slug = Regex.Replace(slug.Replace("-", " "), @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(slug) ? cleaned : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slug);
    }

    private static bool IsValidProgramName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 4) return false;
        if (GenericPageTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase))) return false;
        return RelevantStudyTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRelevant(string value, ScholarshipAnalyzeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.FieldPreference) && value.Contains(request.FieldPreference, StringComparison.OrdinalIgnoreCase))
            return true;

        return RelevantStudyTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindBestSentence(IEnumerable<AggregatedSource> sources, string[] keywords, int maxLength)
    {
        foreach (var source in sources)
        {
            var sentence = FindSentenceNear($"{source.Title}. {source.Text}", keywords, maxLength);
            if (!string.IsNullOrWhiteSpace(sentence)) return sentence;
        }

        return "";
    }

    private static string FindSentenceNear(string text, string[] keywords, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var cleaned = Regex.Replace(text, @"\s+", " ").Trim();
        var index = keywords
            .Select(keyword => cleaned.IndexOf(keyword, StringComparison.OrdinalIgnoreCase))
            .Where(position => position >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (index < 0) return "";

        var start = Math.Max(0, cleaned.LastIndexOfAny(['.', '!', '?', '\n'], index) + 1);
        var endCandidates = new[]
        {
            cleaned.IndexOf('.', index + 1),
            cleaned.IndexOf('!', index + 1),
            cleaned.IndexOf('?', index + 1)
        }.Where(position => position > index).ToArray();
        var end = endCandidates.Length == 0 ? Math.Min(cleaned.Length, index + maxLength) : Math.Min(cleaned.Length, endCandidates.Min() + 1);
        var sentence = cleaned[start..end].Trim();
        if (sentence.Length > maxLength) sentence = sentence[..maxLength].Trim();
        return sentence;
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDateSignal(string value)
    {
        return Regex.IsMatch(value, @"\b(\d{1,2}\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+20\d{2}|(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+20\d{2}|\d{1,2}[/-]\d{1,2}[/-]20\d{2}|20\d{2})\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeGarbage(string value)
    {
        var lower = value.ToLowerInvariant();
        return value.Length > 320 ||
               Regex.Matches(value, ",").Count > 12 ||
               lower.Contains("applicationsubmit") ||
               lower.Contains("afghanistan albania") ||
               lower.Contains("select all select all");
    }

    private static string NormalizeKey(string value)
    {
        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }
}
