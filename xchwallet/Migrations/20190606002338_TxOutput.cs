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

            migrationBuilder.DropTable(
                name: "WalletTxMetas");

            migrationBuilder.DropIndex(
                name: "IX_WalletTxs_WalletTxMetaId",
                table: "WalletTxs");

            migrationBuilder.DropIndex(
                name: "IX_WalletPendingSpends_WalletTxMetaId",
                table: "WalletPendingSpends");

            migrationBuilder.DropColumn(
                name: "Acknowledged",
                table: "WalletTxs");

            migrationBuilder.DropColumn(
                name: "WalletTxMetaId",
                table: "WalletTxs");

            migrationBuilder.DropColumn(
                name: "WalletTxMetaId",
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

            migrationBuilder.RenameColumn(
                name: "WalletTxId",
                table: "WalletPendingSpends",
                newName: "TagOnBehalfOfId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletPendingSpends_WalletTxId",
                table: "WalletPendingSpends",
                newName: "IX_WalletPendingSpends_TagOnBehalfOfId");

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "WalletTxs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TagOnBehalfOfId",
                table: "WalletTxs",
                nullable: true);

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
                name: "IX_WalletTxs_TagOnBehalfOfId",
                table: "WalletTxs",
                column: "TagOnBehalfOfId");

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
                name: "FK_WalletPendingSpends_WalletTags_TagOnBehalfOfId",
                table: "WalletPendingSpends",
                column: "TagOnBehalfOfId",
                principalTable: "WalletTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTxs_WalletTags_TagOnBehalfOfId",
                table: "WalletTxs",
                column: "TagOnBehalfOfId",
                principalTable: "WalletTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletPendingSpends_WalletTags_TagOnBehalfOfId",
                table: "WalletPendingSpends");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTxs_WalletTags_TagOnBehalfOfId",
                table: "WalletTxs");

            migrationBuilder.DropTable(
                name: "TxInputs");

            migrationBuilder.DropTable(
                name: "TxOutputs");

            migrationBuilder.DropIndex(
                name: "IX_WalletTxs_TagOnBehalfOfId",
                table: "WalletTxs");

            migrationBuilder.DropColumn(
                name: "State",
                table: "WalletTxs");

            migrationBuilder.DropColumn(
                name: "TagOnBehalfOfId",
                table: "WalletTxs");

            migrationBuilder.DropColumn(
                name: "TxIds",
                table: "WalletPendingSpends");

            migrationBuilder.RenameColumn(
                name: "TagOnBehalfOfId",
                table: "WalletPendingSpends",
                newName: "WalletTxId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletPendingSpends_TagOnBehalfOfId",
                table: "WalletPendingSpends",
                newName: "IX_WalletPendingSpends_WalletTxId");

            migrationBuilder.AddColumn<bool>(
                name: "Acknowledged",
                table: "WalletTxs",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WalletTxMetaId",
                table: "WalletTxs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WalletTxMetaId",
                table: "WalletPendingSpends",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateTable(
                name: "WalletTxMetas",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Note = table.Column<string>(nullable: true),
                    TagOnBehalfOf = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTxMetas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_WalletTxMetaId",
                table: "WalletTxs",
                column: "WalletTxMetaId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_WalletTxMetaId",
                table: "WalletPendingSpends",
                column: "WalletTxMetaId");

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
