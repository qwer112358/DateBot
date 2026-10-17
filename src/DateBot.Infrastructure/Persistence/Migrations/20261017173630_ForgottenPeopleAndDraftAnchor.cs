using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DateBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForgottenPeopleAndDraftAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AnchorMessageId",
                table: "Actions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ForgottenPeople",
                columns: table => new
                {
                    PersonId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ForgottenAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgottenPeople", x => x.PersonId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForgottenPeople");

            migrationBuilder.DropColumn(
                name: "AnchorMessageId",
                table: "Actions");
        }
    }
}
