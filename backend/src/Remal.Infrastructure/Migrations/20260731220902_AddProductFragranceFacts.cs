using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFragranceFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CharacterAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CharacterEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcentrationAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcentrationEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LongevityAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LongevityEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OccasionsAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OccasionsEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeasonsAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeasonsEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SillageAr",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SillageEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CharacterAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CharacterEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ConcentrationAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ConcentrationEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FamilyAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FamilyEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LongevityAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LongevityEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OccasionsAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OccasionsEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SeasonsAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SeasonsEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SillageAr",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SillageEn",
                schema: "dbo",
                table: "Products");
        }
    }
}
