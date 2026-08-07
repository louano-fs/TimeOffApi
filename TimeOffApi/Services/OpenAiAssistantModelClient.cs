using Microsoft.Extensions.Options;
using OpenAI.Chat;

#pragma warning disable OPENAI001

namespace TimeOffApi.Services;

public sealed class OpenAiAssistantModelClient : IAssistantModelClient, IAssistantModelAvailability
{
    public const string ProviderName = "OpenAI";

    private readonly ChatClient? _client;
    private readonly ILogger<OpenAiAssistantModelClient> _logger;

    public OpenAiAssistantModelClient(
        IConfiguration configuration,
        IOptions<ManagerAssistantOptions> options,
        ILogger<OpenAiAssistantModelClient> logger)
    {
        _logger = logger;
        var apiKey = configuration["OPENAI_API_KEY"];
        if (IsConfiguredProvider(options.Value)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new ChatClient(options.Value.Model, apiKey);
        }
    }

    public bool IsAvailable => _client is not null;

    public async Task<AssistantModelResponse> CompleteAsync(
        AssistantModelRequest request,
        CancellationToken cancellationToken)
    {
        if (_client is null)
            throw new AssistantModelException("The OpenAI model client is not configured.");

        var messages = ToMessages(request);
        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxOutputTokens,
            AllowParallelToolCalls = false,
            ReasoningEffortLevel = ChatReasoningEffortLevel.Low
        };
        foreach (var tool in request.Tools)
        {
            completionOptions.Tools.Add(ChatTool.CreateFunctionTool(
                tool.Name,
                tool.Description,
                BinaryData.FromString(tool.JsonSchema),
                functionSchemaIsStrict: true));
        }

        try
        {
            var result = await _client.CompleteChatAsync(
                messages,
                completionOptions,
                cancellationToken);
            var completion = result.Value;
            var text = string.Concat(completion.Content.Select(x => x.Text));
            var toolCalls = completion.ToolCalls
                .Select(x => new AssistantModelToolCall(
                    x.Id,
                    x.FunctionName,
                    x.FunctionArguments.ToString()))
                .ToArray();
            var refused = completion.FinishReason == ChatFinishReason.ContentFilter
                || !string.IsNullOrWhiteSpace(completion.Refusal);

            return new(
                string.IsNullOrWhiteSpace(text) ? null : text,
                toolCalls,
                refused);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "OpenAI model request failed.");
            throw new AssistantModelException("The OpenAI request failed.", exception);
        }
    }

    internal static bool IsConfiguredProvider(ManagerAssistantOptions options) =>
        options.Provider.Equals(ProviderName, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(options.Model);

    private static List<ChatMessage> ToMessages(AssistantModelRequest request)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(request.SystemInstructions)
        };
        foreach (var input in request.Input)
        {
            messages.Add(input switch
            {
                AssistantModelMessage message when message.Role == AssistantModelRole.User =>
                    new UserChatMessage(message.Text),
                AssistantModelMessage message => new AssistantChatMessage(message.Text),
                AssistantModelToolCallInput call => new AssistantChatMessage(
                    [ChatToolCall.CreateFunctionToolCall(
                        call.CallId,
                        call.Name,
                        BinaryData.FromString(call.ArgumentsJson))]),
                AssistantModelToolResultInput result => new ToolChatMessage(
                    result.CallId,
                    result.ResultJson),
                _ => throw new AssistantModelException("The model input is not supported.")
            });
        }
        return messages;
    }
}

#pragma warning restore OPENAI001
