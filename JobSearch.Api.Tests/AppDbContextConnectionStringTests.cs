using JobSearch.Data;
using Npgsql;

namespace JobSearch.Api.Tests;

public class AppDbContextConnectionStringTests
{
    // TC01 — Default pool size (20) is applied when not overridden.
    [Fact]
    public void GetConnectionString_NoOverride_AppliesDefaultPoolSize()
    {
        var connStr = AppDbContext.GetConnectionString("Host=db;Database=x;Username=u;Password=p");

        Assert.Equal(20, new NpgsqlConnectionStringBuilder(connStr).MaxPoolSize);
    }

    // TC02 — An explicit maxPoolSize overrides the default.
    // Silent failure: if this stopped applying, the worker and API would silently share the
    // same oversized pool ceiling again, defeating the point of sizing them separately.
    [Fact]
    public void GetConnectionString_ExplicitMaxPoolSize_IsApplied()
    {
        var connStr = AppDbContext.GetConnectionString("Host=db;Database=x;Username=u;Password=p", maxPoolSize: 10);

        Assert.Equal(10, new NpgsqlConnectionStringBuilder(connStr).MaxPoolSize);
    }

    // TC03 — Other connection parameters survive being rebuilt through the pool-size builder.
    [Fact]
    public void GetConnectionString_PreservesOtherConnectionParameters()
    {
        var connStr = AppDbContext.GetConnectionString("Host=myhost;Database=mydb;Username=myuser;Password=mypass");

        var parsed = new NpgsqlConnectionStringBuilder(connStr);
        Assert.Equal("myhost", parsed.Host);
        Assert.Equal("mydb", parsed.Database);
        Assert.Equal("myuser", parsed.Username);
    }
}
