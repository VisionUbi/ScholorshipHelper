using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScholarshipAutoFill.Api.Data;
using ScholarshipAutoFill.Api.Domain;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface IAiSearchProvider
{
    string Name { get; }
    string GenerateSearchPrompt(AiSearchCriteriaRequest request, ApplicantProfile profile);
    AiSearchParseResponse ParseSearchResponse(string jsonResponse);
    Task<AiSearchParseResponse> SearchUniversitiesAsync(AiSearchCriteriaRequest request, ApplicantProfile profile, CancellationToken cancellationToken);
}

public interface IAiSearchService
{
    AiSearchProviderStatusResponse GetProviderStatus();
    Task<AiSearchPromptResponse> GeneratePromptAsync(AiSearchCriteriaRequest request, CancellationToken cancellationToken);
    AiSearchParseResponse ParseResponse(AiSearchParseRequest request);
    Task<AiSearchParseResponse> SearchAsync(AiSearchCriteriaRequest request, CancellationToken cancellationToken);
    Task<AiSearchSaveResponse> SaveResultsAsync(AiSearchSaveRequest request, CancellationToken cancellationToken);
}

public sealed class AiSearchService(
    AppDbContext db,
    IConfiguration configuration,
    IEnumerable<IAiSearchProvider> providers) : IAiSearchService
{
    public AiSearchProviderStatusResponse GetProviderStatus()
    {
        var provider = GetProvider();
        var configured = configuration["AiProvider:Provider"] ?? "DeepSeekApi";
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["AiProvider:ApiKey"]);
        var isManual = provider.Name.Equals("ManualDeepSeek", StringComparison.OrdinalIgnoreCase);
        var needsApiKey = !isManual && !provider.Name.Equals("Mock", StringComparison.OrdinalIgnoreCase);
        return new AiSearchProviderStatusResponse(
            configured,
            provider.Name,
            apiKeyConfigured,
            isManual,
            isManual
                ? "Manual fallback mode. Generate a prompt, paste it into DeepSeek manually, then paste JSON back into this app."
                : provider.Name.Equals("Mock", StringComparison.OrdinalIgnoreCase)
                    ? "Mock mode returns local sample data without API credits."
                    : apiKeyConfigured
                        ? "API mode calls the configured official provider endpoint."
                        : "AI provider is not configured. Add API key or switch to Manual mode.");
    }

    public async Task<AiSearchPromptResponse> GeneratePromptAsync(AiSearchCriteriaRequest request, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(cancellationToken);
        var provider = GetProvider();
        return new AiSearchPromptResponse(provider.Name, provider.GenerateSearchPrompt(request, profile), JsonShape);
    }

    public AiSearchParseResponse ParseResponse(AiSearchParseRequest request)
    {
        return GetProvider().ParseSearchResponse(request.JsonResponse);
    }

    public async Task<AiSearchParseResponse> SearchAsync(AiSearchCriteriaRequest request, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(cancellationToken);
        var response = await GetProvider().SearchUniversitiesAsync(request, profile, cancellationToken);
        if (!response.IsValid || response.Universities.Length == 0)
            return response;

        var saved = await SaveResultsAsync(new AiSearchSaveRequest(request, response.Universities, null), cancellationToken);
        return response with { AnalysisId = saved.AnalysisId };
    }

    public async Task<AiSearchSaveResponse> SaveResultsAsync(AiSearchSaveRequest request, CancellationToken cancellationToken)
    {
        var selected = request.SelectedProgramKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var universities = request.Universities ?? [];
        var programs = universities
            .SelectMany(university => university.Programs.Select(program => new { university, program, key = BuildProgramKey(university, program) }))
            .Where(x => selected.Count == 0 || selected.Contains(x.key))
            .ToArray();

        var analysis = new ScholarshipAnalysis
        {
            Input = request.Criteria.Query,
            UniversityName = universities.Length == 1 ? universities[0].UniversityName : "AI search results",
            Status = "Saved from AI search",
            Summary = $"Saved {programs.Length} programme(s) from configured AI provider search."
        };

        foreach (var item in programs)
        {
            analysis.RecommendedPrograms.Add(new RecommendedProgram
            {
                Name = item.program.ProgramName,
                DegreeLevel = item.program.DegreeLevel,
                Score = 0,
                Scholarship = item.program.ScholarshipsAvailable ? item.program.ScholarshipDetails : "Not found on official page",
                Fee = item.program.TuitionFee,
                Deadline = item.program.Deadline,
                EnglishOrMoi = item.program.Language,
                Risks = item.program.Eligibility,
                SourcesJson = JsonSerializer.Serialize(item.program.SourceUrls)
            });

            foreach (var sourceUrl in item.program.SourceUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                analysis.Sources.Add(new ResearchSource
                {
                    Url = sourceUrl,
                    Title = item.program.ProgramName,
                    SourceType = "AI_SEARCH_SOURCE",
                    ExtractedText = item.program.Eligibility
                });
            }

            if (item.program.ScholarshipsAvailable)
            {
                analysis.Scholarships.Add(new ScholarshipOpportunity
                {
                    Name = item.program.ScholarshipDetails,
                    Amount = "Not found on official page",
                    Eligibility = item.program.Eligibility,
                    Deadline = item.program.Deadline,
                    SourceUrl = item.program.SourceUrls.FirstOrDefault() ?? ""
                });
            }
        }

        analysis.Logs.Add(new AnalysisLog { Step = "AiSearchSave", Message = $"Provider: {GetProvider().Name}. Selected programs: {programs.Length}." });
        db.ScholarshipAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);

        return new AiSearchSaveResponse(analysis.Id, programs.Length, "AI search results saved to PostgreSQL.");
    }

    private IAiSearchProvider GetProvider()
    {
        var configured = configuration["AiProvider:Provider"] ?? "DeepSeekApi";
        return providers.FirstOrDefault(x => x.Name.Equals(configured, StringComparison.OrdinalIgnoreCase)) ??
               providers.First(x => x.Name.Equals("DeepSeekApi", StringComparison.OrdinalIgnoreCase));
    }

    private Task<ApplicantProfile> LoadProfileAsync(CancellationToken cancellationToken)
    {
        return db.ApplicantProfiles.OrderBy(x => x.CreatedAtUtc).FirstAsync(cancellationToken);
    }

    public static string BuildProgramKey(AiSearchUniversityResult university, AiSearchProgramResult program)
    {
        return $"{university.UniversityName}|{program.ProgramName}|{program.DegreeLevel}";
    }

    public const string JsonShape = """
{
  "universities": [
    {
      "universityName": "",
      "country": "",
      "city": "",
      "officialWebsite": "",
      "applicationPortal": "",
      "programs": [
        {
          "programName": "",
          "degreeLevel": "",
          "field": "",
          "language": "",
          "deadline": "",
          "tuitionFee": "",
          "scholarshipsAvailable": true,
          "scholarshipDetails": "",
          "eligibility": "",
          "requiredDocuments": [],
          "sourceUrls": []
        }
      ]
    }
  ]
}
""";
}

