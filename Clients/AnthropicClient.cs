using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Anthropic.Models.Messages;
using SdkClient = Anthropic.AnthropicClient;

public class AnthropicClient
{
    private readonly SdkClient _client;
    private readonly string _model;

    public AnthropicClient(string apiKey, string model)
    {
        _client = new SdkClient { ApiKey = apiKey };
        _model = model;
    }

    public async Task<string> SendMessageAsync(string userMessage)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            Messages = [new() { Role = Role.User, Content = userMessage }],
        });

        return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
    }

    public async Task<Anthropic.Models.Messages.Message> CreateMessageAsync(List<MessageParam> messages, IReadOnlyList<Tool> tools = null)
    {
        return await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            Messages = messages,
            Tools = tools?.Select(t => (ToolUnion)t).ToList(),
        });
    }
}
