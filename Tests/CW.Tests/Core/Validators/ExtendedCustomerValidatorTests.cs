using AwesomeAssertions;
using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Customers;
using NUnit.Framework;

namespace CW.Tests.Core.Validators;

/// <summary>
/// The admin customer form posts CW's model, and nop finds a validator by the posted model's exact type: nop's customer
/// rules reach the form only through the plugin's validator for that type. The rules themselves are nop's (its
/// validator reads the database schema when it is built, so it is exercised on the site, not here)
/// </summary>
[TestFixture]
public class ExtendedCustomerValidatorTests
{
    [Test]
    public void The_plugin_has_a_validator_for_the_model_the_customer_form_posts()
    {
        typeof(ExtendedCustomerValidator).Should().BeAssignableTo<IValidator<ExtendedCustomerModel>>();
    }
}
