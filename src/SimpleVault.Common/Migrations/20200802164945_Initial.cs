using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace SimpleVault.Common.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "simple_vault");

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "simple_vault",
                columns: table => new
                {
                    TransactionSigningRequestId = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionId = table.Column<string>(nullable: true),
                    BlockchainId = table.Column<string>(nullable: true),
                    ProtocolCode = table.Column<string>(nullable: true),
                    NetworkType = table.Column<int>(nullable: false),
                    SignedTransaction = table.Column<byte[]>(nullable: true),
                    SigningAddresses = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.TransactionSigningRequestId);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                schema: "simple_vault",
                columns: table => new
                {
                    WalletGenerationRequestId = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BlockchainId = table.Column<string>(nullable: true),
                    ProtocolCode = table.Column<string>(nullable: true),
                    NetworkType = table.Column<int>(nullable: false),
                    Address = table.Column<string>(nullable: true),
                    PublicKey = table.Column<string>(nullable: true),
                    ScriptPublicKey = table.Column<string>(nullable: true),
                    PrivateKey = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.WalletGenerationRequestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wallets_Address",
                schema: "simple_vault",
                table: "wallets",
                column: "Address");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transactions",
                schema: "simple_vault");

            migrationBuilder.DropTable(
                name: "wallets",
                schema: "simple_vault");
        }
    }
}
