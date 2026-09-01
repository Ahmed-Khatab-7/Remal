using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendTickerLinesTo6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TickerLine4Ar",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine4En",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine5Ar",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine5En",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine6Ar",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine6En",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine4Ar",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine4En",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine5Ar",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine5En",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine6Ar",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine6En",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine4Ar",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine4En",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine5Ar",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine5En",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine6Ar",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerLine6En",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TickerLine4Ar",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine4En",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine5Ar",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine5En",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine6Ar",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine6En",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerLine4Ar",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine4En",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine5Ar",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine5En",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine6Ar",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine6En",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerLine4Ar",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine4En",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine5Ar",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine5En",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine6Ar",
                schema: "dbo",
                table: "Bundles");

            migrationBuilder.DropColumn(
                name: "TickerLine6En",
                schema: "dbo",
                table: "Bundles");
        }
    }
}
