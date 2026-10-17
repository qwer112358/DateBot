using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DateBot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fills <c>IsMutual</c> for matches stored before it existed: a mutual notification, or a draft answering a later
    /// notification than the first one (the mutual like that followed our like back).
    /// </summary>
    public partial class MarkMutualMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                UPDATE Profiles SET IsMutual = 1
                WHERE Kind = 'Mutual'
                   OR EXISTS (SELECT 1 FROM Actions a
                              WHERE a.ProfileId = Profiles.Id
                                AND a.AnchorMessageId IS NOT NULL
                                AND a.AnchorMessageId <> Profiles.BotMessageId);
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the column itself is dropped by the previous migration's Down.
        }
    }
}
