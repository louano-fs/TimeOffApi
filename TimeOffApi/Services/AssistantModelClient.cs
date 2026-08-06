namespace TimeOffApi.Services;

public enum AssistantModelRole
{
    User,
    Assistant
}

public abstract record AssistantModelInput;

public sealed record AssistantModelMessage(
    AssistantModelRole Role,
    string Text) : AssistantModelInput;

public sealed record AssistantModelToolCallInput(
    string CallId,
    string Name,
    string ArgumentsJson) : AssistantModelInput;

public sealed record AssistantModelToolResultInput(
    string CallId,
    string Name,
    string ResultJson) : AssistantModelInput;

public sealed record AssistantToolDefinition(
    string Name,
    string Description,
    string JsonSchema);

public sealed record AssistantModelRequest(
    string SystemInstructions,
    IReadOnlyCollection<AssistantModelInput> Input,
    IReadOnlyCollection<AssistantToolDefinition> Tools,
    int MaxOutputTokens);

public sealed record AssistantModelToolCall(
    string CallId,
    string Name,
    string ArgumentsJson);

public sealed record AssistantModelResponse(
    string? Text,
    IReadOnlyCollection<AssistantModelToolCall> ToolCalls,
    bool Refused = false);

public interface IAssistantModelClient
{
    Task<AssistantModelResponse> CompleteAsync(
        AssistantModelRequest request,
        CancellationToken cancellationToken);
}

public interface IAssistantModelAvailability
{
    bool IsAvailable { get; }
}

public sealed class AssistantModelException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class UnconfiguredAssistantModelClient
    : IAssistantModelClient, IAssistantModelAvailability
{
    public bool IsAvailable => false;

    public Task<AssistantModelResponse> CompleteAsync(
        AssistantModelRequest request,
        CancellationToken cancellationToken) =>
        throw new AssistantModelException("No live assistant model provider is configured.");
}
