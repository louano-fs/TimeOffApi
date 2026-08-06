namespace TimeOffApi.Services;

public sealed class ManagerAssistantOptions
{
    public const string SectionName = "ManagerAssistant";

    public bool Enabled { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 20;
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int MaxOutputTokens { get; init; } = 800;
    public int MaxToolRounds { get; init; } = 3;
    public int MaxTeamMembers { get; init; } = 200;
    public int MaxTeamRangeDays { get; init; } = 92;
    public int MaxMessageLength { get; init; } = 1_000;

    internal static bool HasValidLimits(ManagerAssistantOptions options) =>
        options.TimeoutSeconds is >= 1 and <= 20
        && options.RequestTimeoutSeconds is >= 1 and <= 30
        && options.RequestTimeoutSeconds >= options.TimeoutSeconds
        && options.MaxOutputTokens is >= 1 and <= 800
        && options.MaxToolRounds is >= 1 and <= 3
        && options.MaxTeamMembers is >= 1 and <= 200
        && options.MaxTeamRangeDays is >= 1 and <= 92
        && options.MaxMessageLength is >= 1 and <= 1_000;

    internal static bool HasRequiredProviderSettings(ManagerAssistantOptions options) =>
        !options.Enabled
        || (!string.IsNullOrWhiteSpace(options.Provider)
            && !string.IsNullOrWhiteSpace(options.Model));
}
