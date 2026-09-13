using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Shared
{
    /// <inheritdoc />
    public partial class InitialSharedGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "shared",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ServerModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminClaimedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.GroupId);
                });

            migrationBuilder.CreateTable(
                name: "member_claims",
                schema: "shared",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeviceHandle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_claims", x => new { x.GroupId, x.MemberId });
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                schema: "shared",
                columns: table => new
                {
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    RetiredByClaim = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens", x => x.Token);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_LastActivityAt",
                schema: "shared",
                table: "groups",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_tokens_GroupId",
                schema: "shared",
                table: "tokens",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "groups",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "member_claims",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "tokens",
                schema: "shared");
        }
    }
}
