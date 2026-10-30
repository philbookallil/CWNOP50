using AwesomeAssertions;
using Nop.Core.Infrastructure.Mapper;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Infrastructure.Mapper;
using Nop.Web.Areas.Admin.Infrastructure.Mapper;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using NUnit.Framework;

namespace CW.Tests.Core.Infrastructure;

/// <summary>
/// The subsidy admin maps, through the same <c>ToEntity</c> extensions the subsidy controller calls. A save maps the
/// posted form onto the stored entity, so the fields that are not form fields (the store, the creation date, the deleted
/// flag) must come through it unchanged. nop 5.00 maps with Mapster; 4.9 mapped with AutoMapper.
/// </summary>
[TestFixture]
public class CWMapperConfigurationProfileTests
{
    [OneTimeSetUp]
    public void InitMapper()
    {
        //as nop does at start-up: every profile, by order
        var profiles = new IOrderedMapperProfile[] { new AdminMapperConfiguration(), new CWMapperConfigurationProfile() };
        MapperConfiguration.Init(profiles.OrderBy(profile => profile.Order));
    }

    [Test]
    public void Editing_a_subsidy_keeps_its_store_creation_date_and_deleted_flag()
    {
        var created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var subsidy = new Subsidy { Id = 5, StoreId = 2, Name = "Old", CreatedOnUtc = created, Deleted = true, Credit = 10 };
        var model = new SubsidyModel
        {
            Id = 5,
            StoreId = 9,
            Name = "New",
            Credit = 50,
            DurationStart = new DateTime(2026, 1, 1),
            DurationEnd = new DateTime(2026, 12, 31),
            Active = true
        };

        subsidy = model.ToEntity(subsidy);

        subsidy.StoreId.Should().Be(2);
        subsidy.CreatedOnUtc.Should().Be(created);
        subsidy.Deleted.Should().BeTrue();
        subsidy.Name.Should().Be("New");
        subsidy.Credit.Should().Be(50);
        subsidy.DurationStart.Should().Be(new DateTime(2026, 1, 1));
        subsidy.DurationEnd.Should().Be(new DateTime(2026, 12, 31));
        subsidy.Active.Should().BeTrue();
    }

    [Test]
    public void A_new_subsidy_takes_no_store_or_creation_date_from_the_form()
    {
        var model = new SubsidyModel { Id = 0, StoreId = 9, Name = "New", DurationStart = new DateTime(2026, 1, 1), DurationEnd = new DateTime(2026, 12, 31) };

        var subsidy = model.ToEntity<Subsidy>();

        subsidy.StoreId.Should().Be(0, "the controller sets the store on an insert");
        subsidy.CreatedOnUtc.Should().Be(default);
        subsidy.Name.Should().Be("New");
    }

    [Test]
    public void Editing_a_product_group_keeps_its_store_and_creation_date()
    {
        var created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var productGroup = new ProductGroup { Id = 3, StoreId = 2, Name = "Old", CreatedOnUtc = created, Active = false };
        var model = new ProductGroupModel { Id = 3, StoreId = 9, Name = "New", Colour = "#112233", DisplayOrder = 4, Active = true };

        productGroup = model.ToEntity(productGroup);

        productGroup.StoreId.Should().Be(2);
        productGroup.CreatedOnUtc.Should().Be(created);
        productGroup.Name.Should().Be("New");
        productGroup.Colour.Should().Be("#112233");
        productGroup.DisplayOrder.Should().Be(4);
        productGroup.Active.Should().BeTrue();
    }

    [Test]
    public void A_new_variation_takes_its_values_from_the_form_but_not_a_creation_date()
    {
        var model = new SubsidyVariationModel { SubsidyPackNameId = 4, Credit = 7, DurationStart = new DateTime(2026, 2, 1), DurationEnd = new DateTime(2026, 3, 1) };

        var variation = model.ToEntity<SubsidyVariation>();

        variation.SubsidyPackNameId.Should().Be(4);
        variation.Credit.Should().Be(7);
        variation.DurationStart.Should().Be(new DateTime(2026, 2, 1));
        variation.DurationEnd.Should().Be(new DateTime(2026, 3, 1));
        variation.CreatedOnUtc.Should().Be(default, "the controller sets it");
    }

    [Test]
    public void A_subsidy_opens_in_the_form_with_its_values()
    {
        var subsidy = new Subsidy
        {
            Id = 5,
            StoreId = 2,
            Name = "Uniform 2026",
            Credit = 300,
            DurationStart = new DateTime(2026, 1, 1),
            DurationEnd = new DateTime(2026, 12, 31),
            AnnualRecurrence = true,
            OnlyAllocatedProducts = true
        };

        var model = subsidy.ToModel<SubsidyModel>();

        model.Id.Should().Be(5);
        model.StoreId.Should().Be(2);
        model.Name.Should().Be("Uniform 2026");
        model.Credit.Should().Be(300);
        model.DurationStart.Should().Be(new DateTime(2026, 1, 1));
        model.DurationEnd.Should().Be(new DateTime(2026, 12, 31));
        model.AnnualRecurrence.Should().BeTrue();
        model.OnlyAllocatedProducts.Should().BeTrue();
    }
}
