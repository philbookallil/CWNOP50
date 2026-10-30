using AwesomeAssertions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Data.Mapping;
using Nop.Plugin.CW.Core.Data.Migrations;
using NUnit.Framework;

namespace CW.Tests.Core.Data;

/// <summary>
/// The table list SchemaMigration creates from: every CW.Core table is in it once, and every type in it is a table
/// with a name and a builder. A table added later without being added to the list fails here.
/// </summary>
/// <remarks>
/// The foreign-key order of the list is not checked here: the builders resolve table names through nop's engine.
/// The plugin install proves it.
/// </remarks>
[TestFixture]
public class SchemaMigrationTests
{
    /// <summary>
    /// Prefix of the SQL views CWNameCompatibility also maps; the views are created by CWDatabaseService, not by the migration
    /// </summary>
    private const string ViewPrefix = "vw_";

    private static readonly Dictionary<Type, string> TableNames = new CWNameCompatibility().TableNames;

    private static IEnumerable<Type> MappedTables
        => TableNames.Where(entry => !entry.Value.StartsWith(ViewPrefix, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Key);

    /// <summary>
    /// Entity type of every NopEntityBuilder in the CW.Core assembly
    /// </summary>
    private static IEnumerable<Type> BuiltEntities
        => typeof(SchemaMigration).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => type.BaseType)
            .Where(baseType => baseType is { IsGenericType: true } && baseType.GetGenericTypeDefinition() == typeof(NopEntityBuilder<>))
            .Select(baseType => baseType!.GetGenericArguments()[0]);

    [Test]
    public void Every_mapped_table_is_created()
        => SchemaMigration.TablesInCreateOrder.Should().Contain(MappedTables);

    [Test]
    public void Every_created_type_is_a_mapped_table()
        => MappedTables.Should().Contain(SchemaMigration.TablesInCreateOrder);

    [Test]
    public void No_table_is_created_twice()
        => SchemaMigration.TablesInCreateOrder.Should().OnlyHaveUniqueItems();

    [Test]
    public void No_view_is_created_as_a_table()
        => SchemaMigration.TablesInCreateOrder
            .Select(type => TableNames[type])
            .Should().NotContain(name => name.StartsWith(ViewPrefix, StringComparison.OrdinalIgnoreCase));

    [Test]
    public void Every_created_table_has_a_builder()
        => BuiltEntities.Should().Contain(SchemaMigration.TablesInCreateOrder);

    [Test]
    public void Every_builder_table_is_created()
        => SchemaMigration.TablesInCreateOrder.Should().Contain(BuiltEntities);
}
