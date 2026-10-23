using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.UpdateGroupRepayment;

public sealed class UpdateGroupRepaymentValidator(TimeProvider time)
    : GroupRepaymentInputValidator<UpdateGroupRepaymentCommand>(time);
