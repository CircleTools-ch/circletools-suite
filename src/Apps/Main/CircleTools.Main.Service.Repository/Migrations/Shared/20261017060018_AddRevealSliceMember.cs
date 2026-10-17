using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Shared
{
    /// <inheritdoc />
    public partial class AddRevealSliceMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                schema: "shared",
                table: "reveal_slices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberId",
                schema: "shared",
                table: "reveal_slices",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventId",
                schema: "shared",
                table: "reveal_slices");

            migrationBuilder.DropColumn(
                name: "MemberId",
                schema: "shared",
                table: "reveal_slices");
        }
    }
}
