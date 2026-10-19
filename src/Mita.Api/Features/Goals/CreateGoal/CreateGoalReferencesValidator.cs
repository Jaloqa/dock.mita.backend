using Mita.Api.Authentication;
using Mita.Api.Common.Goals;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.CreateGoal;

public sealed class CreateGoalReferencesValidator(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : GoalReferencesValidator<CreateGoalCommand>(db, currentUser, time);
