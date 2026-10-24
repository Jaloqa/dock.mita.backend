using FluentValidation;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.ToConfirm.RejectItem;

public sealed class RejectItemValidator : AbstractValidator<RejectItemCommand>
{
    public RejectItemValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Version)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode("validation.required")
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode("validation.invalidValue");
    }
}
