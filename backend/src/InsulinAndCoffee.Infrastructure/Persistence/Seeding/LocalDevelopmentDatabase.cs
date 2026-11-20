using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InsulinAndCoffee.Infrastructure.Persistence.Seeding;

public static class LocalDevelopmentDatabase
{
    public static NpgsqlConnectionStringBuilder Verify(AppDbContext db)
    {
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1")
            || connection.Port != 5433 || connection.Database != "insulin_coffee"
            || connection.Username != "postgres" || connection.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull)
            throw new InvalidOperationException("This command requires the local Compose PostgreSQL database on port 5433 (insulin_coffee).");

        return connection;
    }
}
