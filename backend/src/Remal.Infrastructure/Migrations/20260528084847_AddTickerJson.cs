using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTickerJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TickerJson",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerJson",
                schema: "dbo",
                table: "Collections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TickerJson",
                schema: "dbo",
                table: "Bundles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TickerJson",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TickerJson",
                schema: "dbo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TickerJson",
                schema: "dbo",
                table: "Bundles");
        }
    }
}
