namespace ScholarshipAutoFill.Api.Domain;

public sealed class ScholarshipAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Input { get; set; } = "";
    public string UniversityName { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string Summary { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<RecommendedProgram> RecommendedPrograms { get; set; } = [];
    public List<ResearchSource> Sources { get; set; } = [];
    public List<ScholarshipOpportunity> Scholarships { get; set; } = [];
    public List<AnalysisLog> Logs { get; set; } = [];
}

public sealed class RecommendedProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScholarshipAnalysisId { get; set; }
    public string Name { get; set; } = "";
    public string DegreeLevel { get; set; } = "";
    public decimal Score { get; set; }
    public string Scholarship { get; set; } = "";
    public string Fee { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string EnglishOrMoi { get; set; } = "";
    public string Risks { get; set; } = "";
    public string SourcesJson { get; set; } = "[]";
}

public sealed class ResearchSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScholarshipAnalysisId { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string ExtractedText { get; set; } = "";
    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ScholarshipOpportunity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScholarshipAnalysisId { get; set; }
    public string Name { get; set; } = "";
    public string Amount { get; set; } = "";
    public string Eligibility { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string SourceUrl { get; set; } = "";
}

public sealed class AnalysisLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScholarshipAnalysisId { get; set; }
    public string Step { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
