using FluentValidation;
using MediatR;
using Mita.Api.Behaviors;
using Mita.Api.Common;
using Moq;
using Shouldly;

namespace Mita.Api.UnitTests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private readonly Mock<ISampleHandler> handler = new();

    [Fact]
    public async Task Should_ReturnValidationError_When_ValidatorFails()
    {
        var behavior = CreateBehavior(new SampleCommandValidator());

        var result = await behavior.Handle(new SampleCommand(""), Next, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.Fields!["name"].ShouldBe(["validation.required"]);
        handler.Verify(h => h.Handle(), Times.Never);
    }

    [Fact]
    public async Task Should_UseCamelCaseNestedPaths_When_NestedPropertyFails()
    {
        var behavior = new ValidationBehavior<NestedCommand, Result<string>>([new NestedCommandValidator()]);
        var command = new NestedCommand([new SampleCommand("")]);

        var result = await behavior.Handle(command, Next, TestContext.Current.CancellationToken);

        result.Error!.Fields!.Keys.ShouldBe(["items[0].name"]);
    }

    [Fact]
    public async Task Should_CallHandler_When_ValidationPasses()
    {
        handler.Setup(h => h.Handle()).Returns("done");
        var behavior = CreateBehavior(new SampleCommandValidator());

        var result = await behavior.Handle(new SampleCommand("Ada"), Next, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("done");
        handler.Verify(h => h.Handle(), Times.Once);
    }

    private static ValidationBehavior<SampleCommand, Result<string>> CreateBehavior(
        params IValidator<SampleCommand>[] validators) => new(validators);

    private Task<Result<string>> Next(CancellationToken cancellationToken) => Task.FromResult<Result<string>>(handler.Object.Handle());

    public interface ISampleHandler
    {
        string Handle();
    }

    public sealed record SampleCommand(string Name) : IRequest<Result<string>>;

    public sealed record NestedCommand(IReadOnlyList<SampleCommand> Items) : IRequest<Result<string>>;

    public sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator() =>
            RuleFor(x => x.Name).NotEmpty().WithErrorCode("validation.required");
    }

    public sealed class NestedCommandValidator : AbstractValidator<NestedCommand>
    {
        public NestedCommandValidator() =>
            RuleForEach(x => x.Items).SetValidator(new SampleCommandValidator());
    }
}
