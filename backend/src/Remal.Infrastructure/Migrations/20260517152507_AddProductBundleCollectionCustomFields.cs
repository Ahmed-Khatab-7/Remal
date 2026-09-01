using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBundleCollectionCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadgeArabic",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeEnglish",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeKind",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine1Ar",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine1En",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine2Ar",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine2En",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine3Ar",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine3En",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeArabic",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeEnglish",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeKind",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine1Ar",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine1En",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine2Ar",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine2En",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine3Ar",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine3En",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeArabic",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeEnglish",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeKind",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine1Ar",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine1En",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine2Ar",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine2En",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine3Ar",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine3En",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BadgeArabic",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BadgeEnglish",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BadgeKind",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine1Ar",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine1En",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine2Ar",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine2En",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine3Ar",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine3En",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BadgeArabic",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "BadgeEnglish",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "BadgeKind",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine1Ar",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine1En",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine2Ar",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine2En",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine3Ar",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine3En",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "BadgeArabic",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "BadgeEnglish",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "BadgeKind",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine1Ar",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine1En",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine2Ar",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine2En",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine3Ar",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine3En",
                schema: "dbo",
                table: "Bundles");
        }
    }
}
