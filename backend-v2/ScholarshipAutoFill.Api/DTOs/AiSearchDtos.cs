namespace ScholarshipAutoFill.Api.DTOs;

public sealed record AiSearchCriteriaRequest(
    string Query,
    string DegreeLevel,
    string? FieldPreference,
    string? CountryPreference,
    string? ScholarshipPreference);

public sealed record AiSearchPromptResponse(
    string Provider,
    string Prompt,
    string ExpectedJsonShape);

public sealed record AiSearchProviderStatusResponse(
    string ConfiguredProvider,
    string ActiveProvider,
    bool ApiKeyConfigured,
    bool IsManualMode,
    string Message);

public sealed record AiSearchParseRequest(string JsonResponse);

public sealed record AiSearchParseResponse(
    bool IsValid,
    string[] Errors,
    AiSearchUniversityResult[] Universities,
    Guid? AnalysisId = null);

public sealed record AiSearchSaveRequest(
    AiSearchCriteriaRequest Criteria,
    AiSearchUniversityResult[] Universities,
    string[]? SelectedProgramKeys);

public sealed record AiSearchSaveResponse(Guid AnalysisId, int SavedPrograms, string Message);

public sealed record AiSearchUniversityResult(
    string UniversityName,
    string Country,
    string City,
    string OfficialWebsite,
    string ApplicationPortal,
    AiSearchProgramResult[] Programs);

public sealed record AiSearchProgramResult(
    string ProgramName,
    string DegreeLevel,
    string Field,
    string Language,
    string Deadline,
    string TuitionFee,
    bool ScholarshipsAvailable,
    string ScholarshipDetails,
    string Eligibility,
    string[] RequiredDocuments,
    string[] SourceUrls);
