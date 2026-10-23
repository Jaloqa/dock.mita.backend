using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.Report;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mita.Api.Common.Logging;
using Mita.Api.Extensions;
using Moq;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class ShareLinkApmFiltersTests
{
    private const string Token = "Zm9v-YmFy_LWJhei1xdXV4LTAxMjM0NTY3ODlfYWJjZ";

    [Fact]
    public async Task Should_AddRedactingFilters_When_Started()
    {
        Func<ITransaction, ITransaction>? transactionFilter = null;
        Func<IError, IError>? errorFilter = null;
        var sender = new Mock<IPayloadSender>();
        var filters = sender.As<IPayloadSenderWithFilters>();
        filters.Setup(target => target.AddFilter(It.IsAny<Func<ITransaction, ITransaction>>()))
            .Callback<Func<ITransaction, ITransaction>>(filter => transactionFilter = filter)
            .Returns(true);
        filters.Setup(target => target.AddFilter(It.IsAny<Func<IError, IError>>()))
            .Callback<Func<IError, IError>>(filter => errorFilter = filter)
            .Returns(true);
        var agent = Mock.Of<IApmAgent>(target => target.PayloadSender == sender.Object);
        var logger = new CapturingLogger();

        await new ShareLinkApmFilters(agent, logger).StartAsync(TestContext.Current.CancellationToken);

        var transaction = Transaction($"GET /v1/shared/groups/{Token}", LinkContext());
        transactionFilter.ShouldNotBeNull().Invoke(transaction).ShouldBeSameAs(transaction);
        ShouldBeMasked(transaction.Context.Request.Url);
        var error = new AgentError(LinkContext());
        errorFilter.ShouldNotBeNull().Invoke(error).ShouldBeSameAs(error);
        ShouldBeMasked(error.Context.Request.Url);
        logger.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, true, "transaction")]
    [InlineData(true, false, "error")]
    public async Task Should_LogWarning_When_SenderRefusesFilter(
        bool transactionAdded,
        bool errorAdded,
        string refused)
    {
        var sender = new Mock<IPayloadSender>();
        var filters = sender.As<IPayloadSenderWithFilters>();
        filters.Setup(target => target.AddFilter(It.IsAny<Func<ITransaction, ITransaction>>()))
            .Returns(transactionAdded);
        filters.Setup(target => target.AddFilter(It.IsAny<Func<IError, IError>>()))
            .Returns(errorAdded);
        var agent = Mock.Of<IApmAgent>(target => target.PayloadSender == sender.Object);
        var logger = new CapturingLogger();

        await new ShareLinkApmFilters(agent, logger).StartAsync(TestContext.Current.CancellationToken);

        logger.Warnings.ShouldHaveSingleItem().ShouldContain(refused);
    }

    [Fact]
    public async Task Should_LogWarnings_When_SenderTakesNoFilters()
    {
        var agent = Mock.Of<IApmAgent>(target => target.PayloadSender == Mock.Of<IPayloadSender>());
        var logger = new CapturingLogger();

        await new ShareLinkApmFilters(agent, logger).StartAsync(TestContext.Current.CancellationToken);

        logger.Warnings.Count.ShouldBe(2);
        logger.Warnings.ShouldContain(warning => warning.Contains("transaction", StringComparison.Ordinal));
        logger.Warnings.ShouldContain(warning => warning.Contains("error", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_MaskShareToken_When_TransactionIsLinkRequest()
    {
        var transaction = Transaction($"GET /v1/shared/groups/{Token}/members", LinkContext());

        ShareLinkApmFilters.RedactTransaction(transaction).ShouldBeSameAs(transaction);

        transaction.Name.ShouldBe("GET /v1/shared/groups/***/members");
        ShouldBeMasked(transaction.Context.Request.Url);
    }

    [Fact]
    public void Should_KeepTransaction_When_NoLinkRequest()
    {
        var owner = Transaction(
            "GET /v{version:apiVersion}/groups/{id:guid}",
            new Context
            {
                Request = new Request("GET", new Url { PathName = "/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e" })
            });
        var background = Transaction("RatesSync", new Context());

        ShareLinkApmFilters.RedactTransaction(owner).ShouldBeSameAs(owner);
        ShareLinkApmFilters.RedactTransaction(background).ShouldBeSameAs(background);

        owner.Name.ShouldBe("GET /v{version:apiVersion}/groups/{id:guid}");
        owner.Context.Request.Url.PathName.ShouldBe("/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e");
        background.Context.Request.ShouldBeNull();
    }

    [Fact]
    public void Should_MaskShareToken_When_ErrorCarriesLinkRequest()
    {
        var logger = new CapturingLogger();
        var error = new AgentError(LinkContext());

        Filters(logger).RedactError(error).ShouldBeSameAs(error);

        ShouldBeMasked(error.Context.Request.Url);
        logger.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Should_KeepError_When_ErrorHasNoRequest()
    {
        var logger = new CapturingLogger();
        var withoutRequest = new AgentError(new Context());

        Filters(logger).RedactError(withoutRequest).ShouldBeSameAs(withoutRequest);

        withoutRequest.Context.Request.ShouldBeNull();
        logger.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_LogWarningOnce_When_ErrorTypeHidesRequestContext(bool foreignContext)
    {
        var logger = new CapturingLogger();
        var filters = Filters(logger);
        IError Error() => foreignContext ? new ErrorWithForeignContext() : new ErrorWithoutContext();
        var first = Error();
        var second = Error();

        filters.RedactError(first).ShouldBeSameAs(first);
        filters.RedactError(second).ShouldBeSameAs(second);

        logger.Warnings.ShouldHaveSingleItem().ShouldContain(first.GetType().Name);
    }

    [Fact]
    public void Should_RegisterFilters_When_ApmServerConfigured()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ElasticApm:ServerUrl"] = "http://apm.example.com:8200";

        builder.AddMitaLogging();

        builder.Services.ShouldContain(service =>
            service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(ShareLinkApmFilters));
    }

    [Fact]
    public void Should_NotRegisterFilters_When_ApmServerMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ElasticApm:ServerUrl"] = string.Empty;

        builder.AddMitaLogging();

        builder.Services.ShouldNotContain(service => service.ImplementationType == typeof(ShareLinkApmFilters));
    }

    private static ShareLinkApmFilters Filters(CapturingLogger logger) =>
        new(Mock.Of<IApmAgent>(), logger);

    private static ITransaction Transaction(string name, Context context)
    {
        var transaction = new Mock<ITransaction>();
        transaction.SetupProperty(target => target.Name, name);
        transaction.SetupGet(target => target.Context).Returns(context);
        return transaction.Object;
    }

    private static Context LinkContext() => new()
    {
        Request = new Request(
            "GET",
            new Url
            {
                Full = $"https://api.example.com/v1/shared/groups/{Token}/expenses?deleted=true",
                HostName = "api.example.com",
                PathName = $"/v1/shared/groups/{Token}/expenses",
                Protocol = "https",
                Raw = $"/v1/shared/groups/{Token}/expenses?deleted=true",
                Search = "deleted=true"
            })
    };

    private static void ShouldBeMasked(Url url)
    {
        url.Full.ShouldBe("https://api.example.com/v1/shared/groups/***/expenses?deleted=true");
        url.PathName.ShouldBe("/v1/shared/groups/***/expenses");
        url.Raw.ShouldBe("/v1/shared/groups/***/expenses?deleted=true");
        url.HostName.ShouldBe("api.example.com");
        url.Search.ShouldBe("deleted=true");
    }

    private sealed class CapturingLogger : ILogger<ShareLinkApmFilters>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private sealed class AgentError(Context context) : IError
    {
        public Context Context { get; } = context;

        public string Culprit { get; set; } = "Mita.Api";

        public CapturedException Exception { get; } = new();

        public string Id => "error";

        public ErrorLog Log { get; set; } = new("Unhandled exception");

        public string ParentId => "parent";

        public string TraceId => "trace";

        public string TransactionId => "transaction";
    }

    private sealed class ErrorWithoutContext : IError
    {
        public string Culprit { get; set; } = "Mita.Api";

        public CapturedException Exception { get; } = new();

        public string Id => "error";

        public ErrorLog Log { get; set; } = new("Unhandled exception");

        public string ParentId => "parent";

        public string TraceId => "trace";

        public string TransactionId => "transaction";
    }

    private sealed class ErrorWithForeignContext : IError
    {
        public object Context { get; } = LinkContext();

        public string Culprit { get; set; } = "Mita.Api";

        public CapturedException Exception { get; } = new();

        public string Id => "error";

        public ErrorLog Log { get; set; } = new("Unhandled exception");

        public string ParentId => "parent";

        public string TraceId => "trace";

        public string TransactionId => "transaction";
    }
}
