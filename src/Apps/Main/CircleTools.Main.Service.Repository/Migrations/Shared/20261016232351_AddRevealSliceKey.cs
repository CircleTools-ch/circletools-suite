using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Shared
{
    /// <inheritdoc />
    public partial class AddRevealSliceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                schema: "shared",
                table: "reveal_slices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Key",
                schema: "shared",
                table: "reveal_slices");
        }
    }
}
