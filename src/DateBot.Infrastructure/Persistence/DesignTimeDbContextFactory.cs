using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DateBot.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef migrations</c> only.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DateBotDbContext>
{
    public DateBotDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DateBotDbContext>().UseSqlite("Data Source=design.db").Options);
}
