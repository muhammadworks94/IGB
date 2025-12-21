using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementsPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    TargetStudentUserId = table.Column<long>(type: "bigint", nullable: true),
                    TargetTutorUserId = table.Column<long>(type: "bigint", nullable: true),
                    TargetGuardianUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_Users_TargetGuardianUserId",
                        column: x => x.TargetGuardianUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Announcements_Users_TargetStudentUserId",
                        column: x => x.TargetStudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Announcements_Users_TargetTutorUserId",
                        column: x => x.TargetTutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Audience_IsPublished_PublishAtUtc_IsDeleted",
                table: "Announcements",
                columns: new[] { "Audience", "IsPublished", "PublishAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetGuardianUserId",
                table: "Announcements",
                column: "TargetGuardianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetStudentUserId",
                table: "Announcements",
                column: "TargetStudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetTutorUserId",
                table: "Announcements",
                column: "TargetTutorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");
        }
    }
}
