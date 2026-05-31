namespace ScholarshipAutoFill.Api.Domain;

public sealed class ApplicantProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string Nationality { get; set; } = "";
    public string Gender { get; set; } = "";
    public string PassportNumber { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string PlaceOfBirth { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string HighestDegree { get; set; } = "";
    public string University { get; set; } = "";
    public string Cgpa { get; set; } = "";
    public int? GraduationYear { get; set; }
    public string IeltsOverall { get; set; } = "";
    public string IeltsListening { get; set; } = "";
    public string IeltsReading { get; set; } = "";
    public string IeltsWriting { get; set; } = "";
    public string IeltsSpeaking { get; set; } = "";
    public string CefrLevel { get; set; } = "";
    public string ExperienceSummary { get; set; } = "";
    public string Skills { get; set; } = "";
    public string ResearchInterests { get; set; } = "";
    public string ScholarshipPreferences { get; set; } = "";
    public string PreferredRegions { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
