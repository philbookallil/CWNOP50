using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// One way of calling an AI provider. A failed call throws <see cref="BrandAiException"/>.
/// </summary>
public interface ICwBrandAiClient
{
    /// <summary>Ask the model for a style.</summary>
    Task<BrandAiAnswer> GenerateAsync(BrandAiConnection connection, BrandAiRequest request, CancellationToken cancellationToken = default);

    /// <summary>One minimal request, to check the connection works.</summary>
    Task TestAsync(BrandAiConnection connection, CancellationToken cancellationToken = default);
}

/// <summary>
/// Picks the client a provider takes.
/// </summary>
public interface ICwBrandAiClientResolver
{
    ICwBrandAiClient Get(BrandAiClientKind kind);
}

/// <inheritdoc />
public class CwBrandAiClientResolver : ICwBrandAiClientResolver
{
    private readonly CwClaudeBrandAiClient _claude;
    private readonly CwOpenAiCompatibleBrandAiClient _generic;

    public CwBrandAiClientResolver(CwClaudeBrandAiClient claude, CwOpenAiCompatibleBrandAiClient generic)
    {
        _claude = claude;
        _generic = generic;
    }

    public ICwBrandAiClient Get(BrandAiClientKind kind) => kind == BrandAiClientKind.Claude ? _claude : _generic;
}
