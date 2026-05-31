using Microsoft.EntityFrameworkCore;
using ScholarshipAutoFill.Api.Data;
using ScholarshipAutoFill.Api.Domain;
using ScholarshipAutoFill.Api.DTOs;

namespace ScholarshipAutoFill.Api.Services;

public interface IProfileService
{
    Task<ApplicantProfileDto?> GetAsync(CancellationToken cancellationToken);
    Task<ApplicantProfileDto> UpsertAsync(UpsertApplicantProfileRequest request, CancellationToken cancellationToken);
}

public sealed class ProfileService(AppDbContext db) : IProfileService
{
    public async Task<ApplicantProfileDto?> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await db.ApplicantProfiles.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ApplicantProfileDto> UpsertAsync(UpsertApplicantProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.ApplicantProfiles.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new ApplicantProfile();
            db.ApplicantProfiles.Add(profile);
        }

        profile.FullName = request.FullName.Trim();
        profile.DateOfBirth = request.DateOfBirth;
        profile.Nationality = request.Nationality.Trim();
        profile.Gender = request.Gender.Trim();
        profile.PassportNumber = request.PassportNumber.Trim();
        profile.NationalId = request.NationalId.Trim();
        profile.PlaceOfBirth = request.PlaceOfBirth.Trim();
        profile.Email = request.Email.Trim();
        profile.Phone = request.Phone.Trim();
        profile.Address = request.Address.Trim();
        profile.HighestDegree = request.HighestDegree.Trim();
        profile.University = request.University.Trim();
        profile.Cgpa = request.Cgpa.Trim();
        profile.GraduationYear = request.GraduationYear;
        profile.IeltsOverall = request.IeltsOverall.Trim();
        profile.IeltsListening = request.IeltsListening.Trim();
        profile.IeltsReading = request.IeltsReading.Trim();
        profile.IeltsWriting = request.IeltsWriting.Trim();
        profile.IeltsSpeaking = request.IeltsSpeaking.Trim();
        profile.CefrLevel = request.CefrLevel.Trim();
        profile.ExperienceSummary = request.ExperienceSummary.Trim();
        profile.Skills = request.Skills.Trim();
        profile.ResearchInterests = request.ResearchInterests.Trim();
        profile.ScholarshipPreferences = request.ScholarshipPreferences.Trim();
        profile.PreferredRegions = request.PreferredRegions.Trim();
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    private static ApplicantProfileDto ToDto(ApplicantProfile profile) => new(
        profile.Id,
        profile.FullName,
        profile.DateOfBirth,
        profile.Nationality,
        profile.Gender,
        profile.PassportNumber,
        profile.NationalId,
        profile.PlaceOfBirth,
        profile.Email,
        profile.Phone,
        profile.Address,
        profile.HighestDegree,
        profile.University,
        profile.Cgpa,
        profile.GraduationYear,
        profile.IeltsOverall,
        profile.IeltsListening,
        profile.IeltsReading,
        profile.IeltsWriting,
        profile.IeltsSpeaking,
        profile.CefrLevel,
        profile.ExperienceSummary,
        profile.Skills,
        profile.ResearchInterests,
        profile.ScholarshipPreferences,
        profile.PreferredRegions);
}
