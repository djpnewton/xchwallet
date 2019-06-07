﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using xchwallet;

namespace xchwallet.Migrations
{
    [DbContext(typeof(WalletContext))]
    [Migration("20190606002338_TxOutput")]
    partial class TxOutput
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.1-servicing-10028")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("xchwallet.ChainAttachment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("ChainTxId");

                    b.Property<byte[]>("Data");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId")
                        .IsUnique();

                    b.ToTable("ChainAttachments");
                });

            modelBuilder.Entity("xchwallet.ChainTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("Confirmations");

                    b.Property<long>("Date");

                    b.Property<string>("Fee")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<long>("Height");

                    b.Property<string>("TxId");

                    b.HasKey("Id");

                    b.HasIndex("TxId")
                        .IsUnique();

                    b.ToTable("ChainTxs");
                });

            modelBuilder.Entity("xchwallet.TxInput", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Addr");

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<int>("ChainTxId");

                    b.Property<uint>("N");

                    b.Property<string>("TxId");

                    b.Property<int?>("WalletAddrId");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId");

                    b.HasIndex("WalletAddrId");

                    b.ToTable("TxInputs");
                });

            modelBuilder.Entity("xchwallet.TxOutput", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Addr");

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<int>("ChainTxId");

                    b.Property<uint>("N");

                    b.Property<string>("TxId");

                    b.Property<int?>("WalletAddrId");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId");

                    b.HasIndex("WalletAddrId");

                    b.ToTable("TxOutputs");
                });

            modelBuilder.Entity("xchwallet.WalletAddr", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Address");

                    b.Property<string>("Path");

                    b.Property<int>("PathIndex");

                    b.Property<int>("TagId");

                    b.HasKey("Id");

                    b.HasIndex("Address")
                        .IsUnique();

                    b.HasIndex("TagId");

                    b.ToTable("WalletAddrs");
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

            modelBuilder.Entity("xchwallet.WalletPendingSpend", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<long>("Date");

                    b.Property<int>("Error");

                    b.Property<string>("ErrorMessage");

                    b.Property<string>("SpendCode");

                    b.Property<int>("State");

                    b.Property<int>("TagChangeId");

                    b.Property<int>("TagId");

                    b.Property<int?>("TagOnBehalfOfId");

                    b.Property<string>("To");

                    b.Property<string>("TxIds");

                    b.HasKey("Id");

                    b.HasIndex("SpendCode")
                        .IsUnique();

                    b.HasIndex("TagChangeId");

                    b.HasIndex("TagId");

                    b.HasIndex("TagOnBehalfOfId");

                    b.ToTable("WalletPendingSpends");
                });

            modelBuilder.Entity("xchwallet.WalletTag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Tag");

                    b.HasKey("Id");

                    b.HasIndex("Tag")
                        .IsUnique();

                    b.ToTable("WalletTags");
                });

            modelBuilder.Entity("xchwallet.WalletTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("ChainTxId");

                    b.Property<int>("Direction");

                    b.Property<int>("State");

                    b.Property<int?>("TagOnBehalfOfId");

                    b.Property<int>("WalletAddrId");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId");

                    b.HasIndex("TagOnBehalfOfId");

                    b.HasIndex("WalletAddrId");

                    b.ToTable("WalletTxs");
                });

            modelBuilder.Entity("xchwallet.ChainAttachment", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "Tx")
                        .WithOne("Attachment")
                        .HasForeignKey("xchwallet.ChainAttachment", "ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("xchwallet.TxInput", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "ChainTx")
                        .WithMany("TxInputs")
                        .HasForeignKey("ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletAddr", "WalletAddr")
                        .WithMany("TxInputs")
                        .HasForeignKey("WalletAddrId");
                });

            modelBuilder.Entity("xchwallet.TxOutput", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "ChainTx")
                        .WithMany("TxOutputs")
                        .HasForeignKey("ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletAddr", "WalletAddr")
                        .WithMany("TxOutputs")
                        .HasForeignKey("WalletAddrId");
                });

            modelBuilder.Entity("xchwallet.WalletAddr", b =>
                {
                    b.HasOne("xchwallet.WalletTag", "Tag")
                        .WithMany("Addrs")
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("xchwallet.WalletPendingSpend", b =>
                {
                    b.HasOne("xchwallet.WalletTag", "TagChange")
                        .WithMany()
                        .HasForeignKey("TagChangeId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletTag", "Tag")
                        .WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletTag", "TagOnBehalfOf")
                        .WithMany()
                        .HasForeignKey("TagOnBehalfOfId");
                });

            modelBuilder.Entity("xchwallet.WalletTx", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "ChainTx")
                        .WithMany()
                        .HasForeignKey("ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletTag", "TagOnBehalfOf")
                        .WithMany()
                        .HasForeignKey("TagOnBehalfOfId");

                    b.HasOne("xchwallet.WalletAddr", "Address")
                        .WithMany("Txs")
                        .HasForeignKey("WalletAddrId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
