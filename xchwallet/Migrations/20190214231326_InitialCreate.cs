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
                    Height = table.Column<long>(nullable: false),
                    Confirmations = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainTxs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletCfgs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
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
                        .Annotation("Sqlite:Autoincrement", true),
                    Tag = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletTxMetas",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Note = table.Column<string>(nullable: true),
                    TagOnBehalfOf = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTxMetas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletAddrs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagId = table.Column<int>(nullable: false),
                    Path = table.Column<string>(nullable: true),
                    PathIndex = table.Column<int>(nullable: false),
                    Address = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletAddrs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletAddrs_WalletTags_TagId",
                        column: x => x.TagId,
                        principalTable: "WalletTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    WalletTxMetaId = table.Column<int>(nullable: false)
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
                    table.ForeignKey(
                        name: "FK_WalletTxs_WalletTxMetas_WalletTxMetaId",
                        column: x => x.WalletTxMetaId,
                        principalTable: "WalletTxMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletPendingSpends",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagId = table.Column<int>(nullable: false),
                    TagChangeId = table.Column<int>(nullable: false),
                    WalletTxId = table.Column<int>(nullable: true),
                    Date = table.Column<long>(nullable: false),
                    SpendCode = table.Column<string>(nullable: true),
                    State = table.Column<int>(nullable: false),
                    Error = table.Column<int>(nullable: false),
                    ErrorMessage = table.Column<string>(nullable: true),
                    To = table.Column<string>(nullable: true),
                    Amount = table.Column<string>(nullable: false),
                    WalletTxMetaId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletPendingSpends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletPendingSpends_WalletTags_TagChangeId",
                        column: x => x.TagChangeId,
                        principalTable: "WalletTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletPendingSpends_WalletTags_TagId",
                        column: x => x.TagId,
                        principalTable: "WalletTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletPendingSpends_WalletTxs_WalletTxId",
                        column: x => x.WalletTxId,
                        principalTable: "WalletTxs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletPendingSpends_WalletTxMetas_WalletTxMetaId",
                        column: x => x.WalletTxMetaId,
                        principalTable: "WalletTxMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChainTxs_TxId",
                table: "ChainTxs",
                column: "TxId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletAddrs_TagId",
                table: "WalletAddrs",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCfgs_Key",
                table: "WalletCfgs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_SpendCode",
                table: "WalletPendingSpends",
                column: "SpendCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_TagChangeId",
                table: "WalletPendingSpends",
                column: "TagChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_TagId",
                table: "WalletPendingSpends",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_WalletTxId",
                table: "WalletPendingSpends",
                column: "WalletTxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPendingSpends_WalletTxMetaId",
                table: "WalletPendingSpends",
                column: "WalletTxMetaId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTags_Tag",
                table: "WalletTags",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_ChainTxId",
                table: "WalletTxs",
                column: "ChainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_WalletAddrId",
                table: "WalletTxs",
                column: "WalletAddrId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxs_WalletTxMetaId",
                table: "WalletTxs",
                column: "WalletTxMetaId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletCfgs");

            migrationBuilder.DropTable(
                name: "WalletPendingSpends");

            migrationBuilder.DropTable(
                name: "WalletTxs");

            migrationBuilder.DropTable(
                name: "ChainTxs");

            migrationBuilder.DropTable(
                name: "WalletAddrs");

            migrationBuilder.DropTable(
                name: "WalletTxMetas");

            migrationBuilder.DropTable(
                name: "WalletTags");
        }
    }
}
