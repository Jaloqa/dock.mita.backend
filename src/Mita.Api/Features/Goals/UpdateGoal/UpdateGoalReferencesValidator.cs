using Mita.Api.Authentication;
using Mita.Api.Common.Goals;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.UpdateGoal;

public sealed class UpdateGoalReferencesValidator(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : GoalReferencesValidator<UpdateGoalCommand>(db, currentUser, time);
