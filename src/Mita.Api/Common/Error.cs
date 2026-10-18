namespace Mita.Api.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unavailable
}

public sealed record Error(ErrorType Type, string Code, IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fields) =>
        new(ErrorType.Validation, "validation.failed", fields);

    public static Error NotFound() => new(ErrorType.NotFound, "notFound");

    public static Error Conflict(string code) => new(ErrorType.Conflict, code);

    public static Error Unauthorized() => new(ErrorType.Unauthorized, "unauthorized");

    public static Error Unauthorized(string code) => new(ErrorType.Unauthorized, code);

    public static Error Forbidden() => new(ErrorType.Forbidden, "forbidden");

    public static Error Forbidden(string code) => new(ErrorType.Forbidden, code);

    public static Error Unavailable(string code) => new(ErrorType.Unavailable, code);
}
