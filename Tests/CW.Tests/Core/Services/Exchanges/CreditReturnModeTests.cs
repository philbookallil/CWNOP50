using AwesomeAssertions;
using Moq;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Exchanges;

/// <summary>
/// The ways of returning an item the wizard offers, and accepts, for a reason and an action: an exchange and a credit
/// return where both rules allow it, and no credit return on a store with Credit Notes switched off
/// </summary>
[TestFixture]
public class CreditReturnModeTests
{
    private const int StoreId = 1;
    private const int ReasonId = 3;
    private const int ActionId = 4;

    private ExchangeService _service = null!;
    private bool _creditNotesEnabled;

    [SetUp]
    public void SetUp()
    {
        _creditNotesEnabled = true;

        var reasonRules = new List<ReturnReasonRule> { new() { ReturnReasonId = ReasonId, StoreId = StoreId, AllowExchange = true, AllowStoreCredit = true } };
        var actionRules = new List<ReturnActionRule> { new() { ReturnActionId = ActionId, StoreId = StoreId, AllowExchange = true, AllowStoreCredit = true } };

        var reasonRuleRepository = new Mock<IRepository<ReturnReasonRule>>();
        reasonRuleRepository.Setup(repository => repository.Table).Returns(() => reasonRules.AsQueryable());
        var actionRuleRepository = new Mock<IRepository<ReturnActionRule>>();
        actionRuleRepository.Setup(repository => repository.Table).Returns(() => actionRules.AsQueryable());

        var featureService = new Mock<ICWFeatureService>();
        featureService.Setup(service => service.IsEnabledAsync(CWFeature.CreditNotes, StoreId)).ReturnsAsync(() => _creditNotesEnabled);

        _service = TestObject.Create<ExchangeService>();
        TestObject.Set(_service, "_returnReasonRuleRepository", reasonRuleRepository.Object);
        TestObject.Set(_service, "_returnActionRuleRepository", actionRuleRepository.Object);
        TestObject.Set(_service, "_cwFeatureService", featureService.Object);
    }

    [Test]
    public async Task Both_ways_are_offered_where_the_rules_allow_them()
    {
        (await _service.DetermineEnabledReturnModesAsync(StoreId, ReasonId, ActionId))
            .Should().BeEquivalentTo([ExchangeReturnMode.Exchange, ExchangeReturnMode.CreditNote]);
    }

    [Test]
    public async Task No_credit_return_is_offered_with_Credit_Notes_switched_off()
    {
        _creditNotesEnabled = false;

        (await _service.DetermineEnabledReturnModesAsync(StoreId, ReasonId, ActionId))
            .Should().BeEquivalentTo([ExchangeReturnMode.Exchange]);
    }
}
