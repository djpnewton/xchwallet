using Microsoft.EntityFrameworkCore.Migrations;

namespace xchwallet.Migrations
{
    public partial class TxOutputUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                table: "TxOutputsForTag");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PendingSpendsForTag");

            migrationBuilder.AlterColumn<string>(
                name: "TxId",
                table: "TxOutputs",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TxId",
                table: "TxInputs",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TxOutputs_TxId_N",
                table: "TxOutputs",
                columns: new[] { "TxId", "N" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TxInputs_TxId_N",
                table: "TxInputs",
                columns: new[] { "TxId", "N" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_TxOutputs_TxId_N",
                table: "TxOutputs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TxInputs_TxId_N",
                table: "TxInputs");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "TxOutputsForTag",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "TxId",
                table: "TxOutputs",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<string>(
                name: "TxId",
                table: "TxInputs",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "PendingSpendsForTag",
                nullable: false,
                defaultValue: 0);
        }
    }
}
