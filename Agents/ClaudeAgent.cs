using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Anthropic.Models.Messages;

public class ClaudeAgent
{
    private readonly AnthropicClient _client;
    private readonly ToolDispatcher _dispatcher;
    private readonly MemoryService _memory;
    private readonly IReadOnlyList<Tool> _tools;
    private readonly List<MessageParam> _history = new();

    public ClaudeAgent(AnthropicClient client, ToolDispatcher dispatcher, MemoryService memory)
    {
        _client = client;
        _dispatcher = dispatcher;
        _memory = memory;
        _tools = dispatcher.GetTools();

        foreach (var message in _memory.Load())
        {
            _history.Add(new MessageParam
            {
                Role = message.Role == "assistant" ? Role.Assistant : Role.User,
                Content = message.Content,
            });
        }
    }

    public async Task<string> SendAsync(string userMessage)
    {
        _history.Add(new MessageParam { Role = Role.User, Content = userMessage });

        while (true)
        {
            var response = await _client.CreateMessageAsync(_history, _tools);

            var assistantContent = new List<ContentBlockParam>();
            var toolResults = new List<ContentBlockParam>();

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                }
                else if (block.TryPickToolUse(out ToolUseBlock toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });

                    var result = _dispatcher.Dispatch(toolUse.Name, toolUse.Input);
                    toolResults.Add(new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,
                        Content = result.Content,
                        IsError = result.IsError,
                    });
                }
            }

            _history.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });

            if (response.StopReason != "tool_use")
            {
                var finalText = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

                _memory.Append(new Message("user", userMessage));
                _memory.Append(new Message("assistant", finalText));

                return finalText;
            }

            _history.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }
    }
}
