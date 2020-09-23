﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SimpleVault.Common.Persistence;

namespace SimpleVault.Common.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    [Migration("20200923113307_WalletGroup")]
    partial class WalletGroup
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("simple_vault")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "3.1.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("SimpleVault.Common.Persistence.Transactions.TransactionEntity", b =>
                {
                    b.Property<long>("TransactionSigningRequestId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("BlockchainId")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("NetworkType")
                        .HasColumnType("integer");

                    b.Property<string>("ProtocolCode")
                        .HasColumnType("text");

                    b.Property<byte[]>("SignedTransaction")
                        .HasColumnType("bytea");

                    b.Property<string>("SigningAddresses")
                        .HasColumnType("text");

                    b.Property<string>("TransactionId")
                        .HasColumnType("text");

                    b.HasKey("TransactionSigningRequestId");

                    b.ToTable("transactions");
                });

            modelBuilder.Entity("SimpleVault.Common.Persistence.Wallets.WalletEntity", b =>
                {
                    b.Property<long>("WalletGenerationRequestId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("Address")
                        .HasColumnType("text");

                    b.Property<string>("BlockchainId")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Group")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("NetworkType")
                        .HasColumnType("integer");

                    b.Property<string>("PrivateKey")
                        .HasColumnType("text");

                    b.Property<string>("ProtocolCode")
                        .HasColumnType("text");

                    b.Property<string>("PublicKey")
                        .HasColumnType("text");

                    b.Property<string>("TenantId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("WalletGenerationRequestId");

                    b.HasIndex("Address");

                    b.ToTable("wallets");
                });
#pragma warning restore 612, 618
        }
    }
}
