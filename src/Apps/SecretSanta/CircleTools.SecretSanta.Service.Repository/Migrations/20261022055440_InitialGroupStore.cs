using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.SecretSanta.Service.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialGroupStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "secretsanta");

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "secretsanta",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    JoinEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatorKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.GroupId);
                });

            migrationBuilder.CreateTable(
                name: "join_links",
                schema: "secretsanta",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenCipher = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_join_links", x => x.TokenHash);
                });

            migrationBuilder.CreateTable(
                name: "member_entries",
                schema: "secretsanta",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Wishlists = table.Column<string>(type: "jsonb", nullable: false),
                    KeptBy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_entries", x => new { x.GroupId, x.MemberId });
                });

            migrationBuilder.CreateTable(
                name: "reveal_tokens",
                schema: "secretsanta",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenCipher = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    GiverMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reveal_tokens", x => x.TokenHash);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                schema: "secretsanta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PrincipalKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SecretCipher = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Route = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_LastActivityAt",
                schema: "secretsanta",
                table: "groups",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_join_links_GroupId",
                schema: "secretsanta",
                table: "join_links",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_reveal_tokens_GroupId_EventId_GiverMemberId",
                schema: "secretsanta",
                table: "reveal_tokens",
                columns: new[] { "GroupId", "EventId", "GiverMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_AccountId",
                schema: "secretsanta",
                table: "role_assignments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_GroupId",
                schema: "secretsanta",
                table: "role_assignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_GroupId_AccountId",
                schema: "secretsanta",
                table: "role_assignments",
                columns: new[] { "GroupId", "AccountId" },
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_GroupId_MemberId",
                schema: "secretsanta",
                table: "role_assignments",
                columns: new[] { "GroupId", "MemberId" },
                unique: true,
                filter: "\"MemberId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_SecretHash",
                schema: "secretsanta",
                table: "role_assignments",
                column: "SecretHash",
                unique: true,
                filter: "\"SecretHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "groups",
                schema: "secretsanta");

            migrationBuilder.DropTable(
                name: "join_links",
                schema: "secretsanta");

            migrationBuilder.DropTable(
                name: "member_entries",
                schema: "secretsanta");

            migrationBuilder.DropTable(
                name: "reveal_tokens",
                schema: "secretsanta");

            migrationBuilder.DropTable(
                name: "role_assignments",
                schema: "secretsanta");
        }
    }
}
