using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SimpleVault.Common.Persistence
{
    public class ContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRE_SQL_CONNECTION_STRING");

            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseNpgsql(connString);

            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
