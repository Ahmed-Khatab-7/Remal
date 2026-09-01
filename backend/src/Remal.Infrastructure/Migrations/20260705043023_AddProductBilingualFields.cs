using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBilingualFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesBaseEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesHeartEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesTopEn",
                schema: "dbo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NotesBaseEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NotesHeartEn",
                schema: "dbo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NotesTopEn",
                schema: "dbo",
                table: "Products");
        }
    }
}
