using Microsoft.EntityFrameworkCore.Migrations;

namespace SimpleVault.Common.Migrations
{
    public partial class WalletGroup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                schema: "simple_vault",
                table: "wallets",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "simple_vault",
                table: "wallets",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Group",
                schema: "simple_vault",
                table: "wallets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "simple_vault",
                table: "wallets");
        }
    }
}
