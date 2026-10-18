using Microsoft.Extensions.Options;

namespace Mita.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Mita";

    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
        string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Fail($"ConnectionStrings:{DatabaseOptions.ConnectionStringName} must be configured.")
            : ValidateOptionsResult.Success;
}
