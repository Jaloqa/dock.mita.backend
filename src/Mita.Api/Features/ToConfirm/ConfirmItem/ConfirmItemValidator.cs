using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.ToConfirm.ConfirmItem;

public sealed class ConfirmItemValidator : AbstractValidator<ConfirmItemCommand>
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";

    public ConfirmItemValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Version)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetString(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(command => command.AccountId)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(TransactionReferenceRules.UnknownAccount)
            .When(command => !JsonValues.IsMissing(command.AccountId));

        RuleFor(command => command.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(command => !JsonValues.IsMissing(command.Rate));

        RuleFor(command => command.Lines).Custom((value, context) =>
        {
            foreach (var (field, code) in ConfirmItemParser.LineErrors(value))
            {
                context.AddFailure(new ValidationFailure(field, code) { ErrorCode = code });
            }
        });
    }
}
