namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// The customer-independent half of a grid build: what BulkOrderGridBuilder.LoadAsync READS,
/// cached per product, language and image setting. Everything after LoadAsync varies with pin state and is
/// recomputed per request.
/// </summary>
/// <remarks>
/// PLAIN DATA ONLY. This round-trips a distributed cache as JSON, so: no nop entities (they carry lazy
/// navigation and a lot of columns nothing here reads), no ImmutableArray, and no dictionary keyed by a
/// record struct - CombinationKey as a key would serialize to the struct's ToString(). The two flat lists
/// below are rebuilt into their real shapes by Hydrate at a cost of one dictionary insert per combination.
///
/// Mutable properties with public setters, for the same reason: init-only works in System.Text.Json but not
/// in every serializer a store might swap in, and this type exists only to be serialized.
/// </remarks>
public class BulkOrderSkeleton
{
    /// <summary>One entry per mapping the product had when this was built, in the service's order.</summary>
    public List<BulkOrderSkeletonMapping> Mappings { get; set; } = [];

    /// <summary>One entry per combination whose XML parsed to a non-empty signature.</summary>
    public List<BulkOrderSkeletonCombination> Combinations { get; set; } = [];

    /// <summary>Every mapping id appearing in at least one signature.</summary>
    public List<int> MappingIdsInAnySignature { get; set; } = [];
}

/// <summary>One mapping's derived data. The mapping ENTITY is re-read; this is what it cost to derive.</summary>
public class BulkOrderSkeletonMapping
{
    public int MappingId { get; set; }

    /// <summary>Localized attribute name.</summary>
    public string Name { get; set; }

    /// <summary>Localized text prompt.</summary>
    public string Prompt { get; set; }

    /// <summary>Ordered by DisplayOrder then Id, localized, picture ids resolved.</summary>
    public List<BulkOrderAxisValue> Values { get; set; } = [];

    public List<int> PreSelectedIds { get; set; } = [];

    /// <summary>Null when this mapping carries no condition - the common case.</summary>
    public BulkOrderSkeletonCondition Condition { get; set; }
}

/// <summary>A parsed condition, flattened out of AttributeCondition's ImmutableArray.</summary>
public class BulkOrderSkeletonCondition
{
    public int DependsOnMappingId { get; set; }

    public List<int> RequiredValueIds { get; set; } = [];

    public bool Satisfiable { get; set; }
}

/// <summary>One combination's parsed signature. The XML is parsed here once and never again.</summary>
public class BulkOrderSkeletonCombination
{
    public int CombinationId { get; set; }

    /// <summary>The key's parts, flattened: mapping id then its value ids.</summary>
    public List<BulkOrderSkeletonKeyPart> Parts { get; set; } = [];
}

public class BulkOrderSkeletonKeyPart
{
    public int MappingId { get; set; }

    public List<int> ValueIds { get; set; } = [];
}