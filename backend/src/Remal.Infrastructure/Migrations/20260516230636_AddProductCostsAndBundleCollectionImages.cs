using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCostsAndBundleCollectionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostAlcohol",
                schema: "dbo",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostOil",
                schema: "dbo",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPackaging",
                schema: "dbo",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl2",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl3",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl2",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl3",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostAlcohol",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CostOil",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CostPackaging",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ImageUrl2",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ImageUrl3",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ImageUrl2",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "ImageUrl3",
                schema: "dbo",
                table: "Bundles");
        }
    }
}
