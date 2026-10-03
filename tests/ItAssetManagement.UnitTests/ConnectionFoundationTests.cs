using ItAssetManagement.Application.Contracts.Database;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ItAssetManagement.UnitTests;

public sealed class ConnectionFoundationTests
{
    // Synthetic parser fixtures only. Tests never open a PostgreSQL connection.
    private const string UriFixture = "postgresql://fixture_user:fixture_password@ep-fixture-pooler.ap-southeast-1.aws.neon.tech/fixture_db?sslmode=require&channel_binding=require";

    [Fact]
    public void Uri_is_converted_with_strict_tls_and_no_startup_options()
    {
        Assert.True(NeonConnectionPolicy.TryCreate(UriFixture, out var settings));
        Assert.Equal("fixture_db", settings!.Database);
        Assert.Equal(5432, settings.Port);
        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
        Assert.Equal(ChannelBinding.Require, settings.ChannelBinding);
        Assert.Equal(GssEncryptionMode.Disable, settings.GssEncryptionMode);
        Assert.False(settings.IncludeErrorDetail);
        Assert.True(string.IsNullOrEmpty(settings.Options));
    }

    [Fact]
    public void Percent_encoded_credentials_are_preserved_without_becoming_driver_options()
    {
        var fixture = UriFixture.Replace("fixture_password", "a%3Bb%3Dc%40d%3Ae");
        Assert.True(NeonConnectionPolicy.TryCreate(fixture, out var settings));
        Assert.Equal("a;b=c@d:e", settings!.Password);
        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
    }

    [Fact]
    public void Key_value_input_cannot_weaken_tls_or_enable_error_details()
    {
        var fixture = "Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture_user;Password=fixture_password;SSL Mode=Disable;Include Error Detail=true;Options=-c default_transaction_read_only=off";
        Assert.True(NeonConnectionPolicy.TryCreate(fixture, out var settings));
        Assert.Equal(SslMode.VerifyFull, settings!.SslMode);
        Assert.False(settings.IncludeErrorDetail);
        Assert.True(string.IsNullOrEmpty(settings.Options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a connection string")]
    [InlineData("Host=localhost;Database=fixture;Username=fixture;Password=fixture")]
    [InlineData("Host=neon.tech.attacker.invalid;Database=fixture;Username=fixture;Password=fixture")]
    [InlineData("Host=ep-fixture.neon.tech;Port=1234;Database=fixture;Username=fixture;Password=fixture")]
    [InlineData("postgresql://fixture_user:fixture_password@ep-fixture.neon.tech/fixture?options=unsafe")]
    public void Missing_invalid_or_non_neon_targets_fail_closed(string? fixture)
    {
        Assert.False(NeonConnectionPolicy.TryCreate(fixture, out var settings));
        Assert.Null(settings);
    }

    [Fact]
    public async Task Missing_and_invalid_configuration_are_reported_without_network()
    {
        Assert.Equal(DatabaseProbeStatus.NotConfigured, (await new ReadOnlyDatabaseProbe(() => null).CheckAsync()).Status);
        var invalid = await new ReadOnlyDatabaseProbe(() => "password-that-must-not-be-echoed").CheckAsync();
        Assert.Equal(DatabaseProbeStatus.InvalidConfiguration, invalid.Status);
        Assert.Null(invalid.Database);
        Assert.Null(invalid.PostgreSqlVersion);
    }

    [Fact]
    public async Task M1_context_maps_ten_tables_and_blocks_unaudited_writes()
    {
        Assert.True(NeonConnectionPolicy.TryCreate(UriFixture, out var settings));
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(settings!.ConnectionString).Options);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(10, context.Model.GetEntityTypes().Count());
        Assert.Equal(19, context.Model.GetEntityTypes().Sum(entity => entity.GetForeignKeys().Count()));
        Assert.All(context.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()), fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));
        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
        await Assert.ThrowsAsync<NotSupportedException>(() => context.SaveChangesAsync());
    }
}
