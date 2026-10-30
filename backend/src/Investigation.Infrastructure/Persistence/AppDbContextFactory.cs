using Investigation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Investigation.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = "Host=localhost;Port=5432;Database=investigation;Username=investigation;Password=investigation";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection);
        return new AppDbContext(options.Options);
    }
}
