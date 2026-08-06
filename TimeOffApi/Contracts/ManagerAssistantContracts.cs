namespace TimeOffApi.Contracts;

public sealed record ManagerAssistantCapabilitiesResponse(
    bool Enabled,
    string? Audience,
    string? Scope,
    bool Streaming,
    int MaxMessageLength);
