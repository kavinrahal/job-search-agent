using AdminDashboard.Api.Data;

namespace AdminDashboard.Api.Tests;

public class AdminConnectionStringBuilderTests
{
    [Fact]
    public void Build_ParsesPostgresUrlIntoNpgsqlConnectionString()
    {
        var result = AdminConnectionStringBuilder.Build("postgres://user:p%40ss@myhost:5432/mydb", maxPoolSize: 10);

        Assert.Contains("Host=myhost", result);
        Assert.Contains("Port=5432", result);
        Assert.Contains("Database=mydb", result);
        Assert.Contains("Username=user", result);
        // The URL-encoded @ in the password must be decoded, not passed through literally.
        Assert.Contains("Password=p@ss", result);
        Assert.Contains("Maximum Pool Size=10", result);
    }

    [Fact]
    public void Build_PassesThroughAnAlreadyNpgsqlFormattedString()
    {
        var result = AdminConnectionStringBuilder.Build(
            "Host=localhost;Database=job_search;Username=postgres;Password=postgres", maxPoolSize: 7);

        Assert.Contains("Host=localhost", result);
        Assert.Contains("Database=job_search", result);
        Assert.Contains("Maximum Pool Size=7", result);
    }

    [Fact]
    public void Build_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => AdminConnectionStringBuilder.Build("", maxPoolSize: 5));
    }

    [Fact]
    public void ResolveWrite_DefaultsToTheReadConnectionWhenNotConfigured()
    {
        // The core requirement from the spec: WriteDatabaseUrl unset means the write context
        // points at the exact same place as the read context, with zero code change needed
        // once a real least-privilege write role is provisioned later — just set the env var.
        const string readConnectionString = "Host=readhost;Database=db;Username=u;Password=p;Maximum Pool Size=10";

        var result = AdminConnectionStringBuilder.ResolveWrite(null, readConnectionString, maxPoolSize: 5);

        Assert.Equal(readConnectionString, result);
    }

    [Fact]
    public void ResolveWrite_UsesTheConfiguredWriteUrlWhenSet()
    {
        const string readConnectionString = "Host=readhost;Database=db;Username=u;Password=p;Maximum Pool Size=10";

        var result = AdminConnectionStringBuilder.ResolveWrite(
            "postgres://writer:secret@writehost:5432/db", readConnectionString, maxPoolSize: 5);

        Assert.Contains("Host=writehost", result);
        Assert.Contains("Username=writer", result);
        Assert.DoesNotContain("readhost", result);
    }

    [Fact]
    public void ResolveWrite_TreatsWhitespaceOnlyConfiguredUrlAsUnset()
    {
        const string readConnectionString = "Host=readhost;Database=db;Username=u;Password=p;Maximum Pool Size=10";

        var result = AdminConnectionStringBuilder.ResolveWrite("   ", readConnectionString, maxPoolSize: 5);

        Assert.Equal(readConnectionString, result);
    }
}
