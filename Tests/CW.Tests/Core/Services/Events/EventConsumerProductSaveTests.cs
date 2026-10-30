using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Plugin.CW.Core;
using Nop.Plugin.CW.Core.Services;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Common;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Events;

/// <summary>
/// What CW saves with nop's product form: the departments (ACL) and the product page's bulk-grid setting, read back off
/// the posted form, go onto the product the form was for and on no other product saved in the same request
/// </summary>
[TestFixture]
public class EventConsumerProductSaveTests
{
    private Mock<IAclOverrideService> _aclService = null!;
    private Mock<IGenericAttributeService> _genericAttributeService = null!;
    private HttpContext? _httpContext;
    private EventConsumer _consumer = null!;

    [SetUp]
    public void SetUp()
    {
        _aclService = new Mock<IAclOverrideService>();
        _genericAttributeService = new Mock<IGenericAttributeService>();
        _httpContext = new DefaultHttpContext();

        //the consumer without its other services: the product save reads only these three
        _consumer = (EventConsumer)RuntimeHelpers.GetUninitializedObject(typeof(EventConsumer));
        SetField("_aclService", _aclService.Object);
        SetField("_genericAttributeService", _genericAttributeService.Object);
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(() => _httpContext);
        SetField("_httpContextAccessor", httpContextAccessor.Object);
    }

    private void SetField(string name, object value) =>
        typeof(EventConsumer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_consumer, value);

    /// <summary>
    /// nop's product edit form for a product: two departments and "always show the grid"
    /// </summary>
    private void PostProductForm(int productId, bool postsId = true)
    {
        var form = new Dictionary<string, StringValues>
        {
            ["SelectedDepartmentIds"] = new StringValues(["3", "4"]),
            [CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY] = "1"
        };
        if (postsId)
            form["Id"] = productId.ToString();

        var request = _httpContext!.Request;
        request.Method = HttpMethods.Post;
        request.Path = $"/Admin/Product/Edit/{productId}";
        request.ContentType = "application/x-www-form-urlencoded";
        request.Form = new FormCollection(form);
    }

    private Task SaveAsync(Product product) => _consumer.HandleEventAsync(new EntityUpdatedEvent<Product>(product));

    [Test]
    public async Task The_posted_product_gets_the_forms_departments_and_grid_setting()
    {
        PostProductForm(5);
        var product = new Product { Id = 5 };

        await SaveAsync(product);

        _aclService.Verify(service => service.SaveAclDepartmentAsync(product, It.Is<IList<int>>(ids => ids.SequenceEqual(new[] { 3, 4 }))), Times.Once);
        _genericAttributeService.Verify(service => service.SaveAttributeAsync<string>(product, CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY, "1", 0), Times.Once);
    }

    [Test]
    public async Task A_form_without_an_Id_still_saves_onto_the_product()
    {
        PostProductForm(5, postsId: false);
        var product = new Product { Id = 5 };

        await SaveAsync(product);

        _aclService.Verify(service => service.SaveAclDepartmentAsync(product, It.IsAny<IList<int>>()), Times.Once);
        _genericAttributeService.Verify(service => service.SaveAttributeAsync<string>(product, CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY, "1", 0), Times.Once);
    }

    [Test]
    public async Task An_associated_product_saved_by_the_same_request_keeps_its_own()
    {
        //turning grouped product 5 into a simple one: nop clears the parent of each associated product and saves it
        PostProductForm(5);

        await SaveAsync(new Product { Id = 9 });

        _aclService.Invocations.Should().BeEmpty();
        _genericAttributeService.Invocations.Should().BeEmpty();
    }

    [Test]
    public async Task A_product_saved_without_a_request_is_left_alone()
    {
        _httpContext = null;

        await SaveAsync(new Product { Id = 5 });

        _aclService.Invocations.Should().BeEmpty();
        _genericAttributeService.Invocations.Should().BeEmpty();
    }
}
