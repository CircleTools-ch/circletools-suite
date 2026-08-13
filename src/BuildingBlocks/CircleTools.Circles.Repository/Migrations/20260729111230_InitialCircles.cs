using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Circles.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCircles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "circles");

            migrationBuilder.CreateTable(
                name: "circles",
                schema: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invitation_attempts",
                schema: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_attempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "circle_memberships",
                schema: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circle_memberships_circles_CircleId",
                        column: x => x.CircleId,
                        principalSchema: "circles",
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitation_declines",
                schema: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeclinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_declines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invitation_declines_circles_CircleId",
                        column: x => x.CircleId,
                        principalSchema: "circles",
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagedMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecretHandle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invitations_circles_CircleId",
                        column: x => x.CircleId,
                        principalSchema: "circles",
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_circle_memberships_CircleId_UserId",
                schema: "circles",
                table: "circle_memberships",
                columns: new[] { "CircleId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_circle_memberships_UserId",
                schema: "circles",
                table: "circle_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_attempts_InviterUserId_AttemptedAt",
                schema: "circles",
                table: "invitation_attempts",
                columns: new[] { "InviterUserId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_attempts_RecipientEmailHash_AttemptedAt",
                schema: "circles",
                table: "invitation_attempts",
                columns: new[] { "RecipientEmailHash", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_declines_CircleId_EmailHash",
                schema: "circles",
                table: "invitation_declines",
                columns: new[] { "CircleId", "EmailHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_CircleId_EmailHash",
                schema: "circles",
                table: "invitations",
                columns: new[] { "CircleId", "EmailHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_EmailHash",
                schema: "circles",
                table: "invitations",
                column: "EmailHash");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_ExpiresAt",
                schema: "circles",
                table: "invitations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_SecretHandle",
                schema: "circles",
                table: "invitations",
                column: "SecretHandle",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "circle_memberships",
                schema: "circles");

            migrationBuilder.DropTable(
                name: "invitation_attempts",
                schema: "circles");

            migrationBuilder.DropTable(
                name: "invitation_declines",
                schema: "circles");

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "circles");

            migrationBuilder.DropTable(
                name: "circles",
                schema: "circles");
        }
    }
}
