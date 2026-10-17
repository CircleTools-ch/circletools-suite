using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Shared
{
    /// <inheritdoc />
    public partial class AddParticipantKeysAndEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParticipantKeyHash",
                schema: "shared",
                table: "member_claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "member_entries",
                schema: "shared",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Wishlists = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_entries", x => new { x.GroupId, x.MemberId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_claims_ParticipantKeyHash",
                schema: "shared",
                table: "member_claims",
                column: "ParticipantKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_entries",
                schema: "shared");

            migrationBuilder.DropIndex(
                name: "IX_member_claims_ParticipantKeyHash",
                schema: "shared",
                table: "member_claims");

            migrationBuilder.DropColumn(
                name: "ParticipantKeyHash",
                schema: "shared",
                table: "member_claims");
        }
    }
}
