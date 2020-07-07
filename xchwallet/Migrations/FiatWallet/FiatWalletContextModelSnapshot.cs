﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using xchwallet;

namespace xchwallet.Migrations.FiatWallet
{
    [DbContext(typeof(FiatWalletContext))]
    partial class FiatWalletContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("xchwallet.BankTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<long>("Amount")
                        .HasColumnType("bigint");

                    b.Property<string>("BankMetadata")
                        .HasColumnType("longtext");

                    b.Property<long>("Date")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("BankTxs");
                });

            modelBuilder.Entity("xchwallet.FiatWalletTag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Tag")
                        .HasColumnType("varchar(255)");

                    b.HasKey("Id");

                    b.HasIndex("Tag")
                        .IsUnique();

                    b.ToTable("WalletTags");
                });

            modelBuilder.Entity("xchwallet.FiatWalletTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("AccountName")
                        .HasColumnType("longtext");

                    b.Property<string>("AccountNumber")
                        .HasColumnType("longtext");

                    b.Property<long>("Amount")
                        .HasColumnType("bigint");

                    b.Property<string>("BankAddress")
                        .HasColumnType("longtext");

                    b.Property<string>("BankName")
                        .HasColumnType("longtext");

                    b.Property<int?>("BankTxId")
                        .HasColumnType("int");

                    b.Property<long>("Date")
                        .HasColumnType("bigint");

                    b.Property<string>("DepositCode")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("Direction")
                        .HasColumnType("int");

                    b.Property<int>("FiatWalletTagId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("BankTxId");

                    b.HasIndex("DepositCode")
                        .IsUnique();

                    b.HasIndex("FiatWalletTagId");

                    b.ToTable("WalletTxs");
                });

            modelBuilder.Entity("xchwallet.RecipientParams", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Code")
                        .HasColumnType("longtext");

                    b.Property<int>("FiatWalletTxId")
                        .HasColumnType("int");

                    b.Property<string>("Particulars")
                        .HasColumnType("longtext");

                    b.Property<string>("Reference")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("FiatWalletTxId")
                        .IsUnique();

                    b.ToTable("RecipientParams");
                });

            modelBuilder.Entity("xchwallet.WalletCfg", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Key")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Value")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("WalletCfgs");
                });

            modelBuilder.Entity("xchwallet.FiatWalletTx", b =>
                {
                    b.HasOne("xchwallet.BankTx", "BankTx")
                        .WithMany()
                        .HasForeignKey("BankTxId");

                    b.HasOne("xchwallet.FiatWalletTag", "Tag")
                        .WithMany("Txs")
                        .HasForeignKey("FiatWalletTagId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
