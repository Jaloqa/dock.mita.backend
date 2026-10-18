namespace Mita.Api.Common;

public static class ResultHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : new ErrorProblemResult(result.Error!);

    public static IResult ToHttp(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : new ErrorProblemResult(result.Error!);
}
