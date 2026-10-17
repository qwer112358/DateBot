using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DateBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtraPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraPhotoFileIds",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraPhotoFileIds",
                table: "Profiles");
        }
    }
}
