using Elastic.Apm.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Mita.Api.Extensions;
using Mita.Api.Middleware;
using Moq;
using Shouldly;

namespace Mita.Api.UnitTests.Middleware;

public sealed class ApmTransactionNameMiddlewareTests
{
    private const string Token = "Zm9v-YmFy_LWJhei1xdXV4LTAxMjM0NTY3ODlfYWJjZ";

    private const string GroupId = "0f8fad5b-d9cb-469f-a165-70867728950e";

    [Theory]
    [InlineData("GET", "/v1/shared/groups/" + Token, "/v{version:apiVersion}/shared/groups/{token}")]
    [InlineData(
        "GET",
        "/v1/shared/groups/" + Token + "/expenses/" + GroupId + "/history",
        "/v{version:apiVersion}/shared/groups/{token}/expenses/{expenseId:guid}/history")]
    [InlineData("PUT", "/v1/groups/" + GroupId, "/v{version:apiVersion}/groups/{id:guid}")]
    public async Task Should_NameTransactionByRoutePattern_When_RequestMatchedRoute(
        string method,
        string path,
        string pattern)
    {
        var transaction = Transaction($"{method} {path}");
        var context = Request(method, path, RouteEndpointOf(pattern));
        var next = new NextSpy();

        await Middleware(transaction.Object, next).InvokeAsync(context);

        transaction.Object.Name.ShouldBe($"{method} {pattern}");
        next.Called.ShouldBeTrue();
    }

    [Theory]
    [InlineData("PUT", "/v1/shared/groups/" + Token, true)]
    [InlineData("POST", "/v1/shared/groups/" + Token + "/members", false)]
    [InlineData("GET", "/v2/Shared/Groups/" + Token + "/expenses", true)]
    public async Task Should_NameTransactionUnknownRoute_When_LinkRequestMatchedNoRoute(
        string method,
        string path,
        bool rejected)
    {
        var transaction = Transaction($"{method} {path}");
        var context = Request(method, path, rejected ? RejectionEndpoint() : null);
        var next = new NextSpy();

        await Middleware(transaction.Object, next).InvokeAsync(context);

        transaction.Object.Name.ShouldBe($"{method} unknown route");
        next.Called.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/v1/unknown", false)]
    [InlineData("/v1/groups/" + GroupId, true)]
    [InlineData("/v1/shared/groups/", false)]
    public async Task Should_KeepTransactionName_When_RequestOutsideLinkMatchedNoRoute(string path, bool rejected)
    {
        var transaction = Transaction($"DELETE {path}");
        var context = Request("DELETE", path, rejected ? RejectionEndpoint() : null);
        var next = new NextSpy();

        await Middleware(transaction.Object, next).InvokeAsync(context);

        transaction.Object.Name.ShouldBe($"DELETE {path}");
        transaction.VerifySet(target => target.Name = It.IsAny<string>(), Times.Never());
        next.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_CallNext_When_NoTransaction()
    {
        var tracer = new Mock<ITracer>();
        var context = Request(
            "GET",
            "/v1/shared/groups/" + Token,
            RouteEndpointOf("/v{version:apiVersion}/shared/groups/{token}"));
        var next = new NextSpy();

        await new ApmTransactionNameMiddleware(next.InvokeAsync, tracer.Object).InvokeAsync(context);

        next.Called.ShouldBeTrue();
        tracer.VerifyGet(target => target.CurrentTransaction, Times.Once());
    }

    [Fact]
    public void Should_RegisterTracer_When_ApmServerConfigured()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ElasticApm:ServerUrl"] = "http://apm.example.com:8200";

        builder.AddMitaLogging();

        builder.Services.ShouldContain(service => service.ServiceType == typeof(ITracer));
    }

    private static ApmTransactionNameMiddleware Middleware(ITransaction transaction, NextSpy next) =>
        new(next.InvokeAsync, Mock.Of<ITracer>(target => target.CurrentTransaction == transaction));

    private static Mock<ITransaction> Transaction(string name)
    {
        var transaction = new Mock<ITransaction>();
        transaction.SetupProperty(target => target.Name, name);
        return transaction;
    }

    private static DefaultHttpContext Request(string method, string path, Endpoint? endpoint)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.SetEndpoint(endpoint);
        return context;
    }

    private static RouteEndpoint RouteEndpointOf(string pattern) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0, EndpointMetadataCollection.Empty, pattern);

    private static Endpoint RejectionEndpoint() =>
        new(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "405 HTTP Method Not Supported");

    private sealed class NextSpy
    {
        public bool Called { get; private set; }

        public Task InvokeAsync(HttpContext context)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }
}
