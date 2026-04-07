using LLMGateway.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Tests.Data;

public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected AppDbContext Db { get; }

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
    }

    /// <summary>
    /// Clears the EF Core change tracker so FindAsync doesn't return stale tracked entities
    /// after ExecuteDeleteAsync bypasses the tracker.
    /// </summary>
    protected void DetachAll()
    {
        Db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
