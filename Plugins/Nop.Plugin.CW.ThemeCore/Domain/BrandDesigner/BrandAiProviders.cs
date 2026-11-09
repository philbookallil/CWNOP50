namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>Which call a provider takes: the Claude Messages API, or the OpenAI-style chat request.</summary>
public enum BrandAiClientKind
{
    Claude,
    Generic
}

/// <summary>How a provider is paid for, which decides the data notice the admin sees.</summary>
public enum BrandAiProviderKind
{
    Paid,
    FreeCloud,
    FreeLocal,
    Custom
}

/// <summary>A provider preset: the values it fills in. The admin can change them afterwards.</summary>
public class BrandAiProvider
{
    public string Name { get; init; }

    public BrandAiClientKind ClientKind { get; init; }

    public BrandAiProviderKind Kind { get; init; }

    public string BaseUrl { get; init; }

    /// <summary>A starting value: free model lists change often, so the model stays free text.</summary>
    public string Model { get; init; }

    public bool RequiresKey { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// The provider presets of the Brand Designer's AI assistant.
/// </summary>
public static class BrandAiProviders
{
    public const string CustomName = "Custom";

    /// <summary>All presets, in the order the list shows them.</summary>
    public static IReadOnlyList<BrandAiProvider> All { get; } =
    [
        new()
        {
            Name = "Claude",
            ClientKind = BrandAiClientKind.Claude,
            Kind = BrandAiProviderKind.Paid,
            BaseUrl = "https://api.anthropic.com",
            Model = "claude-sonnet-5-5",
            RequiresKey = true
        },
        new()
        {
            Name = "OpenAI",
            ClientKind = BrandAiClientKind.Generic,
            Kind = BrandAiProviderKind.Paid,
            BaseUrl = "https://api.openai.com/v1",
            RequiresKey = true
        },
        new()
        {
            Name = "Google Gemini",
            ClientKind = BrandAiClientKind.Generic,
            Kind = BrandAiProviderKind.FreeCloud,
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai",
            Model = "gemini-2.5-flash",
            RequiresKey = true
        },
        new()
        {
            Name = "Groq",
            ClientKind = BrandAiClientKind.Generic,
            Kind = BrandAiProviderKind.FreeCloud,
            BaseUrl = "https://api.groq.com/openai/v1",
            Model = "openai/gpt-oss-120b",
            RequiresKey = true
        },
        new()
        {
            Name = "Ollama (local)",
            ClientKind = BrandAiClientKind.Generic,
            Kind = BrandAiProviderKind.FreeLocal,
            BaseUrl = "http://localhost:11434/v1",
            TimeoutSeconds = 120
        },
        new()
        {
            Name = CustomName,
            ClientKind = BrandAiClientKind.Generic,
            Kind = BrandAiProviderKind.Custom
        }
    ];

    /// <summary>The preset of that name, or null.</summary>
    public static BrandAiProvider Find(string name)
        => All.FirstOrDefault(provider => string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase));
}
