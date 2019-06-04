using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class BalanceUpdates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ChainTxs");

            migrationBuilder.DropColumn(
                name: "From",
                table: "ChainTxs");

            migrationBuilder.DropColumn(
                name: "To",
                table: "ChainTxs");

            migrationBuilder.CreateTable(
                name: "BalanceUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ChainTxId = table.Column<int>(nullable: false),
                    WalletAddrId = table.Column<int>(nullable: false),
                    TxId = table.Column<string>(nullable: true),
                    From = table.Column<string>(nullable: true),
                    To = table.Column<string>(nullable: true),
                    Input = table.Column<bool>(nullable: false),
                    N = table.Column<uint>(nullable: false),
                    Amount = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BalanceUpdates_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BalanceUpdates_WalletAddrs_WalletAddrId",
                        column: x => x.WalletAddrId,
                        principalTable: "WalletAddrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceUpdates_ChainTxId",
                table: "BalanceUpdates",
                column: "ChainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceUpdates_WalletAddrId",
                table: "BalanceUpdates",
                column: "WalletAddrId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceUpdates");

            migrationBuilder.AddColumn<string>(
                name: "Amount",
                table: "ChainTxs",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "From",
                table: "ChainTxs",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "To",
                table: "ChainTxs",
                nullable: true);
        }
    }
}