public abstract class AiSearchProviderBase : IAiSearchProvider
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString
    };

    public abstract string Name { get; }

    public virtual string GenerateSearchPrompt(AiSearchCriteriaRequest request, ApplicantProfile profile)
    {
        return $$"""
You are an Expert University Admission and Scholarship Research Agent. Your task is to analyze ONLY the university program URL I provide and return the result in EXACT VALID JSON format.

CRITICAL RULES:
1. Return ONLY JSON.
2. Do NOT write explanations before or after JSON.
3. Do NOT use markdown.
4. Do NOT use code blocks.
5. If information is missing, use null.
6. Keep the exact JSON structure.
7. Always extract information directly from the provided university page and official linked pages.
8. Never change field names.
9. Never add extra fields.
10. Always return arrays even if only one item exists.

UNIVERSITY URL: {{request.Query}}

APPLICANT PROFILE:
Full name: {{profile.FullName}}
Nationality: {{profile.Nationality}}
Degree: {{profile.HighestDegree}}
CGPA: {{profile.Cgpa}}
IELTS: {{profile.IeltsOverall}} overall, CEFR {{profile.CefrLevel}}
Experience: {{profile.ExperienceSummary}}
Skills: {{profile.Skills}}
Research interests: {{profile.ResearchInterests}}
Scholarship preferences: {{profile.ScholarshipPreferences}}
Preferred regions: {{profile.PreferredRegions}}
Degree level requested: {{request.DegreeLevel}}
Field preference: {{request.FieldPreference}}
Country preference: {{request.CountryPreference}}
Scholarship preference: {{request.ScholarshipPreference}}

OUTPUT FORMAT:
{
  "university_name": "",
  "country": "",
  "city": "",
  "program_name": "",
  "degree_level": "",
  "faculty": "",
  "study_language": "",
  "duration": "",
  "intake_year": "",
  "application_portal": "",
  "application_fee": {
    "required": false,
    "amount": null,
    "currency": null
  },
  "tuition_fee": {
    "annual_fee": null,
    "currency": null,
    "notes": ""
  },
  "deadlines": [
    {
      "round": "",
      "deadline": "",
      "status": ""
    }
  ],
  "language_requirements": {
    "english_required": false,
    "minimum_level": "",
    "accepted_tests": [
      {
        "test_name": "",
        "minimum_score": ""
      }
    ],
    "moi_accepted": false,
    "moi_details": ""
  },
  "admission_requirements": {
    "previous_degree_required": "",
    "minimum_gpa": "",
    "required_documents": [],
    "entry_test_required": false,
    "entry_test_name": ""
  },
  "scholarships": [
    {
      "scholarship_name": "",
      "coverage": "",
      "amount": "",
      "deadline": "",
      "eligibility": ""
    }
  ],
  "estimated_living_cost": {
    "monthly": null,
    "currency": null
  },
  "risks": [
    {
      "risk_type": "",
      "details": "",
      "severity": "Low"
    }
  ],
  "advantages": [],
  "official_sources": [],
  "overall_assessment": {
    "admission_chance": "",
    "scholarship_chance": "",
    "summary": ""
  }
}

RISK ANALYSIS RULES:
Always evaluate:
* Scholarship competitiveness
* Admission competitiveness
* English language restrictions
* MOI acceptance uncertainty
* Visa challenges
* Limited seats
* High tuition fee
* Missing prerequisite courses
* Entry test requirements
* Portfolio or interview requirements

Severity values allowed:
Low
Medium
High

ADMISSION CHANCE VALUES:
Very High
High
Moderate
Low
Very Low

SCHOLARSHIP CHANCE VALUES:
Very High
High
Moderate
Low
Very Low

Before generating output, verify JSON validity.
Return ONLY VALID JSON.
""";
    }

    public virtual AiSearchParseResponse ParseSearchResponse(string jsonResponse)
    {
        try
        {
            var cleaned = RemoveOrphanCitationNumbers(NormalizeUnquotedJsonRanges(EscapeLineBreaksInsideJsonStrings(ExtractJson(jsonResponse))));
            var parsed = JsonSerializer.Deserialize<AiSearchRoot>(cleaned, JsonOptions);
            var universities = parsed?.Universities?.Select(CleanUniversity).Where(x => !string.IsNullOrWhiteSpace(x.UniversityName)).ToArray() ?? [];
            if (universities.Length == 0)
            {
                var single = JsonSerializer.Deserialize<StrictProgramResearchResult>(cleaned, JsonOptions);
                if (single is not null && !string.IsNullOrWhiteSpace(single.UniversityName))
                    universities = [MapStrictResult(single)];
            }
            var errors = Validate(universities).ToArray();
            return new AiSearchParseResponse(errors.Length == 0, errors, universities);
        }
        catch (Exception ex)
        {
            return new AiSearchParseResponse(false, [$"Invalid JSON response: {ex.Message}"], []);
        }
    }

    public abstract Task<AiSearchParseResponse> SearchUniversitiesAsync(AiSearchCriteriaRequest request, ApplicantProfile profile, CancellationToken cancellationToken);

    protected static AiSearchUniversityResult CleanUniversity(AiSearchUniversityResult university)
    {
        return university with
        {
            UniversityName = Clean(university.UniversityName),
            Country = Clean(university.Country),
            City = Clean(university.City),
            OfficialWebsite = Clean(university.OfficialWebsite),
            ApplicationPortal = Clean(university.ApplicationPortal),
            Programs = university.Programs?
                .Where(x => !IsGenericProgramName(x.ProgramName))
                .Select(CleanProgram)
                .ToArray() ?? []
        };
    }

    private static AiSearchProgramResult CleanProgram(AiSearchProgramResult program)
    {
        return program with
        {
            ProgramName = Clean(program.ProgramName),
            DegreeLevel = Clean(program.DegreeLevel),
            Field = Clean(program.Field),
            Language = Clean(program.Language),
            Deadline = Clean(program.Deadline),
            TuitionFee = Clean(program.TuitionFee),
            ScholarshipDetails = Clean(program.ScholarshipDetails),
            Eligibility = Clean(program.Eligibility),
            RequiredDocuments = program.RequiredDocuments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Clean).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
            SourceUrls = program.SourceUrls?.Where(IsHttpUrl).Select(Clean).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? []
        };
    }

    private static IEnumerable<string> Validate(AiSearchUniversityResult[] universities)
    {
        if (universities.Length == 0) yield return "No universities found in JSON.";
        foreach (var university in universities)
        {
            if (string.IsNullOrWhiteSpace(university.UniversityName)) yield return "A university is missing universityName.";
            foreach (var program in university.Programs)
            {
                if (string.IsNullOrWhiteSpace(program.ProgramName)) yield return $"{university.UniversityName}: a program is missing programName.";
                if (program.SourceUrls.Length == 0) yield return $"{university.UniversityName} / {program.ProgramName}: sourceUrls must contain at least one official URL.";
            }
        }
    }

    private static string ExtractJson(string value)
    {
        value = value.Trim();
        var firstBrace = value.IndexOf('{');
        var lastBrace = value.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace ? value[firstBrace..(lastBrace + 1)] : value;
    }

    private static string EscapeLineBreaksInsideJsonStrings(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inString = false;
        var escaped = false;

        foreach (var character in value)
        {
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && inString)
            {
                builder.Append(character);
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
                builder.Append(character);
                continue;
            }

            if (inString && character is '\r' or '\n')
            {
                builder.Append("\\n");
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string NormalizeUnquotedJsonRanges(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inString = false;
        var escaped = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && inString)
            {
                builder.Append(character);
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
                builder.Append(character);
                continue;
            }

            if (inString || character != ':')
            {
                builder.Append(character);
                continue;
            }

            builder.Append(character);
            index++;
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                builder.Append(value[index]);
                index++;
            }

            if (index >= value.Length)
                break;

            var tokenStart = index;
            if (value[index] is '-' or '+' || char.IsDigit(value[index]))
            {
                while (index < value.Length && !IsJsonValueDelimiter(value[index]))
                    index++;

                var token = value[tokenStart..index].Trim();
                if (LooksLikeUnquotedRange(token))
                {
                    builder.Append('"').Append(token.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                    index--;
                    continue;
                }

                builder.Append(value[tokenStart..index]);
                index--;
                continue;
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    private static bool LooksLikeUnquotedRange(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (token.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("null", StringComparison.OrdinalIgnoreCase))
            return false;

        return token.Contains('-') && token.Any(char.IsDigit);
    }

    private static bool IsJsonValueDelimiter(char character)
    {
        return character is ',' or '}' or ']' or '\r' or '\n';
    }

    private static string RemoveOrphanCitationNumbers(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inString = false;
        var escaped = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && inString)
            {
                builder.Append(character);
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
                builder.Append(character);
                continue;
            }

            if (inString)
            {
                builder.Append(character);
                continue;
            }

            if (char.IsWhiteSpace(character) && IsPreviousTokenValueEnd(builder))
            {
                var lookahead = index;
                while (lookahead < value.Length && char.IsWhiteSpace(value[lookahead]))
                    lookahead++;

                var numberStart = lookahead;
                while (lookahead < value.Length && char.IsDigit(value[lookahead]))
                    lookahead++;

                if (lookahead > numberStart)
                {
                    var afterNumber = lookahead;
                    while (afterNumber < value.Length && char.IsWhiteSpace(value[afterNumber]))
                        afterNumber++;

                    if (afterNumber < value.Length && value[afterNumber] is ',' or '}' or ']')
                    {
                        index = lookahead - 1;
                        continue;
                    }
                }
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool IsPreviousTokenValueEnd(StringBuilder builder)
    {
        for (var index = builder.Length - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(builder[index]))
                continue;

            return builder[index] is '"' or '}' or ']' || char.IsDigit(builder[index]);
        }

        return false;
    }

    private static string Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not found on official page" : value.Trim();
    }

    private static bool IsHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    private static bool IsGenericProgramName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var lower = value.ToLowerInvariant();
        string[] blocked = ["apply online", "admission", "evaluation", "documents", "hub", "enrolment", "fee", "deadline", "faq", "portal", "login"];
        return blocked.Any(lower.Contains);
    }

    private sealed record AiSearchRoot(AiSearchUniversityResult[]? Universities);

    private static AiSearchUniversityResult MapStrictResult(StrictProgramResearchResult result)
    {
        var scholarship = result.Scholarships?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ScholarshipName));
        var deadline = result.Deadlines?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Deadline));
        var sources = result.OfficialSources?.Where(IsHttpUrl).Select(Clean).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var language = BuildLanguage(result.LanguageRequirements);
        var fee = BuildFee(result.TuitionFee, result.ApplicationFee);
        var eligibility = BuildEligibility(result);

        return new AiSearchUniversityResult(
            Clean(result.UniversityName),
            Clean(result.Country),
            Clean(result.City),
            sources.FirstOrDefault() ?? "Not found on official page",
            Clean(result.ApplicationPortal),
            [
                new AiSearchProgramResult(
                    Clean(result.ProgramName),
                    Clean(result.DegreeLevel),
                    Clean(result.Faculty),
                    language,
                    deadline is null ? "Not found on official page" : $"{Clean(deadline.Round)}: {Clean(deadline.Deadline)} ({Clean(deadline.Status)})",
                    fee,
                    result.Scholarships?.Length > 0,
                    scholarship is null ? "Not found on official page" : $"{Clean(scholarship.ScholarshipName)} | {Clean(scholarship.Coverage)} | {Clean(scholarship.Amount)} | Deadline: {Clean(scholarship.Deadline)} | Eligibility: {Clean(scholarship.Eligibility)}",
                    eligibility,
                    result.AdmissionRequirements?.RequiredDocuments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Clean).ToArray() ?? [],
                    sources)
            ]);
    }

    private static string BuildLanguage(StrictLanguageRequirements? language)
    {
        if (language is null) return "Not found on official page";
        var tests = language.AcceptedTests?.Select(x => $"{Clean(x.TestName)} {Clean(x.MinimumScore)}").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
        var parts = new List<string>
        {
            $"English required: {language.EnglishRequired}",
            $"Minimum level: {Clean(language.MinimumLevel)}",
            $"MOI accepted: {language.MoiAccepted}",
            $"MOI details: {Clean(language.MoiDetails)}"
        };
        if (tests.Length > 0) parts.Add("Accepted tests: " + string.Join(", ", tests));
        return string.Join(" | ", parts);
    }

    private static string BuildFee(StrictTuitionFee? tuition, StrictApplicationFee? application)
    {
        var tuitionText = tuition is null
            ? "Tuition: Not found on official page"
            : $"Tuition: {CleanFlexibleValue(tuition.AnnualFee)} {Clean(tuition.Currency)}. {Clean(tuition.Notes)}";
        var applicationText = application is null
            ? "Application fee: Not found on official page"
            : $"Application fee required: {application.Required}; amount: {CleanFlexibleValue(application.Amount)} {Clean(application.Currency)}";
        return $"{tuitionText} | {applicationText}";
    }

    private static string BuildEligibility(StrictProgramResearchResult result)
    {
        var admission = result.AdmissionRequirements;
        var risks = result.Risks?.Select(x => $"{Clean(x.RiskType)} - {Clean(x.Details)} ({Clean(x.Severity)})").ToArray() ?? [];
        var advantages = result.Advantages?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Clean).ToArray() ?? [];
        var parts = new List<string>
        {
            $"Previous degree: {Clean(admission?.PreviousDegreeRequired)}",
            $"Minimum GPA: {Clean(admission?.MinimumGpa)}",
            $"Entry test: {(admission?.EntryTestRequired ?? false ? Clean(admission?.EntryTestName) : "Not required")}",
            $"Admission chance: {Clean(result.OverallAssessment?.AdmissionChance)}",
            $"Scholarship chance: {Clean(result.OverallAssessment?.ScholarshipChance)}",
            $"Summary: {Clean(result.OverallAssessment?.Summary)}"
        };
        if (advantages.Length > 0) parts.Add("Advantages: " + string.Join("; ", advantages));
        if (risks.Length > 0) parts.Add("Risks: " + string.Join("; ", risks));
        return string.Join(" | ", parts);
    }

    private sealed record StrictProgramResearchResult(
        [property: System.Text.Json.Serialization.JsonPropertyName("university_name")] string? UniversityName,
        [property: System.Text.Json.Serialization.JsonPropertyName("country")] string? Country,
        [property: System.Text.Json.Serialization.JsonPropertyName("city")] string? City,
        [property: System.Text.Json.Serialization.JsonPropertyName("program_name")] string? ProgramName,
        [property: System.Text.Json.Serialization.JsonPropertyName("degree_level")] string? DegreeLevel,
        [property: System.Text.Json.Serialization.JsonPropertyName("faculty")] string? Faculty,
        [property: System.Text.Json.Serialization.JsonPropertyName("study_language")] string? StudyLanguage,
        [property: System.Text.Json.Serialization.JsonPropertyName("duration")] string? Duration,
        [property: System.Text.Json.Serialization.JsonPropertyName("intake_year")] string? IntakeYear,
        [property: System.Text.Json.Serialization.JsonPropertyName("application_portal")] string? ApplicationPortal,
        [property: System.Text.Json.Serialization.JsonPropertyName("application_fee")] StrictApplicationFee? ApplicationFee,
        [property: System.Text.Json.Serialization.JsonPropertyName("tuition_fee")] StrictTuitionFee? TuitionFee,
        [property: System.Text.Json.Serialization.JsonPropertyName("deadlines")] StrictDeadline[]? Deadlines,
        [property: System.Text.Json.Serialization.JsonPropertyName("language_requirements")] StrictLanguageRequirements? LanguageRequirements,
        [property: System.Text.Json.Serialization.JsonPropertyName("admission_requirements")] StrictAdmissionRequirements? AdmissionRequirements,
        [property: System.Text.Json.Serialization.JsonPropertyName("scholarships")] StrictScholarship[]? Scholarships,
        [property: System.Text.Json.Serialization.JsonPropertyName("estimated_living_cost")] StrictLivingCost? EstimatedLivingCost,
        [property: System.Text.Json.Serialization.JsonPropertyName("risks")] StrictRisk[]? Risks,
        [property: System.Text.Json.Serialization.JsonPropertyName("advantages")] string[]? Advantages,
        [property: System.Text.Json.Serialization.JsonPropertyName("official_sources")] string[]? OfficialSources,
        [property: System.Text.Json.Serialization.JsonPropertyName("overall_assessment")] StrictOverallAssessment? OverallAssessment);

    private static string CleanFlexibleValue(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "Not found on official page";

        return value.Value.ValueKind == JsonValueKind.String
            ? Clean(value.Value.GetString())
            : value.Value.GetRawText();
    }

    private sealed record StrictApplicationFee(bool Required, JsonElement? Amount, string? Currency);
    private sealed record StrictTuitionFee([property: System.Text.Json.Serialization.JsonPropertyName("annual_fee")] JsonElement? AnnualFee, string? Currency, string? Notes);
    private sealed record StrictDeadline(string? Round, string? Deadline, string? Status);
    private sealed record StrictLanguageRequirements(
        [property: System.Text.Json.Serialization.JsonPropertyName("english_required")] bool EnglishRequired,
        [property: System.Text.Json.Serialization.JsonPropertyName("minimum_level")] string? MinimumLevel,
        [property: System.Text.Json.Serialization.JsonPropertyName("accepted_tests")] StrictAcceptedTest[]? AcceptedTests,
        [property: System.Text.Json.Serialization.JsonPropertyName("moi_accepted")] bool MoiAccepted,
        [property: System.Text.Json.Serialization.JsonPropertyName("moi_details")] string? MoiDetails);
    private sealed record StrictAcceptedTest([property: System.Text.Json.Serialization.JsonPropertyName("test_name")] string? TestName, [property: System.Text.Json.Serialization.JsonPropertyName("minimum_score")] string? MinimumScore);
    private sealed record StrictAdmissionRequirements(
        [property: System.Text.Json.Serialization.JsonPropertyName("previous_degree_required")] string? PreviousDegreeRequired,
        [property: System.Text.Json.Serialization.JsonPropertyName("minimum_gpa")] string? MinimumGpa,
        [property: System.Text.Json.Serialization.JsonPropertyName("required_documents")] string[]? RequiredDocuments,
        [property: System.Text.Json.Serialization.JsonPropertyName("entry_test_required")] bool EntryTestRequired,
        [property: System.Text.Json.Serialization.JsonPropertyName("entry_test_name")] string? EntryTestName);
    private sealed record StrictScholarship([property: System.Text.Json.Serialization.JsonPropertyName("scholarship_name")] string? ScholarshipName, string? Coverage, string? Amount, string? Deadline, string? Eligibility);
    private sealed record StrictLivingCost(JsonElement? Monthly, string? Currency);
    private sealed record StrictRisk([property: System.Text.Json.Serialization.JsonPropertyName("risk_type")] string? RiskType, string? Details, string? Severity);
    private sealed record StrictOverallAssessment([property: System.Text.Json.Serialization.JsonPropertyName("admission_chance")] string? AdmissionChance, [property: System.Text.Json.Serialization.JsonPropertyName("scholarship_chance")] string? ScholarshipChance, string? Summary);
}

