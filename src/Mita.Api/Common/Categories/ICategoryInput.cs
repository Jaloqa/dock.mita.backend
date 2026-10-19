using System.Text.Json;

namespace Mita.Api.Common.Categories;

public interface ICategoryInput
{
    JsonElement? Name { get; }

    JsonElement? Kind { get; }

    JsonElement? Color { get; }
}
