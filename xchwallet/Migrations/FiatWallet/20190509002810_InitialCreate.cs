using Microsoft.EntityFrameworkCore.Metadata;
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
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BankMetadata = table.Column<string>(nullable: true),
                    Date = table.Column<long>(nullable: false),
                    Amount = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTxs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletCfgs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Key = table.Column<string>(nullable: true),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletCfgs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTags",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Tag = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTxs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
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

            migrationBuilder.CreateIndex(
                name: "IX_WalletCfgs_Key",
                table: "WalletCfgs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTags_Tag",
                table: "WalletTags",
                column: "Tag",
                unique: true);

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
                name: "WalletTxs");

            migrationBuilder.DropTable(
                name: "BankTxs");

            migrationBuilder.DropTable(
                name: "WalletTags");
        }
    }
}
