using Microsoft.EntityFrameworkCore;
using ScholarshipAutoFill.Api.Domain;

namespace ScholarshipAutoFill.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicantProfile> ApplicantProfiles => Set<ApplicantProfile>();
    public DbSet<ScholarshipAnalysis> ScholarshipAnalyses => Set<ScholarshipAnalysis>();
    public DbSet<RecommendedProgram> RecommendedPrograms => Set<RecommendedProgram>();
    public DbSet<ResearchSource> ResearchSources => Set<ResearchSource>();
    public DbSet<ScholarshipOpportunity> ScholarshipOpportunities => Set<ScholarshipOpportunity>();
    public DbSet<AnalysisLog> AnalysisLogs => Set<AnalysisLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicantProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(250);
            entity.Property(x => x.PassportNumber).HasMaxLength(80);
            entity.Property(x => x.NationalId).HasMaxLength(80);
        });

        modelBuilder.Entity<ScholarshipAnalysis>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasMany(x => x.RecommendedPrograms).WithOne().HasForeignKey(x => x.ScholarshipAnalysisId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Sources).WithOne().HasForeignKey(x => x.ScholarshipAnalysisId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Scholarships).WithOne().HasForeignKey(x => x.ScholarshipAnalysisId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Logs).WithOne().HasForeignKey(x => x.ScholarshipAnalysisId).OnDelete(DeleteBehavior.Cascade);
        });

        SeedProfile(modelBuilder);
    }

    private static void SeedProfile(ModelBuilder modelBuilder)
    {
        var seedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<ApplicantProfile>().HasData(new ApplicantProfile
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Muhammad Ubaid Khan",
            DateOfBirth = new DateOnly(1998, 12, 23),
            Nationality = "Pakistani",
            Gender = "Male",
            PassportNumber = "NG4153372",
            NationalId = "35200 2614337 1",
            PlaceOfBirth = "Mansehra, Pakistan",
            Email = "Ubaidkhank1998@gmail.com",
            Phone = "+92 3125300827",
            Address = "Mohallah Dodal Shinkiari, Tehsil Baffa, District Mansehra, Pakistan",
            HighestDegree = "BS Software Engineering",
            University = "Hazara University Mansehra",
            Cgpa = "3.46 / 4.0",
            GraduationYear = 2021,
            IeltsOverall = "5.5",
            IeltsListening = "5.5",
            IeltsReading = "5.0",
            IeltsWriting = "5.5",
            IeltsSpeaking = "6.5",
            CefrLevel = "B2",
            ExperienceSummary = "2.5+ years as Software Engineer and Full Stack Developer",
            Skills = ".NET Core, ASP.NET Core, Angular, Microservices, Docker, Kubernetes, Jenkins, RabbitMQ, Redis, WSO2, SQL Server, PostgreSQL, FinTech systems, Telecom systems, Distributed architecture, API integrations, Cloud deployment",
            ResearchInterests = "Distributed systems, microservices architecture, full-stack development, AI-driven software engineering",
            ScholarshipPreferences = "Fully funded or partially funded Master's scholarships. MOI-friendly universities preferred.",
            PreferredRegions = "Germany, Italy, Malaysia, Korea, China",
            CreatedAtUtc = seedTimestamp,
            UpdatedAtUtc = seedTimestamp
        });
    }
}
