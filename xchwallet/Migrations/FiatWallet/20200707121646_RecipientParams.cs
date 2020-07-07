using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations.FiatWallet
{
    public partial class RecipientParams : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipientParams",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FiatWalletTxId = table.Column<int>(nullable: false),
                    Reference = table.Column<string>(nullable: true),
                    Code = table.Column<string>(nullable: true),
                    Particulars = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipientParams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipientParams_FiatWalletTxId",
                table: "RecipientParams",
                column: "FiatWalletTxId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipientParams");
        }
    }
}
