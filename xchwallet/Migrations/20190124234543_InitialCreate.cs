using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChainTxs",
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
                    Confirmations = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainTxs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletAddrs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tag = table.Column<string>(nullable: true),
                    Path = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletAddrs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTxs",
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
                    table.PrimaryKey("PK_WalletTxs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTxs_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletTxs_WalletAddrs_WalletAddrId",
                        column: x => x.WalletAddrId,
                        principalTable: "WalletAddrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_ChainTxId",
                table: "WalletTxs",
                column: "ChainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_WalletAddrId",
                table: "WalletTxs",
                column: "WalletAddrId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletTxs");

            migrationBuilder.DropTable(
                name: "ChainTxs");

            migrationBuilder.DropTable(
                name: "WalletAddrs");
        }
    }
}
