using MediatR;
using Mita.Api.Common;
using Shouldly;

namespace Mita.Api.UnitTests.Architecture;

public sealed class RequestContractTests
{
    private static readonly Type[] ApiTypes = typeof(Program).Assembly.GetTypes();

    [Fact]
    public void Should_ReturnFailableResult_When_TypeIsRequest()
    {
        var requests = ApiTypes
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(i => (Request: type, Response: i.GetGenericArguments()[0])))
            .ToList();

        requests.ShouldNotBeEmpty();
        requests
            .Where(r => !IsFailableResult(r.Response))
            .Select(r => r.Request.FullName)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Should_NotImplementNonGenericRequest_When_TypeIsInApi() =>
        ApiTypes
            .Where(type => typeof(IRequest).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ShouldBeEmpty();

    private static bool IsFailableResult(Type response) =>
        response.GetInterfaces().Any(i =>
            i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(IFailableResult<>)
            && i.GetGenericArguments()[0] == response);
}
