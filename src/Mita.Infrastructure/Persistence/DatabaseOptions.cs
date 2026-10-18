using Microsoft.Extensions.Options;

namespace Mita.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Mita";
    public const string ApplyMigrationsOnStartupKey = "Database:ApplyMigrationsOnStartup";

    public string ConnectionString { get; set; } = string.Empty;

    public string? ApplyMigrationsOnStartupRaw { get; set; }

    public bool ApplyMigrationsOnStartup => bool.TryParse(ApplyMigrationsOnStartupRaw, out var value) && value;
}

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail($"ConnectionStrings:{DatabaseOptions.ConnectionStringName} must be configured.");
        }

        return options.ApplyMigrationsOnStartupRaw is not null && !bool.TryParse(options.ApplyMigrationsOnStartupRaw, out _)
            ? ValidateOptionsResult.Fail($"{DatabaseOptions.ApplyMigrationsOnStartupKey} must be true or false.")
            : ValidateOptionsResult.Success;
    }
}
