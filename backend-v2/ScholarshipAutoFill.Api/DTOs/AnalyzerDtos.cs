namespace ScholarshipAutoFill.Api.DTOs;

public sealed record ScholarshipAnalyzeRequest(
    string Url,
    string ProgramName,
    string DegreeLevel,
    string? CountryPreference,
    string? FieldPreference,
    string? ManualContent);

public sealed record ScholarshipAnalyzeResponse(
    Guid AnalysisId,
    Guid ApplicationId,
    string UniversityName,
    bool IsEligible,
    decimal EligibilityScoreOutOf10,
    string CrawlStatus,
    string CrawlerModeUsed,
    long ElapsedMs,
    string MinimumIeltsRequirement,
    string ApplicationFee,
    RecommendedProgramResult[] RecommendedPrograms,
    string[] Reasons,
    string[] Gaps,
    string[] RequiredDocuments,
    ResearchSourceResult[] Sources);

public sealed record RecommendedProgramResult(
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
    string[] MissingRequirementsOrRisks,
    string[] SourceUrls);

public sealed record ResearchSourceResult(string Url, string Title, string SourceType);

public sealed record AggregatedSource(string Url, string Title, string SourceType, string Text, bool FetchSucceeded, string? Error);
