using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DateBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MiniApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "BotMessageId",
                table: "Profiles",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<long>(
                name: "AppId",
                table: "Profiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Facts",
                table: "Profiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsMutual",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Actions",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_AppId",
                table: "Profiles",
                column: "AppId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Username",
                table: "Profiles",
                column: "Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Profiles_AppId",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_Username",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "AppId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Facts",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "IsMutual",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Actions");

            migrationBuilder.AlterColumn<long>(
                name: "BotMessageId",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
