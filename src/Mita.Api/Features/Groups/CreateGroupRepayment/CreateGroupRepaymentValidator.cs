using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.CreateGroupRepayment;

public sealed class CreateGroupRepaymentValidator(TimeProvider time)
    : GroupRepaymentInputValidator<CreateGroupRepaymentCommand>(time);
