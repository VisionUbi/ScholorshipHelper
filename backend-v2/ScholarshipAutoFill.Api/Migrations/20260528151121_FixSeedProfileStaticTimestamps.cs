using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarshipAutoFill.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedProfileStaticTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApplicantProfiles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApplicantProfiles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { new DateTime(2026, 5, 28, 7, 54, 52, 495, DateTimeKind.Utc).AddTicks(3651), new DateTime(2026, 5, 28, 7, 54, 52, 495, DateTimeKind.Utc).AddTicks(3656) });
        }
    }
}