public sealed class ManualDeepSeekProvider : AiSearchProviderBase
{
    public override string Name => "ManualDeepSeek";

    public override Task<AiSearchParseResponse> SearchUniversitiesAsync(AiSearchCriteriaRequest request, ApplicantProfile profile, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AiSearchParseResponse(false, ["ManualDeepSeek mode does not call any API. Generate the prompt, paste it into DeepSeek manually, then paste the JSON response back here."], []));
    }
}

public sealed class MockAiSearchProvider : AiSearchProviderBase
{
    public override string Name => "Mock";

    public override Task<AiSearchParseResponse> SearchUniversitiesAsync(AiSearchCriteriaRequest request, ApplicantProfile profile, CancellationToken cancellationToken)
    {
        var result = new AiSearchParseResponse(true, [], [
            new AiSearchUniversityResult(
                "Sample Technical University",
                request.CountryPreference ?? "Germany",
                "Sample City",
                "https://example.edu",
                "https://example.edu/apply",
                [
                    new AiSearchProgramResult(
                        "Computer Science",
                        request.DegreeLevel,
                        "Computer Science",
                        "English, B2 required. MOI may be reviewed case by case.",
                        "Not found on official page",
                        "Not found on official page",
                        true,
                        "Mock scholarship for local development only.",
                        "Good fit for Software Engineering, microservices, distributed systems and full-stack development.",
                        ["CV", "Passport", "Transcript", "Degree", "English proficiency certificate"],
                        ["https://example.edu/programs/computer-science"])
                ])
        ]);
        return Task.FromResult(result);
    }
}

