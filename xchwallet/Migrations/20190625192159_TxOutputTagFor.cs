using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class TxOutputTagFor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletPendingSpends_WalletTags_TagOnBehalfOfId",
                table: "WalletPendingSpends");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTxs_WalletTags_TagOnBehalfOfId",
                table: "WalletTxs");

            migrationBuilder.DropIndex(
                name: "IX_WalletTxs_TagOnBehalfOfId",
                table: "WalletTxs");

            migrationBuilder.DropColumn(
                name: "TagOnBehalfOfId",
                table: "WalletTxs");

            migrationBuilder.RenameColumn(
                name: "TagOnBehalfOfId",
                table: "WalletPendingSpends",
                newName: "TagForId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletPendingSpends_TagOnBehalfOfId",
                table: "WalletPendingSpends",
                newName: "IX_WalletPendingSpends_TagForId");

            migrationBuilder.AddColumn<int>(
                name: "TagForId",
                table: "TxOutputs",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingSpendsForTag",
                columns: table => new
                {
                    PendingSpendId = table.Column<int>(nullable: false),
                    TagId = table.Column<int>(nullable: false),
                    Id = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingSpendsForTag", x => new { x.PendingSpendId, x.TagId });
                    table.ForeignKey(
                        name: "FK_PendingSpendsForTag_WalletPendingSpends_PendingSpendId",
                        column: x => x.PendingSpendId,
                        principalTable: "WalletPendingSpends",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingSpendsForTag_WalletTags_TagId",
                        column: x => x.TagId,
                        principalTable: "WalletTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TxOutputsForTag",
                columns: table => new
                {
                    TxOutputId = table.Column<int>(nullable: false),
                    TagId = table.Column<int>(nullable: false),
                    Id = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxOutputsForTag", x => new { x.TxOutputId, x.TagId });
                    table.ForeignKey(
                        name: "FK_TxOutputsForTag_WalletTags_TagId",
                        column: x => x.TagId,
                        principalTable: "WalletTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TxOutputsForTag_TxOutputs_TxOutputId",
                        column: x => x.TxOutputId,
                        principalTable: "TxOutputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TxOutputs_TagForId",
                table: "TxOutputs",
                column: "TagForId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSpendsForTag_PendingSpendId",
                table: "PendingSpendsForTag",
                column: "PendingSpendId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingSpendsForTag_TagId",
                table: "PendingSpendsForTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TxOutputsForTag_TagId",
                table: "TxOutputsForTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TxOutputsForTag_TxOutputId",
                table: "TxOutputsForTag",
                column: "TxOutputId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TxOutputs_WalletTags_TagForId",
                table: "TxOutputs",
                column: "TagForId",
                principalTable: "WalletTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletPendingSpends_WalletTags_TagForId",
                table: "WalletPendingSpends",
                column: "TagForId",
                principalTable: "WalletTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TxOutputs_WalletTags_TagForId",
                table: "TxOutputs");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletPendingSpends_WalletTags_TagForId",
                table: "WalletPendingSpends");

            migrationBuilder.DropTable(
                name: "PendingSpendsForTag");

            migrationBuilder.DropTable(
                name: "TxOutputsForTag");

            migrationBuilder.DropIndex(
                name: "IX_TxOutputs_TagForId",
                table: "TxOutputs");

            migrationBuilder.DropColumn(
                name: "TagForId",
                table: "TxOutputs");

            migrationBuilder.RenameColumn(
                name: "TagForId",
                table: "WalletPendingSpends",
                newName: "TagOnBehalfOfId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletPendingSpends_TagForId",
                table: "WalletPendingSpends",
                newName: "IX_WalletPendingSpends_TagOnBehalfOfId");

            migrationBuilder.AddColumn<int>(
                name: "TagOnBehalfOfId",
                table: "WalletTxs",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_TagOnBehalfOfId",
                table: "WalletTxs",
                column: "TagOnBehalfOfId");

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
    }
}
