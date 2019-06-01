using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class ChainOutputs : Migration
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
                name: "ChainInputs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TxId = table.Column<string>(nullable: true),
                    N = table.Column<uint>(nullable: false),
                    From = table.Column<string>(nullable: true),
                    To = table.Column<string>(nullable: true),
                    Amount = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainInputs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChainOutputs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TxId = table.Column<string>(nullable: true),
                    N = table.Column<uint>(nullable: false),
                    From = table.Column<string>(nullable: true),
                    To = table.Column<string>(nullable: true),
                    Amount = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainOutputs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChainTxInputs",
                columns: table => new
                {
                    ChainTxId = table.Column<int>(nullable: false),
                    ChainInputId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainTxInputs", x => new { x.ChainTxId, x.ChainInputId });
                    table.ForeignKey(
                        name: "FK_ChainTxInputs_ChainInputs_ChainInputId",
                        column: x => x.ChainInputId,
                        principalTable: "ChainInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChainTxInputs_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChainTxOutputs",
                columns: table => new
                {
                    ChainTxId = table.Column<int>(nullable: false),
                    ChainOutputId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainTxOutputs", x => new { x.ChainTxId, x.ChainOutputId });
                    table.ForeignKey(
                        name: "FK_ChainTxOutputs_ChainOutputs_ChainOutputId",
                        column: x => x.ChainOutputId,
                        principalTable: "ChainOutputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChainTxOutputs_ChainTxs_ChainTxId",
                        column: x => x.ChainTxId,
                        principalTable: "ChainTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChainTxInputs_ChainInputId",
                table: "ChainTxInputs",
                column: "ChainInputId");

            migrationBuilder.CreateIndex(
                name: "IX_ChainTxOutputs_ChainOutputId",
                table: "ChainTxOutputs",
                column: "ChainOutputId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChainTxInputs");

            migrationBuilder.DropTable(
                name: "ChainTxOutputs");

            migrationBuilder.DropTable(
                name: "ChainInputs");

            migrationBuilder.DropTable(
                name: "ChainOutputs");

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
