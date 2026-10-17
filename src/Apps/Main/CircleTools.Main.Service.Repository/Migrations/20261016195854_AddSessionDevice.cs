using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Device",
                table: "refresh_token_chains",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Device",
                table: "refresh_token_chains");
        }
    }
}
