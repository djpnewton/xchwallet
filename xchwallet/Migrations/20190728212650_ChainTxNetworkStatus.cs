using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class ChainTxNetworkStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChainTxNetworkStatus",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ChainTxId = table.Column<int>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    DateLastBroadcast = table.Column<long>(nullable: false),
                    TxBin = table.Column<byte[]>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainTxNetworkStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChainTxNetworkStatus_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChainTxNetworkStatus_ChainTxId",
                table: "ChainTxNetworkStatus",
                column: "ChainTxId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChainTxNetworkStatus");
        }
    }
}
