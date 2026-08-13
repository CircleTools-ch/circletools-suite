using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDispatchState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_EnqueuedAt",
                table: "outbox_messages");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "outbox_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt_EnqueuedAt",
                table: "outbox_messages",
                columns: new[] { "ProcessedAt", "EnqueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_ProcessedAt_EnqueuedAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_EnqueuedAt",
                table: "outbox_messages",
                column: "EnqueuedAt");
        }
    }
}
