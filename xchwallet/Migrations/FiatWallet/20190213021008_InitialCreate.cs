using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations.FiatWallet
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTxs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankMetadata = table.Column<string>(nullable: true),
                    Date = table.Column<long>(nullable: false),
                    Amount = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTxs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChainTx",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TxId = table.Column<string>(nullable: true),
                    Date = table.Column<long>(nullable: false),
                    From = table.Column<string>(nullable: true),
                    To = table.Column<string>(nullable: true),
                    Amount = table.Column<string>(type: "string", nullable: false),
                    Fee = table.Column<string>(type: "string", nullable: false),
                    Height = table.Column<long>(nullable: false),
                    Confirmations = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainTx", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletCfgs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(nullable: true),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletCfgs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTag",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tag = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTags",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tag = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletAddr",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagId = table.Column<int>(nullable: false),
                    Path = table.Column<string>(nullable: true),
                    PathIndex = table.Column<int>(nullable: false),
                    Address = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletAddr", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletAddr_WalletTag_TagId",
                        column: x => x.TagId,
                        principalTable: "WalletTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTxs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankTxId = table.Column<int>(nullable: true),
                    FiatWalletTagId = table.Column<int>(nullable: false),
                    Direction = table.Column<int>(nullable: false),
                    Date = table.Column<long>(nullable: false),
                    Amount = table.Column<long>(nullable: false),
                    DepositCode = table.Column<string>(nullable: true),
                    BankName = table.Column<string>(nullable: true),
                    BankAddress = table.Column<string>(nullable: true),
                    AccountName = table.Column<string>(nullable: true),
                    AccountNumber = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTxs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTxs_BankTxs_BankTxId",
                        column: x => x.BankTxId,
                        principalTable: "BankTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTxs_WalletTags_FiatWalletTagId",
                        column: x => x.FiatWalletTagId,
                        principalTable: "WalletTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTx",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChainTxId = table.Column<int>(nullable: false),
                    WalletAddrId = table.Column<int>(nullable: false),
                    Direction = table.Column<int>(nullable: false),
                    Acknowledged = table.Column<bool>(nullable: false),
                    Note = table.Column<string>(nullable: true),
                    WalletId = table.Column<long>(nullable: false),
                    TagOnBehalfOf = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTx", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTx_ChainTx_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTx",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletTx_WalletAddr_WalletAddrId",
                        column: x => x.WalletAddrId,
                        principalTable: "WalletAddr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletAddr_TagId",
                table: "WalletAddr",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCfgs_Key",
                table: "WalletCfgs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTag_Tag",
                table: "WalletTag",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTx_ChainTxId",
                table: "WalletTx",
                column: "ChainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTx_WalletAddrId",
                table: "WalletTx",
                column: "WalletAddrId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_BankTxId",
                table: "WalletTxs",
                column: "BankTxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_DepositCode",
                table: "WalletTxs",
                column: "DepositCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_FiatWalletTagId",
                table: "WalletTxs",
                column: "FiatWalletTagId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletCfgs");

            migrationBuilder.DropTable(
                name: "WalletTx");

            migrationBuilder.DropTable(
                name: "WalletTxs");

            migrationBuilder.DropTable(
                name: "ChainTx");

            migrationBuilder.DropTable(
                name: "WalletAddr");

            migrationBuilder.DropTable(
                name: "BankTxs");

            migrationBuilder.DropTable(
                name: "WalletTags");

            migrationBuilder.DropTable(
                name: "WalletTag");
        }
    }
}
