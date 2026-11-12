using AwesomeAssertions;
using Moq;
using Nop.Plugin.CW.Core.Areas.Admin.Controllers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The admin customer page saves only a branch it could have offered: an active, not deleted branch of the customer's
/// store, or no branch
/// </summary>
[TestFixture]
public class CustomerBranchValidationTests
{
    private const int StoreId = 2;

    private ExtendedCustomerController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        var branchService = new Mock<IBranchService>();
        branchService.Setup(service => service.GetBranchByIdAsync(10)).ReturnsAsync(new Branch { Id = 10, StoreId = StoreId, Active = true });
        branchService.Setup(service => service.GetBranchByIdAsync(11)).ReturnsAsync(new Branch { Id = 11, StoreId = StoreId, Active = true, Deleted = true });
        branchService.Setup(service => service.GetBranchByIdAsync(12)).ReturnsAsync(new Branch { Id = 12, StoreId = StoreId, Active = false });
        branchService.Setup(service => service.GetBranchByIdAsync(13)).ReturnsAsync(new Branch { Id = 13, StoreId = 3, Active = true });

        _controller = TestObject.Create<ExtendedCustomerController>();
        TestObject.Set(_controller, "_branchService", branchService.Object);
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);
        TestObject.Set(_controller, "_localizationService", localizationService.Object);
    }

    private Task<bool> ValidateAsync(int branchId) => TestObject.InvokeAsync<bool>(_controller, "ValidateBranchAsync", branchId, StoreId);

    [TestCase(0, Description = "no branch")]
    [TestCase(10, Description = "an active branch of the store")]
    public async Task A_branch_the_page_offers_is_accepted(int branchId)
    {
        (await ValidateAsync(branchId)).Should().BeTrue();
        _controller.ModelState.IsValid.Should().BeTrue();
    }

    [TestCase(404, Description = "a branch that does not exist")]
    [TestCase(11, Description = "a deleted branch")]
    [TestCase(12, Description = "an inactive branch")]
    [TestCase(13, Description = "a branch of another store")]
    public async Task Any_other_branch_is_refused(int branchId)
    {
        (await ValidateAsync(branchId)).Should().BeFalse();
        _controller.ModelState.ContainsKey("BranchId").Should().BeTrue();
    }
}
