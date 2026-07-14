using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.Models.Messages;

public record ToolResult(string Content, bool IsError);

public class ToolDispatcher
{
    private readonly CalculatorTool _calculator = new();
    private readonly DateTimeTool _dateTime = new();
    private readonly FileTool _file = new();

    private readonly Dictionary<string, Func<IReadOnlyDictionary<string, JsonElement>, string>> _handlers;

    public ToolDispatcher()
    {
        _handlers = new Dictionary<string, Func<IReadOnlyDictionary<string, JsonElement>, string>>
        {
            [_calculator.Name] = input => _calculator.Execute(
                GetRequiredString(input, "operation"),
                GetRequiredDouble(input, "a"),
                GetRequiredDouble(input, "b")).ToString(CultureInfo.InvariantCulture),

            [_dateTime.Name] = input => _dateTime.Execute(
                GetOptionalString(input, "timezone"),
                GetOptionalString(input, "format")),

            [_file.Name] = input => _file.Execute(
                GetRequiredString(input, "operation"),
                GetRequiredString(input, "path"),
                GetOptionalString(input, "content")),
        };
    }

    public IReadOnlyList<object> ToolDefinitions => new object[]
    {
        new { name = _calculator.Name, description = _calculator.Description, input_schema = _calculator.InputSchema },
        new { name = _dateTime.Name, description = _dateTime.Description, input_schema = _dateTime.InputSchema },
        new { name = _file.Name, description = _file.Description, input_schema = _file.InputSchema },
    };

    public IReadOnlyList<Tool> GetTools()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return ToolDefinitions
            .Select(definition =>
            {
                var parsed = JsonSerializer.Deserialize<RawToolDefinition>(
                    JsonSerializer.Serialize(definition), options);

                return new Tool
                {
                    Name = parsed.Name,
                    Description = parsed.Description,
                    InputSchema = new()
                    {
                        Properties = parsed.InputSchema.Properties,
                        Required = parsed.InputSchema.Required ?? new List<string>(),
                    },
                };
            })
            .ToList();
    }

    private record RawToolDefinition(
        string Name,
        string Description,
        [property: JsonPropertyName("input_schema")] RawInputSchema InputSchema);

    private record RawInputSchema(string Type, Dictionary<string, JsonElement> Properties, List<string> Required);

    public ToolResult Dispatch(string toolName, IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!_handlers.TryGetValue(toolName, out var handler))
        {
            return new ToolResult($"Unknown tool: {toolName}", IsError: true);
        }

        try
        {
            return new ToolResult(handler(input), IsError: false);
        }
        catch (Exception ex)
        {
            return new ToolResult(ex.Message, IsError: true);
        }
    }

    private static string GetRequiredString(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var value))
        {
            throw new ArgumentException($"Missing required argument '{key}'.");
        }

        return value.GetString();
    }

    private static string GetOptionalString(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        return input.TryGetValue(key, out var value) ? value.GetString() : null;
    }

    private static double GetRequiredDouble(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var value))
        {
            throw new ArgumentException($"Missing required argument '{key}'.");
        }

        return value.GetDouble();
    }
}
