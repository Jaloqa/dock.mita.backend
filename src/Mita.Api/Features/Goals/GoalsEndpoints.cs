using Mita.Api.Extensions;
using Mita.Api.Features.Goals.ContributeToGoal;
using Mita.Api.Features.Goals.CreateGoal;
using Mita.Api.Features.Goals.DeleteGoal;
using Mita.Api.Features.Goals.ListGoals;
using Mita.Api.Features.Goals.UpdateGoal;

namespace Mita.Api.Features.Goals;

public static class GoalsEndpoints
{
    public static IEndpointRouteBuilder MapGoalsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Goals")
            .MapListGoals()
            .MapCreateGoal()
            .MapUpdateGoal()
            .MapDeleteGoal()
            .MapContributeToGoal();

        return app;
    }
}
