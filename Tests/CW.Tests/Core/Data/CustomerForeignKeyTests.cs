using System.Data;
using System.Reflection;
using AwesomeAssertions;
using FluentMigrator.Builders.Create.Table;
using FluentMigrator.Expressions;
using FluentMigrator.Infrastructure;
using Moq;
using Nop.Core.Domain.Customers;
using Nop.Core.Infrastructure;
using Nop.Data.Mapping;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Data.Mapping;
using Nop.Plugin.CW.Core.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using NUnit.Framework;

namespace CW.Tests.Core.Data;

/// <summary>
/// No key of a CW.Core table to nop's Customer refuses the customer's delete: nop's "Delete guests" task deletes every
/// guest in one statement and publishes no event, so one refused guest stops the task for all of them
/// </summary>
[TestFixture]
public class CustomerForeignKeyTests
{
    private List<CreateForeignKeyExpression> _keys;
    private ITypeFinder _typeFinder;

    /// <summary>
    /// Forgets the names NameCompatibilityManager loaded, so a later test loads its own
    /// </summary>
    private static void ResetNameCompatibility()
    {
        var type = typeof(NameCompatibilityManager);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        ((Dictionary<Type, string>)type.GetField("_tableNames", flags)!.GetValue(null)!).Clear();
        ((Dictionary<(Type, string), string>)type.GetField("_columnName", flags)!.GetValue(null)!).Clear();
        ((IList<Type>)type.GetField("_loadedFor", flags)!.GetValue(null)!).Clear();
        type.GetField("_isInitialized", flags)!.SetValue(null, false);
    }

    [OneTimeSetUp]
    public void BuildTables()
    {
        //the builders resolve table names through nop's type finder; here CW.Core's names are the ones it finds
        var typeFinder = new Mock<ITypeFinder>();
        typeFinder.Setup(finder => finder.FindClassesOfType<INameCompatibility>(It.IsAny<bool>())).Returns([typeof(CWNameCompatibility)]);
        _typeFinder = Singleton<ITypeFinder>.Instance;
        Singleton<ITypeFinder>.Instance = typeFinder.Object;
        ResetNameCompatibility();

        _keys = [];

        foreach (var entityType in SchemaMigration.TablesInCreateOrder)
        {
            var builderType = typeof(SchemaMigration).Assembly.GetTypes()
                .Single(type => !type.IsAbstract && type.IsSubclassOf(typeof(NopEntityBuilder<>).MakeGenericType(entityType)));

            var expressions = new List<IMigrationExpression>();
            var context = new Mock<IMigrationContext>();
            context.SetupGet(migrationContext => migrationContext.Expressions).Returns(expressions);

            var table = new CreateTableExpression { TableName = TableName(entityType) };

            ((IEntityBuilder)Activator.CreateInstance(builderType)).MapEntity(new CreateTableExpressionBuilder(table, context.Object));

            _keys.AddRange(expressions.OfType<CreateForeignKeyExpression>());
        }
    }

    [OneTimeTearDown]
    public void RestoreEngine()
    {
        Singleton<ITypeFinder>.Instance = _typeFinder;
        ResetNameCompatibility();
    }

    private static string TableName(Type entityType) => new CWNameCompatibility().TableNames[entityType];

    private IEnumerable<CreateForeignKeyExpression> CustomerKeys
        => _keys.Where(key => key.ForeignKey.PrimaryTable == nameof(Customer));

    private CreateForeignKeyExpression PackNameKey
        => CustomerKeys.Single(key => key.ForeignKey.ForeignTable == TableName(typeof(PackName)));

    [Test]
    public void The_keys_to_the_customer_are_the_packnames_and_the_branch_link()
        => CustomerKeys.Select(key => $"{key.ForeignKey.ForeignTable}.{string.Join('_', key.ForeignKey.ForeignColumns)}")
            .Should().BeEquivalentTo(new[]
            {
                $"{TableName(typeof(PackName))}.{nameof(PackName.CustomerId)}",
                $"{TableName(typeof(CustomerBranch))}.{nameof(CustomerBranch.Id)}"
            });

    [Test]
    public void No_key_refuses_the_customers_delete()
        => CustomerKeys.Should().AllSatisfy(key => key.ForeignKey.OnDelete.Should().BeOneOf(Rule.SetNull, Rule.Cascade));

    [Test]
    public void A_packname_outlives_its_customer()
        => PackNameKey.ForeignKey.OnDelete.Should().Be(Rule.SetNull);

    [Test]
    public void The_update_migration_replaces_the_key_the_schema_creates()
    {
        var key = PackNameKey.ForeignKey;

        PackNameCustomerForeignKeyMigration.ForeignKeyName.Should().Be(
            $"FK_{key.ForeignTable}_{string.Join('_', key.ForeignColumns)}_{key.PrimaryTable}_{string.Join('_', key.PrimaryColumns)}");
    }
}
