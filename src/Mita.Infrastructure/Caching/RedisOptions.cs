using Microsoft.Extensions.Options;

namespace Mita.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string ConnectionStringName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options) =>
        string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Fail($"ConnectionStrings:{RedisOptions.ConnectionStringName} must be configured.")
            : ValidateOptionsResult.Success;
}
