using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ScholarshipAutoFill.Api.Data;
using ScholarshipAutoFill.Api.Domain;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface IAiResearchService
{
    Task<ScholarshipAnalyzeResponse> AnalyzeAsync(ScholarshipAnalyzeRequest request, CancellationToken cancellationToken);
}

public sealed class AiResearchService(
    AppDbContext db,
    IOfficialSourceSearchService sourceSearch,
    IContentAggregationService contentAggregation,
    IProgramRecommendationService recommendationService,
    ILlmAnalysisService llmAnalysisService) : IAiResearchService
{
    public async Task<ScholarshipAnalyzeResponse> AnalyzeAsync(ScholarshipAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var profile = await db.ApplicantProfiles.OrderBy(x => x.CreatedAtUtc).FirstAsync(cancellationToken);
        var sourceResults = await sourceSearch.FindSourcesAsync(request, cancellationToken);
        var sources = await contentAggregation.FetchSourcesAsync(sourceResults, cancellationToken);
        var llmRecommendations = await llmAnalysisService.TryBuildRecommendationsAsync(profile, request, sources, cancellationToken);
        var recommendations = llmRecommendations ?? recommendationService.BuildRecommendations(profile, sources, request);
        sw.Stop();

        var analysis = new ScholarshipAnalysis
        {
            Input = request.Url,
            UniversityName = GuessUniversity(request, sources),
            Status = recommendations.Length > 0 ? "Recommendations Found" : "Needs Research",
            Summary = recommendations.Length > 0
                ? $"Found {recommendations.Length} relevant programme(s) from official sources."
                : "No relevant programmes were extracted from official sources."
        };

        foreach (var source in sources)
        {
            analysis.Sources.Add(new ResearchSource
            {
                Url = source.Url,
                Title = source.Title,
                SourceType = source.SourceType,
                ExtractedText = source.Text.Length <= 5000 ? source.Text : source.Text[..5000]
            });
        }

        foreach (var item in recommendations)
        {
            analysis.RecommendedPrograms.Add(new RecommendedProgram
            {
                Name = item.ProgramName,
                DegreeLevel = item.DegreeLevel,
                Score = item.EligibilityMatchScore,
                Scholarship = item.ScholarshipAvailability,
                Fee = item.TuitionFee,
                Deadline = item.Deadline,
                EnglishOrMoi = $"{item.EnglishRequirement} | MOI: {item.MoiAcceptance}",
                Risks = string.Join(Environment.NewLine, item.MissingRequirementsOrRisks),
                SourcesJson = JsonSerializer.Serialize(item.SourceUrls)
            });
        }

        analysis.Logs.Add(new AnalysisLog { Step = "OfficialSourceSearch", Message = $"Selected {sourceResults.Count} official candidate sources." });
        analysis.Logs.Add(new AnalysisLog { Step = "ContentAggregation", Message = $"Fetched {sources.Count(x => x.FetchSucceeded)} of {sources.Count} sources successfully." });
        analysis.Logs.Add(new AnalysisLog
        {
            Step = "Recommendation",
            Message = llmRecommendations is null
                ? $"Built {recommendations.Length} recommendations from local official-source extraction."
                : $"Built {recommendations.Length} recommendations from DeepSeek official-source extraction."
        });

        db.ScholarshipAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);

        return new ScholarshipAnalyzeResponse(
            analysis.Id,
            analysis.Id,
            analysis.UniversityName,
            recommendations.Any(x => x.EligibilityMatchScore >= 6.5m),
            recommendations.Length == 0 ? 0 : recommendations.Max(x => x.EligibilityMatchScore),
            sources.Any(x => x.FetchSucceeded) ? "SUCCESS" : "FAILED",
            llmRecommendations is null ? "AI_RESEARCH_OFFICIAL_SOURCES" : "DEEPSEEK_OFFICIAL_SOURCE_EXTRACTION",
            sw.ElapsedMilliseconds,
            ExtractOverallEnglish(recommendations),
            ExtractOverallFee(recommendations),
            recommendations,
            BuildReasons(recommendations, sources),
            BuildGaps(recommendations, sources),
            ["CV", "Passport", "Transcript", "Degree", "English proficiency certificate"],
            sources.Select(x => new ResearchSourceResult(x.Url, x.Title, x.SourceType)).ToArray());
    }

    private static string GuessUniversity(ScholarshipAnalyzeRequest request, IReadOnlyList<AggregatedSource> sources)
    {
        var title = sources.Select(x => x.Title)
            .FirstOrDefault(x => x.Contains("University", StringComparison.OrdinalIgnoreCase) || x.Contains("Universit", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(title))
            return CleanUniversityName(title);

        return Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ? uri.Host : request.Url ?? "Unknown university";
    }

    private static string CleanUniversityName(string title)
    {
        var parts = Regex
            .Split(title, @"\s*[\|\-–—]\s*")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray();
        var candidate = parts.FirstOrDefault(x => x.Contains("University", StringComparison.OrdinalIgnoreCase) || x.Contains("Universit", StringComparison.OrdinalIgnoreCase)) ?? title;
        return candidate.Trim();
    }

    private static string ExtractOverallEnglish(RecommendedProgramResult[] recommendations)
    {
        var value = recommendations.Select(x => x.EnglishRequirement).FirstOrDefault(x => !x.StartsWith("Not found", StringComparison.OrdinalIgnoreCase));
        return value ?? "Not found on official page";
    }

    private static string ExtractOverallFee(RecommendedProgramResult[] recommendations)
    {
        var value = recommendations.Select(x => x.TuitionFee).FirstOrDefault(x => !x.StartsWith("Not found", StringComparison.OrdinalIgnoreCase));
        return value ?? "Not found on official page";
    }

    private static string[] BuildReasons(RecommendedProgramResult[] recommendations, IReadOnlyList<AggregatedSource> sources)
    {
        var reasons = new List<string>();
        if (recommendations.Length > 0) reasons.Add($"Recommended {recommendations.Length} programme(s) from official university sources.");
        if (sources.Any(x => x.SourceType == "SCHOLARSHIP_FEE")) reasons.Add("Official scholarship/fee source was included.");
        reasons.Add("Scoring used saved profile: BS Software Engineering, CGPA 3.46, IELTS 5.5, full-stack and microservices experience.");
        return reasons.ToArray();
    }

    private static string[] BuildGaps(RecommendedProgramResult[] recommendations, IReadOnlyList<AggregatedSource> sources)
    {
        var gaps = new List<string>();
        if (recommendations.Length == 0) gaps.Add("No relevant official programme pages were found or extracted.");
        if (sources.Any(x => !x.FetchSucceeded)) gaps.Add("Some official sources could not be fetched by the backend. Their URLs are still included for manual verification.");
        gaps.Add("Use official programme pages to verify exact deadline, English/MOI rules, fees and non-EU visa applicant timeline.");
        return gaps.ToArray();
    }
}
