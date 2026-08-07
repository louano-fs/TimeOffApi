using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class OpenAiAssistantModelClientTests
{
    [Fact]
    public void Is_available_only_when_openai_model_and_api_key_are_configured()
    {
        var configured = CreateClient(
            provider: "OpenAI",
            model: "gpt-5-nano",
            apiKey: "test-key");
        var missingKey = CreateClient(
            provider: "OpenAI",
            model: "gpt-5-nano",
            apiKey: null);
        var otherProvider = CreateClient(
            provider: "Other",
            model: "gpt-5-nano",
            apiKey: "test-key");

        configured.IsAvailable.Should().BeTrue();
        missingKey.IsAvailable.Should().BeFalse();
        otherProvider.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_configuration_fails_before_any_provider_request()
    {
        var client = CreateClient(
            provider: "OpenAI",
            model: "gpt-5-nano",
            apiKey: null);

        var action = () => client.CompleteAsync(
            new AssistantModelRequest("instructions", [], [], 100),
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<AssistantModelException>())
            .Which.Message.Should().Contain("not configured");
    }

    [Theory]
    [InlineData("OpenAI", "gpt-5-nano", true)]
    [InlineData("openai", "gpt-5-nano", true)]
    [InlineData("OpenAI", "", false)]
    [InlineData("Other", "gpt-5-nano", false)]
    public void Provider_selection_is_explicit_and_case_insensitive(
        string provider,
        string model,
        bool expected)
    {
        OpenAiAssistantModelClient.IsConfiguredProvider(new ManagerAssistantOptions
        {
            Provider = provider,
            Model = model
        }).Should().Be(expected);
    }

    private static OpenAiAssistantModelClient CreateClient(
        string provider,
        string model,
        string? apiKey)
    {
        var values = new Dictionary<string, string?>();
        if (apiKey is not null)
            values["OPENAI_API_KEY"] = apiKey;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new(
            configuration,
            Options.Create(new ManagerAssistantOptions
            {
                Provider = provider,
                Model = model
            }),
            NullLogger<OpenAiAssistantModelClient>.Instance);
    }
}
