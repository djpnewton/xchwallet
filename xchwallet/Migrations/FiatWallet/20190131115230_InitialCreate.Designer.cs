﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using xchwallet;

namespace xchwallet.Migrations.FiatWallet
{
    [DbContext(typeof(FiatWalletContext))]
    [Migration("20190131115230_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.1-servicing-10028");

            modelBuilder.Entity("xchwallet.BankTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("Amount");

                    b.Property<string>("BankMetadata");

                    b.Property<long>("Date");

                    b.HasKey("Id");

                    b.ToTable("BankTxs");
                });

            modelBuilder.Entity("xchwallet.FiatWalletTag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Tag");

                    b.HasKey("Id");

                    b.ToTable("WalletTags");
                });

            modelBuilder.Entity("xchwallet.FiatWalletTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("Amount");

                    b.Property<int?>("BankTxId");

                    b.Property<long>("Date");

                    b.Property<string>("DepositCode");

                    b.Property<int>("Direction");

                    b.Property<int>("FiatWalletTagId");

                    b.HasKey("Id");

                    b.HasIndex("BankTxId");

                    b.HasIndex("DepositCode")
                        .IsUnique();

                    b.HasIndex("FiatWalletTagId");

                    b.ToTable("WalletTxs");
                });

            modelBuilder.Entity("xchwallet.WalletCfg", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Key");

                    b.Property<string>("Value");

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
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
