using System.Collections.Immutable;

namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// One attribute's parsed ConditionAttributeXml. Parsed once per product in LoadAsync rather than
/// evaluated through IProductAttributeParser.IsConditionMetAsync, which takes an attributes-XML STRING: using
/// it would mean building one such string per pin state and per axis value. Evaluating conditions inside a
/// per-cell loop is the expensive shape; this parses once and compares ids afterwards.
/// </summary>
/// <param name="MappingId">The attribute carrying the condition</param>
/// <param name="DependsOnMappingId">The attribute it depends on</param>
/// <param name="RequiredValueIds">
/// The values A must hold, EXACTLY — see IsMetBy. Empty is meaningful: admin writes an empty Value element to
/// mean "nothing selected" (ProductController.cs:658-660), and nop strips it from this list, so an empty list
/// is a condition met only when A holds nothing.
/// </param>
/// <param name="Satisfiable">
/// False when a required entry was present and non-empty but is not a value id. nop compares such an entry as
/// a string against value ids and never matches, so the condition is dead. Only reachable by hand-editing the
/// column; recorded rather than ignored so that our answer is nop's answer in every case.
/// </param>
public readonly record struct AttributeCondition(
    int MappingId,
    int DependsOnMappingId,
    ImmutableArray<int> RequiredValueIds,
    bool Satisfiable)
{
    /// <summary>
    /// nop's comparison, exactly: equal COUNT, then every required value present
    /// (ProductAttributeParser.cs:746-761). Count first is not an optimisation — it is the reason a two-value
    /// condition can never be met by a single-select axis value.
    /// </summary>
    public bool IsMetBy(IReadOnlyCollection<int> selectedValueIds)
    {
        if (!Satisfiable)
            return false;

        if (selectedValueIds.Count != RequiredValueIds.Length)
            return false;

        foreach (var required in RequiredValueIds)
            if (!selectedValueIds.Contains(required))
                return false;

        return true;
    }
}