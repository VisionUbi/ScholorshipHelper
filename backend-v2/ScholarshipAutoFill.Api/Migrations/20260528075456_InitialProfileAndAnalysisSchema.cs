using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarshipAutoFill.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialProfileAndAnalysisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicantProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    PassportNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NationalId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    HighestDegree = table.Column<string>(type: "text", nullable: false),
                    University = table.Column<string>(type: "text", nullable: false),
                    Cgpa = table.Column<string>(type: "text", nullable: false),
                    GraduationYear = table.Column<int>(type: "integer", nullable: true),
                    IeltsOverall = table.Column<string>(type: "text", nullable: false),
                    IeltsListening = table.Column<string>(type: "text", nullable: false),
                    IeltsReading = table.Column<string>(type: "text", nullable: false),
                    IeltsWriting = table.Column<string>(type: "text", nullable: false),
                    IeltsSpeaking = table.Column<string>(type: "text", nullable: false),
                    CefrLevel = table.Column<string>(type: "text", nullable: false),
                    ExperienceSummary = table.Column<string>(type: "text", nullable: false),
                    Skills = table.Column<string>(type: "text", nullable: false),
                    ResearchInterests = table.Column<string>(type: "text", nullable: false),
                    ScholarshipPreferences = table.Column<string>(type: "text", nullable: false),
                    PreferredRegions = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScholarshipAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    UniversityName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScholarshipAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScholarshipAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisLogs_ScholarshipAnalyses_ScholarshipAnalysisId",
                        column: x => x.ScholarshipAnalysisId,
                        principalTable: "ScholarshipAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecommendedPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScholarshipAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DegreeLevel = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    Scholarship = table.Column<string>(type: "text", nullable: false),
                    Fee = table.Column<string>(type: "text", nullable: false),
                    Deadline = table.Column<string>(type: "text", nullable: false),
                    EnglishOrMoi = table.Column<string>(type: "text", nullable: false),
                    Risks = table.Column<string>(type: "text", nullable: false),
                    SourcesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendedPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecommendedPrograms_ScholarshipAnalyses_ScholarshipAnalysis~",
                        column: x => x.ScholarshipAnalysisId,
                        principalTable: "ScholarshipAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResearchSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScholarshipAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchSources_ScholarshipAnalyses_ScholarshipAnalysisId",
                        column: x => x.ScholarshipAnalysisId,
                        principalTable: "ScholarshipAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScholarshipOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScholarshipAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<string>(type: "text", nullable: false),
                    Eligibility = table.Column<string>(type: "text", nullable: false),
                    Deadline = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScholarshipOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScholarshipOpportunities_ScholarshipAnalyses_ScholarshipAna~",
                        column: x => x.ScholarshipAnalysisId,
                        principalTable: "ScholarshipAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ApplicantProfiles",
                columns: new[] { "Id", "Address", "CefrLevel", "Cgpa", "CreatedAtUtc", "DateOfBirth", "Email", "ExperienceSummary", "FullName", "Gender", "GraduationYear", "HighestDegree", "IeltsListening", "IeltsOverall", "IeltsReading", "IeltsSpeaking", "IeltsWriting", "NationalId", "Nationality", "PassportNumber", "Phone", "PlaceOfBirth", "PreferredRegions", "ResearchInterests", "ScholarshipPreferences", "Skills", "University", "UpdatedAtUtc" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "Mohallah Dodal Shinkiari, Tehsil Baffa, District Mansehra, Pakistan", "B2", "3.46 / 4.0", new DateTime(2026, 5, 28, 7, 54, 52, 495, DateTimeKind.Utc).AddTicks(3651), new DateOnly(1998, 12, 23), "Ubaidkhank1998@gmail.com", "2.5+ years as Software Engineer and Full Stack Developer", "Muhammad Ubaid Khan", "Male", 2021, "BS Software Engineering", "5.5", "5.5", "5.0", "6.5", "5.5", "35200 2614337 1", "Pakistani", "NG4153372", "+92 3125300827", "Mansehra, Pakistan", "Germany, Italy, Malaysia, Korea, China", "Distributed systems, microservices architecture, full-stack development, AI-driven software engineering", "Fully funded or partially funded Master's scholarships. MOI-friendly universities preferred.", ".NET Core, ASP.NET Core, Angular, Microservices, Docker, Kubernetes, Jenkins, RabbitMQ, Redis, WSO2, SQL Server, PostgreSQL, FinTech systems, Telecom systems, Distributed architecture, API integrations, Cloud deployment", "Hazara University Mansehra", new DateTime(2026, 5, 28, 7, 54, 52, 495, DateTimeKind.Utc).AddTicks(3656) });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisLogs_ScholarshipAnalysisId",
                table: "AnalysisLogs",
                column: "ScholarshipAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendedPrograms_ScholarshipAnalysisId",
                table: "RecommendedPrograms",
                column: "ScholarshipAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchSources_ScholarshipAnalysisId",
                table: "ResearchSources",
                column: "ScholarshipAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ScholarshipOpportunities_ScholarshipAnalysisId",
                table: "ScholarshipOpportunities",
                column: "ScholarshipAnalysisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisLogs");

            migrationBuilder.DropTable(
                name: "ApplicantProfiles");

            migrationBuilder.DropTable(
                name: "RecommendedPrograms");

            migrationBuilder.DropTable(
                name: "ResearchSources");

            migrationBuilder.DropTable(
                name: "ScholarshipOpportunities");

            migrationBuilder.DropTable(
                name: "ScholarshipAnalyses");
        }
    }
}
