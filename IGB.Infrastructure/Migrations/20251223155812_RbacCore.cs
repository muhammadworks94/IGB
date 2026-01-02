using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RbacCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RbacPermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RbacPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RbacRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RbacRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RbacRolePermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RbacRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RbacRolePermissions_RbacPermissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "RbacPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RbacRolePermissions_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRbacRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRbacRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRbacRoles_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRbacRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RbacPermissions_Key",
                table: "RbacPermissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RbacRolePermissions_PermissionId",
                table: "RbacRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RbacRolePermissions_RoleId_PermissionId_IsDeleted",
                table: "RbacRolePermissions",
                columns: new[] { "RoleId", "PermissionId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RbacRoles_Name",
                table: "RbacRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRbacRoles_RoleId",
                table: "UserRbacRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRbacRoles_UserId_RoleId_IsDeleted",
                table: "UserRbacRoles",
                columns: new[] { "UserId", "RoleId", "IsDeleted" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RbacRolePermissions");

            migrationBuilder.DropTable(
                name: "UserRbacRoles");

            migrationBuilder.DropTable(
                name: "RbacPermissions");

            migrationBuilder.DropTable(
                name: "RbacRoles");
        }
    }
}
