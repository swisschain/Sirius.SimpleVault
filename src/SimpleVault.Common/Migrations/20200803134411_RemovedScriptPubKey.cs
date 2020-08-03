using Microsoft.EntityFrameworkCore.Migrations;

namespace SimpleVault.Common.Migrations
{
    public partial class RemovedScriptPubKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScriptPublicKey",
                schema: "simple_vault",
                table: "wallets");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScriptPublicKey",
                schema: "simple_vault",
                table: "wallets",
                type: "text",
                nullable: true);
        }
    }
}
