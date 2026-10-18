namespace Mita.Api.Common;

public interface IFailableResult<TSelf>
    where TSelf : IFailableResult<TSelf>
{
    bool IsSuccess { get; }

    Error? Error { get; }

    static abstract TSelf Failure(Error error);
}

public sealed class Result : IFailableResult<Result>
{
    private static readonly Result SuccessInstance = new(null);

    private Result(Error? error) => Error = error;

    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public static Result Success() => SuccessInstance;

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : IFailableResult<Result<T>>
{
    private readonly T? value;

    private Result(T? value, Error? error)
    {
        this.value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result has no value.");

    public Error? Error { get; }

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
