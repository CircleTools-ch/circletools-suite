using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Linking
{
    /// <inheritdoc />
    public partial class PerEventLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_member_wishlists",
                schema: "linking",
                table: "member_wishlists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_assignments",
                schema: "linking",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "IX_assignments_GroupId_RecipientMemberId",
                schema: "linking",
                table: "assignments");

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                schema: "linking",
                table: "member_wishlists",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "EventName",
                schema: "linking",
                table: "member_wishlists",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                schema: "linking",
                table: "assignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "EventName",
                schema: "linking",
                table: "assignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill (ADR-0042): existing single-exchange rows belong to the migrated group's default
            // event, whose id is the group id — the same deterministic value the client migration uses in
            // Group.FromState, so the two sides line up with no coordination. Done before the new primary
            // key is added, while the column still holds the empty-guid default.
            migrationBuilder.Sql(@"UPDATE linking.assignments SET ""EventId"" = ""GroupId"";");
            migrationBuilder.Sql(@"UPDATE linking.member_wishlists SET ""EventId"" = ""GroupId"";");

            migrationBuilder.AddPrimaryKey(
                name: "PK_member_wishlists",
                schema: "linking",
                table: "member_wishlists",
                columns: new[] { "GroupId", "EventId", "MemberAccountId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_assignments",
                schema: "linking",
                table: "assignments",
                columns: new[] { "GroupId", "EventId", "GiverAccountId" });

            migrationBuilder.CreateTable(
                name: "group_events",
                schema: "linking",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_events", x => new { x.GroupId, x.EventId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_GroupId_EventId_RecipientMemberId",
                schema: "linking",
                table: "assignments",
                columns: new[] { "GroupId", "EventId", "RecipientMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_group_events_OrganizerAccountId",
                schema: "linking",
                table: "group_events",
                column: "OrganizerAccountId");

            // Seed the event registry so members already linked in a group can enumerate its (single,
            // default) event immediately — one row per group, id = group id. The organizer's next sync
            // re-publishes the real event list on top (ADR-0042). DISTINCT ON picks one row per group
            // (there is one organizer per group), avoiding an aggregate over uuid — which Postgres has no
            // MIN for.
            migrationBuilder.Sql(@"
                INSERT INTO linking.group_events (""GroupId"", ""EventId"", ""OrganizerAccountId"", ""GroupName"", ""EventName"")
                SELECT DISTINCT ON (""GroupId"") ""GroupId"", ""GroupId"", ""OrganizerAccountId"", ""GroupName"", ''
                FROM linking.member_links
                ORDER BY ""GroupId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_events",
                schema: "linking");

            migrationBuilder.DropPrimaryKey(
                name: "PK_member_wishlists",
                schema: "linking",
                table: "member_wishlists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_assignments",
                schema: "linking",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "IX_assignments_GroupId_EventId_RecipientMemberId",
                schema: "linking",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "EventId",
                schema: "linking",
                table: "member_wishlists");

            migrationBuilder.DropColumn(
                name: "EventName",
                schema: "linking",
                table: "member_wishlists");

            migrationBuilder.DropColumn(
                name: "EventId",
                schema: "linking",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "EventName",
                schema: "linking",
                table: "assignments");

            migrationBuilder.AddPrimaryKey(
                name: "PK_member_wishlists",
                schema: "linking",
                table: "member_wishlists",
                columns: new[] { "GroupId", "MemberAccountId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_assignments",
                schema: "linking",
                table: "assignments",
                columns: new[] { "GroupId", "GiverAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_GroupId_RecipientMemberId",
                schema: "linking",
                table: "assignments",
                columns: new[] { "GroupId", "RecipientMemberId" });
        }
    }
}
