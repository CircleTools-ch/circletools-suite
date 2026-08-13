using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Sync
{
    /// <inheritdoc />
    public partial class InitialSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sync");

            migrationBuilder.CreateTable(
                name: "account_counters",
                schema: "sync",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextSeq = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_counters", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "aggregates",
                schema: "sync",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ServerSeq = table.Column<long>(type: "bigint", nullable: false),
                    ServerModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aggregates", x => new { x.AccountId, x.AggregateId });
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "sync",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastCursor = table.Column<long>(type: "bigint", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => new { x.AccountId, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "markers",
                schema: "sync",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ServerSeq = table.Column<long>(type: "bigint", nullable: false),
                    MarkerType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markers", x => new { x.AccountId, x.AggregateId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregates_AccountId_ServerSeq",
                schema: "sync",
                table: "aggregates",
                columns: new[] { "AccountId", "ServerSeq" });

            migrationBuilder.CreateIndex(
                name: "IX_markers_AccountId_ServerSeq",
                schema: "sync",
                table: "markers",
                columns: new[] { "AccountId", "ServerSeq" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_counters",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "aggregates",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "markers",
                schema: "sync");
        }
    }
}
