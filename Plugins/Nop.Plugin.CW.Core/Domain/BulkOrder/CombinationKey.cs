using System.Collections.Immutable;
using System.Diagnostics;

namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// The identity of a combination: which combinable attributes it uses, and which values of each.
/// </summary>
/// <remarks>
/// Value <b>quantity is ignored</b>, to match nop: FindProductAttributeCombinationAsync
/// (ProductAttributeParser.cs:774) calls AreProductAttributesEqualAsync with the default
/// ignoreQuantity: true (:650), which compares by SET EQUALITY - a combination matches only a selection
/// built from exactly the attributes that combination was built from. That set is its signature.
/// </remarks>
public readonly record struct CombinationKey(ImmutableArray<CombinationKeyPart> Parts)
{
    /// <summary>
    /// Builds a key from unordered parts. Sorting is what makes two equal selections compare equal,
    /// so it happens here and nowhere else.
    /// </summary>
    public static CombinationKey Create(IEnumerable<(int MappingId, IEnumerable<int> ValueIds)> parts)
    {
        //Merge by mapping id: nop's ParseValues unions the values of every <ProductAttribute> node sharing an
        //ID (ProductAttributeParser.cs:521-540), so two such nodes are ONE attribute, not two. Merging here
        //also makes Parts.Length the count of distinct attributes, which HasSignature relies on.
        var ordered = parts
            .GroupBy(part => part.MappingId)
            .Select(group => new CombinationKeyPart(group.Key,
                [.. group.SelectMany(part => part.ValueIds).Distinct().Order()]))
            .Where(part => part.ValueIds.Length > 0)
            .OrderBy(part => part.MappingId)
            .ToImmutableArray();

        return new CombinationKey(ordered);
    }

    public static CombinationKey FromNormalized(IEnumerable<CombinationKeyPart> parts)
    {
        var array = parts.ToImmutableArray();

        Debug.Assert(IsNormalized(array), "CombinationKey.FromNormalized: parts are not normalised - use Create.");

        return new CombinationKey(array);
    }

    /// <summary>
    /// The precondition of FromNormalized, in one linear pass and with no allocation.
    /// </summary>
    /// <remarks>
    /// Written as a loop rather than as LINQ assertions on purpose. Debug.Assert is [Conditional("DEBUG")],
    /// so this costs nothing in Release - but it runs per combination per request in a DEBUG build, which is
    /// how the site is developed and tested, and Order()/Distinct() per part would allocate more than the
    /// Create pipeline this whole change exists to remove.
    ///
    /// The two strict inequalities each do two jobs: >= on mapping ids catches unordered AND unmerged parts,
    /// >= on value ids catches unordered AND duplicated values. Empty ValueIds is a fault of its own - Create
    /// drops such parts, so one arriving here means the caller did not come through Create.
    /// </remarks>
    private static bool IsNormalized(ImmutableArray<CombinationKeyPart> parts)
    {
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0 && parts[i - 1].MappingId >= parts[i].MappingId)
                return false;

            var valueIds = parts[i].ValueIds;

            if (valueIds.Length == 0)
                return false;

            for (var j = 1; j < valueIds.Length; j++)
                if (valueIds[j - 1] >= valueIds[j])
                    return false;
        }

        return true;
    }

    /// <summary>The mapping ids this key covers - the signature.</summary>
    public IEnumerable<int> MappingIds => Parts.Select(part => part.MappingId);

    /// <summary>True when this key's signature is exactly the given set of mapping ids.</summary>
    /// <remarks>
    /// The length test is valid because Create merges parts by mapping id. Written as a loop rather than
    /// LINQ deliberately: this runs once per combination per grid rebuild, and foreach over ImmutableArray
    /// uses a struct enumerator, so a candidate that fails costs no allocation at all.
    /// </remarks>
    public bool HasSignature(ISet<int> mappingIds)
    {
        if (Parts.Length != mappingIds.Count)
            return false;

        foreach (var part in Parts)
        {
            if (!mappingIds.Contains(part.MappingId))
                return false;
        }

        return true;
    }

    public bool Equals(CombinationKey other)
        => Parts.Length == other.Parts.Length && Parts.SequenceEqual(other.Parts);

    public override int GetHashCode()
    {
        //deterministic across processes - the grid skeleton cache key is derived from this
        var hash = 17;

        foreach (var part in Parts)
        {
            hash = unchecked(hash * 31 + part.MappingId);

            foreach (var valueId in part.ValueIds)
                hash = unchecked(hash * 31 + valueId);
        }

        return hash;
    }
}

/// <summary>One attribute's contribution to a <see cref="CombinationKey"/>. ValueIds are sorted ascending.</summary>
public readonly record struct CombinationKeyPart(int MappingId, ImmutableArray<int> ValueIds)
{
    public bool Equals(CombinationKeyPart other)
        => MappingId == other.MappingId && ValueIds.SequenceEqual(other.ValueIds);

    public override int GetHashCode() => MappingId;
}