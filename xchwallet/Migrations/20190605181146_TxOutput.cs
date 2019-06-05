using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class TxOutput : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletPendingSpends_WalletTxs_WalletTxId",
                table: "WalletPendingSpends");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletPendingSpends_WalletTxMetas_WalletTxMetaId",
                table: "WalletPendingSpends");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTxs_WalletTxMetas_WalletTxMetaId",
                table: "WalletTxs");

            migrationBuilder.DropIndex(
                name: "IX_WalletPendingSpends_WalletTxId",
                table: "WalletPendingSpends");

            migrationBuilder.DropColumn(
                name: "WalletTxId",
                table: "WalletPendingSpends");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ChainTxs");

            migrationBuilder.DropColumn(
                name: "From",
                table: "ChainTxs");

            migrationBuilder.DropColumn(
                name: "To",
                table: "ChainTxs");

            migrationBuilder.AlterColumn<int>(
                name: "WalletTxMetaId",
                table: "WalletTxs",
                nullable: true,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "WalletTxMetaId",
                table: "WalletPendingSpends",
                nullable: true,
                oldClrType: typeof(int));

            migrationBuilder.AddColumn<string>(
                name: "TxIds",
                table: "WalletPendingSpends",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TxInputs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ChainTxId = table.Column<int>(nullable: false),
                    WalletAddrId = table.Column<int>(nullable: true),
                    TxId = table.Column<string>(nullable: true),
                    Addr = table.Column<string>(nullable: true),
                    N = table.Column<uint>(nullable: false),
                    Amount = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TxInputs_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TxInputs_WalletAddrs_WalletAddrId",
                        column: x => x.WalletAddrId,
                        principalTable: "WalletAddrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TxOutputs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ChainTxId = table.Column<int>(nullable: false),
                    WalletAddrId = table.Column<int>(nullable: true),
                    TxId = table.Column<string>(nullable: true),
                    Addr = table.Column<string>(nullable: true),
                    N = table.Column<uint>(nullable: false),
                    Amount = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TxOutputs_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TxOutputs_WalletAddrs_WalletAddrId",
                        column: x => x.WalletAddrId,
                        principalTable: "WalletAddrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TxInputs_ChainTxId",
                table: "TxInputs",
                column: "ChainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_TxInputs_WalletAddrId",
                table: "TxInputs",
                column: "WalletAddrId");

            migrationBuilder.CreateIndex(
                name: "IX_TxOutputs_ChainTxId",
                table: "TxOutputs",
                column: "ChainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_TxOutputs_WalletAddrId",
                table: "TxOutputs",
                column: "WalletAddrId");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletPendingSpends_WalletTxMetas_WalletTxMetaId",
                table: "WalletPendingSpends",
                column: "WalletTxMetaId",
                principalTable: "WalletTxMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTxs_WalletTxMetas_WalletTxMetaId",
                table: "WalletTxs",
                column: "WalletTxMetaId",
                principalTable: "WalletTxMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletPendingSpends_WalletTxMetas_WalletTxMetaId",
                table: "WalletPendingSpends");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTxs_WalletTxMetas_WalletTxMetaId",
                table: "WalletTxs");

            migrationBuilder.DropTable(
                name: "TxInputs");

            migrationBuilder.DropTable(
                name: "TxOutputs");

            migrationBuilder.DropColumn(
                name: "TxIds",
                table: "WalletPendingSpends");

            migrationBuilder.AlterColumn<int>(
                name: "WalletTxMetaId",
                table: "WalletTxs",
                nullable: false,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "WalletTxMetaId",
                table: "WalletPendingSpends",
                nullable: false,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletTxId",
                table: "WalletPendingSpends",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_WalletTxId",
                table: "WalletPendingSpends",
                column: "WalletTxId");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletPendingSpends_WalletTxs_WalletTxId",
                table: "WalletPendingSpends",
                column: "WalletTxId",
                principalTable: "WalletTxs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletPendingSpends_WalletTxMetas_WalletTxMetaId",
                table: "WalletPendingSpends",
                column: "WalletTxMetaId",
                principalTable: "WalletTxMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTxs_WalletTxMetas_WalletTxMetaId",
                table: "WalletTxs",
                column: "WalletTxMetaId",
                principalTable: "WalletTxMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