public sealed class DeepSeekApiProvider(HttpClient httpClient, IConfiguration configuration) : AiSearchProviderBase
{
    public override string Name => "DeepSeekApi";

    public override async Task<AiSearchParseResponse> SearchUniversitiesAsync(AiSearchCriteriaRequest request, ApplicantProfile profile, CancellationToken cancellationToken)
    {
        var apiKey = configuration["AiProvider:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiSearchParseResponse(false, ["AI provider is not configured. Add API key or switch to Manual mode."], []);

        var endpoint = configuration["AiProvider:BaseUrl"] ?? "https://api.deepseek.com/chat/completions";
        var model = configuration["AiProvider:Model"] ?? "deepseek-chat";
        var payload = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "Return strict JSON only. Use official sources only. Do not invent data." },
                new { role = "user", content = GenerateSearchPrompt(request, profile) }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new AiSearchParseResponse(false, [$"API request failed: {(int)response.StatusCode} {body}"], []);

        var chat = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        var content = chat?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        return ParseSearchResponse(content);
    }

    private sealed record ChatCompletionResponse(ChatChoice[]? Choices);
    private sealed record ChatChoice(ChatMessage? Message);
    private sealed record ChatMessage(string? Content);
}

public sealed class OpenAiProvider(HttpClient httpClient, IConfiguration configuration) : AiSearchProviderBase
{
    public override string Name => "OpenAi";

    public override async Task<AiSearchParseResponse> SearchUniversitiesAsync(AiSearchCriteriaRequest request, ApplicantProfile profile, CancellationToken cancellationToken)
    {
        var apiKey = configuration["AiProvider:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiSearchParseResponse(false, ["AI provider is not configured. Add API key or switch to Manual mode."], []);

        var endpoint = configuration["AiProvider:BaseUrl"] ?? "https://api.openai.com/v1/chat/completions";
        var model = configuration["AiProvider:Model"] ?? "gpt-4.1-mini";
        var payload = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "Return strict JSON only. Use official sources only. Do not invent data." },
                new { role = "user", content = GenerateSearchPrompt(request, profile) }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new AiSearchParseResponse(false, [$"API request failed: {(int)response.StatusCode} {body}"], []);

        var chat = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        var content = chat?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        return ParseSearchResponse(content);
    }

    private sealed record ChatCompletionResponse(ChatChoice[]? Choices);
    private sealed record ChatChoice(ChatMessage? Message);
    private sealed record ChatMessage(string? Content);
}
