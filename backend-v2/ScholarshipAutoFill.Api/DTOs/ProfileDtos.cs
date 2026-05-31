using System.ComponentModel.DataAnnotations;

namespace ScholarshipAutoFill.Api.DTOs;

public sealed record ApplicantProfileDto(
    Guid Id,
    string FullName,
    DateOnly? DateOfBirth,
    string Nationality,
    string Gender,
    string PassportNumber,
    string NationalId,
    string PlaceOfBirth,
    string Email,
    string Phone,
    string Address,
    string HighestDegree,
    string University,
    string Cgpa,
    int? GraduationYear,
    string IeltsOverall,
    string IeltsListening,
    string IeltsReading,
    string IeltsWriting,
    string IeltsSpeaking,
    string CefrLevel,
    string ExperienceSummary,
    string Skills,
    string ResearchInterests,
    string ScholarshipPreferences,
    string PreferredRegions);

public sealed class UpsertApplicantProfileRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string Nationality { get; set; } = "";
    public string Gender { get; set; } = "";
    public string PassportNumber { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string PlaceOfBirth { get; set; } = "";
    [EmailAddress]
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
}
