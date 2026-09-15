using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Shared
{
    /// <inheritdoc />
    public partial class ProtectShareTokensAtRestAndAccountIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenCipher",
                schema: "shared",
                table: "tokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_member_claims_AccountId",
                schema: "shared",
                table: "member_claims",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_OwnerAccountId",
                schema: "shared",
                table: "groups",
                column: "OwnerAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_member_claims_AccountId",
                schema: "shared",
                table: "member_claims");

            migrationBuilder.DropIndex(
                name: "IX_groups_OwnerAccountId",
                schema: "shared",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "TokenCipher",
                schema: "shared",
                table: "tokens");
        }
    }
}
