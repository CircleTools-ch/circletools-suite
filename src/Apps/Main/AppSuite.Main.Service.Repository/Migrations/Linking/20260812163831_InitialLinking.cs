using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppSuite.Main.Service.Repository.Migrations.Linking
{
    /// <inheritdoc />
    public partial class InitialLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "linking");

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "linking",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GiverAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientWishlistSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    BudgetCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    EventDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventLocation = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EventDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DrawVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => new { x.GroupId, x.GiverAccountId });
                });

            migrationBuilder.CreateTable(
                name: "member_links",
                schema: "linking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "member_wishlists",
                schema: "linking",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Items = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_wishlists", x => new { x.GroupId, x.MemberAccountId });
                });

            migrationBuilder.CreateTable(
                name: "resolutions",
                schema: "linking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_GiverAccountId",
                schema: "linking",
                table: "assignments",
                column: "GiverAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_GroupId_RecipientMemberId",
                schema: "linking",
                table: "assignments",
                columns: new[] { "GroupId", "RecipientMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_member_links_GroupId_TargetAccountId_State",
                schema: "linking",
                table: "member_links",
                columns: new[] { "GroupId", "TargetAccountId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_member_links_OrganizerAccountId_GroupId",
                schema: "linking",
                table: "member_links",
                columns: new[] { "OrganizerAccountId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_member_links_TargetAccountId_State",
                schema: "linking",
                table: "member_links",
                columns: new[] { "TargetAccountId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_member_wishlists_MemberAccountId",
                schema: "linking",
                table: "member_wishlists",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_resolutions_OrganizerAccountId_CreatedAt",
                schema: "linking",
                table: "resolutions",
                columns: new[] { "OrganizerAccountId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments",
                schema: "linking");

            migrationBuilder.DropTable(
                name: "member_links",
                schema: "linking");

            migrationBuilder.DropTable(
                name: "member_wishlists",
                schema: "linking");

            migrationBuilder.DropTable(
                name: "resolutions",
                schema: "linking");
        }
    }
}
