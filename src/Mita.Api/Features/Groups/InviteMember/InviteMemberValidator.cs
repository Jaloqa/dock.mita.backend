using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Users;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.Groups.InviteMember;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator() =>
        RuleFor(command => command.Email).Custom((value, context) =>
        {
            if (EmailError(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(InviteMemberCommand.Email), code) { ErrorCode = code });
            }
        });

    private static string? EmailError(JsonElement? value)
    {
        if (JsonValues.IsMissing(value))
        {
            return "validation.required";
        }

        return JsonValues.TryGetString(value, out var text)
            && GroupInviteEmails.TryNormalize(text, out var email)
            && EmailAddress.IsValid(email)
            ? null
            : "validation.invalidEmail";
    }
}
