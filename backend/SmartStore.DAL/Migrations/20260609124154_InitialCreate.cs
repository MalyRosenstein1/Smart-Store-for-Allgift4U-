using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStore.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AI_Logs",
                columns: table => new
                {
                    LogID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ImageName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AI_Analysis = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AI_Logs", x => x.LogID);
                });

            migrationBuilder.CreateTable(
                name: "OrderTemplates",
                columns: table => new
                {
                    TemplateID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTemplates", x => x.TemplateID);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MinQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 10)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductID);
                });

            migrationBuilder.CreateTable(
                name: "OrderTemplateComponents",
                columns: table => new
                {
                    TemplateID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    QuantityRequired = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTemplateComponents", x => new { x.TemplateID, x.ProductID });
                    table.ForeignKey(
                        name: "FK_OrderTemplateComponents_OrderTemplates_TemplateID",
                        column: x => x.TemplateID,
                        principalTable: "OrderTemplates",
                        principalColumn: "TemplateID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderTemplateComponents_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRecommendations",
                columns: table => new
                {
                    RecommendationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRecommendations", x => x.RecommendationID);
                    table.ForeignKey(
                        name: "FK_PurchaseRecommendations_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTemplateComponents_ProductID",
                table: "OrderTemplateComponents",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRecommendations_ProductID",
                table: "PurchaseRecommendations",
                column: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AI_Logs");

            migrationBuilder.DropTable(
                name: "OrderTemplateComponents");

            migrationBuilder.DropTable(
                name: "PurchaseRecommendations");

            migrationBuilder.DropTable(
                name: "OrderTemplates");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
