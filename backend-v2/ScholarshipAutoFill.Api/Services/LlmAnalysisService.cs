using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ScholarshipAutoFill.Api.Domain;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface ILlmAnalysisService
{
    Task<RecommendedProgramResult[]?> TryBuildRecommendationsAsync(
        ApplicantProfile profile,
        ScholarshipAnalyzeRequest request,
        IReadOnlyList<AggregatedSource> sources,
        CancellationToken cancellationToken);
}

public sealed class LlmAnalysisService(HttpClient httpClient, IConfiguration configuration, ILogger<LlmAnalysisService> logger) : ILlmAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecommendedProgramResult[]?> TryBuildRecommendationsAsync(
        ApplicantProfile profile,
        ScholarshipAnalyzeRequest request,
        IReadOnlyList<AggregatedSource> sources,
        CancellationToken cancellationToken)
    {
        var endpoint = configuration["Llm:Endpoint"] ?? "http://localhost:11434/v1/chat/completions";
        var model = configuration["Llm:Model"] ?? "llama3.1:8b";
        var apiKey = configuration["Llm:ApiKey"];
        var providerName = configuration["Llm:Provider"] ?? (IsLocalEndpoint(endpoint) ? "Local LLM" : "OpenAI-compatible LLM");

        if (!IsLocalEndpoint(endpoint) && string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogInformation("{Provider} API key is not configured. Skipping LLM extraction.", providerName);
            return null;
        }

        var readableSources = sources
            .Where(x => x.FetchSucceeded && !string.IsNullOrWhiteSpace(x.Text))
            .Take(12)
            .ToArray();

        if (readableSources.Length == 0)
        {
            logger.LogInformation("No readable official source content was available for DeepSeek extraction.");
            return [];
        }

        var payload = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = """
You are a strict university admissions research extractor.
Use only the official source content supplied by the application.
Do not use memory, guesses, common knowledge, or external facts.
Do not invent programmes, fees, deadlines, scholarships, IELTS rules, or MOI acceptance.
If a field is missing from the supplied official content, write exactly: Not found on official page.
Return JSON only.
"""
                },
                new
                {
                    role = "user",
                    content = BuildPrompt(profile, request, readableSources)
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(apiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            using var response = await httpClient.SendAsync(httpRequest, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("{Provider} extraction failed with status {StatusCode}: {Body}", providerName, response.StatusCode, body);
                return null;
            }

            var chat = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
            var content = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content)) return null;

            var extracted = JsonSerializer.Deserialize<LlmRecommendationResponse>(content, JsonOptions);
            return extracted?.RecommendedPrograms?
                .Where(IsValidLlmProgram)
                .Select(x => new RecommendedProgramResult(
                    x.ProgramName.Trim(),
                    EmptyToNotFound(x.DegreeLevel),
                    EmptyToNotFound(x.DepartmentOrFaculty),
                    EmptyToNotFound(x.OfficialProgramUrl),
                    Math.Clamp(decimal.Round(x.EligibilityMatchScore, 1), 1, 10),
                    EmptyToNotFound(x.ScholarshipAvailability),
                    EmptyToNotFound(x.TuitionFee),
                    EmptyToNotFound(x.Deadline),
                    EmptyToNotFound(x.EnglishRequirement),
                    EmptyToNotFound(x.MoiAcceptance),
                    EmptyToNotFound(x.MatchReason),
                    x.MissingRequirementsOrRisks?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToArray() ?? [],
                    x.SourceUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? []))
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Provider} extraction failed.", providerName);
            return null;
        }
    }

    private static bool IsLocalEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPrompt(ApplicantProfile profile, ScholarshipAnalyzeRequest request, IReadOnlyList<AggregatedSource> sources)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Applicant profile:");
        sb.AppendLine($"Nationality: {profile.Nationality}");
        sb.AppendLine($"Degree: {profile.HighestDegree}");
        sb.AppendLine($"CGPA: {profile.Cgpa}");
        sb.AppendLine($"IELTS overall: {profile.IeltsOverall}");
        sb.AppendLine($"Skills: {profile.Skills}");
        sb.AppendLine($"Research interests: {profile.ResearchInterests}");
        sb.AppendLine($"Scholarship preferences: {profile.ScholarshipPreferences}");
        sb.AppendLine();
        sb.AppendLine("User request:");
        sb.AppendLine($"Input URL or university: {request.Url}");
        sb.AppendLine($"Degree level: {request.DegreeLevel}");
        sb.AppendLine($"Field preference: {request.FieldPreference}");
        sb.AppendLine($"Country preference: {request.CountryPreference}");
        sb.AppendLine();
        sb.AppendLine("Required output JSON shape:");
        sb.AppendLine("""
{
  "recommendedPrograms": [
    {
      "programName": "Actual official programme name only",
      "degreeLevel": "Master/MSc/etc or Not found on official page",
      "departmentOrFaculty": "Official department/faculty or Not found on official page",
      "officialProgramUrl": "Official source URL",
      "eligibilityMatchScore": 1.0,
      "scholarshipAvailability": "Official scholarship info or Not found on official page",
      "tuitionFee": "Official fee/application fee or Not found on official page",
      "deadline": "Official deadline or Not found on official page",
      "englishRequirement": "Official IELTS/TOEFL/B2 rule or Not found on official page",
      "moiAcceptance": "Official MOI rule or Not found on official page",
      "matchReason": "Why it matches the applicant, based on profile and official programme content",
      "missingRequirementsOrRisks": ["Specific honest risks only"],
      "sourceUrls": ["Official URLs supporting the claims"]
    }
  ]
}
""");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("Only recommend real academic programmes related to Computer Science, Software Engineering, Informatics, Data Science, Artificial Intelligence, Cybersecurity, Information Systems, Computer Engineering, Automation, Robotics, Distributed Systems, or Cloud.");
        sb.AppendLine("Reject portals, apply pages, fee pages, admission instruction pages, login pages, FAQ pages, and scholarship-only pages as programmes.");
        sb.AppendLine("Every important claim must be supported by one of the supplied source URLs.");
        sb.AppendLine("If no real matching programme exists in the supplied content, return an empty recommendedPrograms array.");
        sb.AppendLine();
        sb.AppendLine("Official source content:");

        foreach (var source in sources)
        {
            sb.AppendLine($"SOURCE URL: {source.Url}");
            sb.AppendLine($"SOURCE TITLE: {source.Title}");
            sb.AppendLine($"SOURCE TYPE: {source.SourceType}");
            sb.AppendLine(TrimForPrompt(source.Text, 5000));
            sb.AppendLine("---");
        }

        return sb.ToString();
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static bool IsValidLlmProgram(LlmRecommendedProgram program)
    {
        if (string.IsNullOrWhiteSpace(program.ProgramName)) return false;
        if (string.IsNullOrWhiteSpace(program.OfficialProgramUrl)) return false;
        if (!Uri.TryCreate(program.OfficialProgramUrl, UriKind.Absolute, out _)) return false;

        var name = program.ProgramName.ToLowerInvariant();
        string[] bad = ["apply", "admission", "evaluation", "documents", "hub", "enrolment", "fee", "deadline", "faq", "portal", "login"];
        return !bad.Any(name.Contains);
    }

    private static string EmptyToNotFound(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not found on official page" : value.Trim();
    }

    private sealed record ChatCompletionResponse(ChatChoice[]? Choices);
    private sealed record ChatChoice(ChatMessage? Message);
    private sealed record ChatMessage(string? Content);
    private sealed record LlmRecommendationResponse(LlmRecommendedProgram[]? RecommendedPrograms);

    private sealed record LlmRecommendedProgram(
        string ProgramName,
        string DegreeLevel,
        string DepartmentOrFaculty,
        string OfficialProgramUrl,
        decimal EligibilityMatchScore,
        string ScholarshipAvailability,
        string TuitionFee,
        string Deadline,
        string EnglishRequirement,
        string MoiAcceptance,
        string MatchReason,
        string[]? MissingRequirementsOrRisks,
        string[]? SourceUrls);
}
