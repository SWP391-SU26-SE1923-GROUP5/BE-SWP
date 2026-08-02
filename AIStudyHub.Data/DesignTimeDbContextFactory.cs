using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AIStudyHub.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer("Server=.;Database=AIStudyHub;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
