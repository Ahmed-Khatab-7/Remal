using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    TriggerProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggerVolume = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BuyQuantity = table.Column<int>(type: "int", nullable: false),
                    MinSpend = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RewardProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RewardVolume = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RewardQuantity = table.Column<int>(type: "int", nullable: false),
                    RewardPercentOff = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_IsActive",
                schema: "dbo",
                table: "Promotions",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "dbo");
        }
    }
}
