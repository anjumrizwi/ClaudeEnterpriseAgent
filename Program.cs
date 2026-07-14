using System;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Claude Enterprise Agent");
Console.WriteLine("Starting...");

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["Anthropic:ApiKey"];
var model = configuration["Anthropic:Model"] ?? "claude-sonnet-5";

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Missing Anthropic:ApiKey. Set it with: dotnet user-secrets set \"Anthropic:ApiKey\" \"<key>\"");
    return;
}

var client = new AnthropicClient(apiKey, model);
var dispatcher = new ToolDispatcher();
var memory = new MemoryService();
var agent = new ClaudeAgent(client, dispatcher, memory);

Console.WriteLine("Type a message and press Enter. Type \"exit\" to quit.");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    try
    {
        var response = await agent.SendAsync(input);
        Console.WriteLine(response);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}
