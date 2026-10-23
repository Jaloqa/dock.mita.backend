using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.AddCategory;

public sealed record AddCategoryRequest(JsonElement? Name);

public sealed record AddCategoryCommand(GroupScope Scope, JsonElement? Name) : IRequest<Result<GroupCategoryDto>>;
