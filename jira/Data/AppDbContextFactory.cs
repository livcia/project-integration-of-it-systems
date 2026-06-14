using System.Diagnostics.CodeAnalysis;
using DotNetEnv;
using jira.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace jira;

[ExcludeFromCodeCoverage]
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();

        var envCandidates = new[]
        {
            Path.Combine(cwd, "..", ".env"),
            Path.Combine(cwd, ".env"),
        };

        foreach (var candidate in envCandidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                Env.Load(full);
                break;
            }
        }

        var connectionString = ConnectionStringHelper.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
