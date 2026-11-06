using AwesomeAssertions;
using Nop.Plugin.CW.ThemeCore.Domain;
using Nop.Plugin.CW.ThemeCore.Services;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// The per-store custom CSS is written to a file every storefront page of the store loads. The sanitizer always removes
/// what can break out of CSS or run script, and applies the store's url() policy to what can load another resource.
/// </summary>
[TestFixture]
public class CwCssSanitizerTests
{
    #region Utilities

    private static CwCssSanitizeResult Sanitize(string css, CustomCssUrlPolicy policy = CustomCssUrlPolicy.Allow)
        => new CwCssSanitizer().Sanitize(css, policy);

    #endregion

    #region Plain CSS

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Empty_input_gives_empty_css_and_no_warning(string? css)
    {
        var result = Sanitize(css!);

        result.Css.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void Ordinary_css_is_kept_trimmed_and_without_warnings()
    {
        var result = Sanitize("  .header { color: #fff; background: var(--cw-primary); }  ");

        result.Css.Should().Be(".header { color: #fff; background: var(--cw-primary); }");
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void Css_over_the_size_cap_is_truncated_with_a_warning()
    {
        var result = Sanitize(new string('a', 100_010));

        result.Css.Length.Should().Be(100_000);
        result.Warnings.Should().ContainSingle().Which.Should().Contain("truncated");
    }

    #endregion

    #region Always blocked

    [Test]
    public void Angle_brackets_are_removed_so_the_css_cannot_close_its_style_element()
    {
        var result = Sanitize("a{}</style><script>alert(1)</script>");

        result.Css.Should().NotContain("<").And.NotContain(">");
        result.Warnings.Should().ContainSingle();
    }

    [TestCase("@import url('https://evil.example/x.css');")]
    [TestCase("@IMPORT 'x.css';")]
    public void Import_rules_are_removed(string css)
    {
        var result = Sanitize(css + " .a{color:red}");

        result.Css.Should().NotContainEquivalentOf("@import");
        result.Css.Should().Contain(".a{color:red}");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("@import");
    }

    [Test]
    public void Expression_is_removed()
    {
        var result = Sanitize(".a{width:expression(alert(1))}");

        result.Css.Should().NotContainEquivalentOf("expression(");
        result.Warnings.Should().ContainSingle();
    }

    [TestCase("javascript:")]
    [TestCase("JavaScript :")]
    [TestCase("vbscript:")]
    public void Script_uris_are_removed(string scheme)
    {
        var result = Sanitize($".a{{background:url({scheme}alert(1))}}");

        result.Css.Should().NotContainEquivalentOf("script:");
        result.Css.Should().NotContainEquivalentOf("script :");
    }

    [TestCase(".a{behavior:url(x.htc);color:red}")]
    [TestCase(".a{-moz-binding:url(x.xml#b);color:red}")]
    public void Behavior_and_moz_binding_are_removed(string css)
    {
        var result = Sanitize(css);

        result.Css.Should().NotContain("behavior").And.NotContain("-moz-binding");
        result.Css.Should().Contain("color:red");
    }

    [Test]
    public void Each_kind_of_removal_adds_its_own_warning()
    {
        var result = Sanitize("<b> @import 'x'; .a{width:expression(1);background:url(javascript:x);behavior:x;}");

        result.Warnings.Should().HaveCount(5);
    }

    #endregion

    #region url() policy

    private const string MixedUrls =
        ".a{background:url('https://cdn.example/a.png')}" +
        ".b{background:url(//cdn.example/b.png)}" +
        ".c{background:url(\"/images/c.png\")}" +
        ".d{background:url(data:image/png;base64,AAAA)}";

    [Test]
    public void Allow_keeps_every_url()
    {
        var result = Sanitize(MixedUrls, CustomCssUrlPolicy.Allow);

        result.Css.Should().Be(MixedUrls);
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void Data_and_same_origin_only_removes_external_urls_and_keeps_the_rest()
    {
        var result = Sanitize(MixedUrls, CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().NotContain("cdn.example");
        result.Css.Should().Contain("url(\"/images/c.png\")");
        result.Css.Should().Contain("url(data:image/png;base64,AAAA)");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("same-origin");
    }

    [Test]
    public void Data_and_same_origin_only_gives_no_warning_when_nothing_is_external()
    {
        var result = Sanitize(".c{background:url(/images/c.png)}", CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().Contain("url(/images/c.png)");
        result.Warnings.Should().BeEmpty();
    }

    [TestCase("url(HTTPS://cdn.example/a.png)")]
    [TestCase("url( 'http://cdn.example/a.png' )")]
    public void External_url_detection_ignores_case_and_spacing(string url)
    {
        var result = Sanitize($".a{{background:{url}}}", CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().NotContain("cdn.example");
    }

    [Test]
    public void Block_removes_every_url()
    {
        var result = Sanitize(MixedUrls, CustomCssUrlPolicy.Block);

        result.Css.Should().NotContainEquivalentOf("url(");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("Block");
    }

    #endregion

    #region What the browser decodes

    //CSS lets any character be written as a backslash escape ("\68" is "h", "\69" is "i"), and the browser decodes it
    //before it acts on a keyword or a URL; the URL parser then drops tabs and newlines and reads '\' as '/'. The checks
    //must read the CSS the same way.

    [Test]
    public void Escaped_external_url_is_removed_under_data_and_same_origin_only()
    {
        var result = Sanitize(@".a{background:url(\68ttps://evil.example/x.png)}", CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().NotContain("evil.example");
    }

    [Test]
    public void Escaped_import_is_removed()
    {
        var result = Sanitize(@"@\69mport 'https://evil.example/x.css'; .a{color:red}");

        result.Css.Should().NotContain("evil.example");
        result.Css.Should().Contain(".a{color:red}");
    }

    [Test]
    public void Url_written_with_an_escaped_function_name_is_removed_under_block()
    {
        var result = Sanitize(@".a{background:\75rl(https://evil.example/x.png)}", CustomCssUrlPolicy.Block);

        result.Css.Should().NotContain("evil.example");
    }

    [TestCase(@".a{background:url(""https://evil.example/a)b.png"")}")]
    [TestCase(@".a{background:url('https://evil.example/a\'b.png')}")]
    [TestCase(@".a{background:url(""\68ttps://evil.example/x.png"")}")]
    [TestCase(@".a{background:url(https\3a //evil.example/x.png)}")]
    [TestCase(@".a{background:url(""ht\9 tps://evil.example/x.png"")}")]
    [TestCase(@".a{background:url(""\\\\evil.example/x.png"")}")]
    [TestCase(@".a{background:url(\5c\5c evil.example/x.png)}")]
    [TestCase(@".a{background:url(""ftp://evil.example/x.png"")}")]
    [TestCase(@".a{background:url(""  //evil.example/x.png"")}")]
    public void External_urls_in_any_spelling_are_removed_under_data_and_same_origin_only(string css)
    {
        var result = Sanitize(css, CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().NotContain("evil.example");
        result.Warnings.Should().ContainSingle();
    }

    [TestCase(@".a{background:url(""/img/a)b.png"")}")]
    [TestCase(@".a{background:url(img/\61 .png)}")]
    [TestCase(@".a{background:url(""\\img/x.png"")}")]
    [TestCase(@".a{background:url(""data:image/svg+xml,%3Csvg%3E"")}")]
    public void Same_site_and_data_urls_in_any_spelling_are_kept(string css)
    {
        var result = Sanitize(css, CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().Contain("url(");
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void Escaped_colon_after_javascript_is_removed()
    {
        var result = Sanitize(@".a{background:url(javascript\3a alert(1))}");

        result.Css.Should().NotContainEquivalentOf("javascript");
    }

    #endregion

    #region image-set() and image()

    [TestCase(".a{background-image:image-set(\"https://evil.example/x.png\" 1x)}")]
    [TestCase(".a{background-image:-webkit-image-set(\"https://evil.example/x.png\" 1x)}")]
    [TestCase(".a{background-image:image(\"https://evil.example/x.png\")}")]
    [TestCase(".a{background-image:image-set(url(/a.png) 1x, \"/b.png\" 2x)}")]
    public void Image_functions_are_removed_under_block(string css)
    {
        var result = Sanitize(css, CustomCssUrlPolicy.Block);

        result.Css.Should().Be(".a{background-image:}");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("Block");
    }

    [Test]
    public void Image_set_with_an_external_string_is_removed_under_data_and_same_origin_only()
    {
        var result = Sanitize(".a{background-image:image-set(\"/a.png\" 1x, \"https://evil.example/x.png\" 2x)}",
            CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().NotContain("evil.example").And.NotContain("image-set");
    }

    [Test]
    public void Image_set_of_same_site_images_is_kept_under_data_and_same_origin_only()
    {
        const string css = ".a{background-image:image-set(\"/a.png\" 1x, url(/b.png) 2x)}";

        var result = Sanitize(css, CustomCssUrlPolicy.DataAndSameOriginOnly);

        result.Css.Should().Be(css);
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void An_unclosed_image_set_is_removed_up_to_the_end_of_its_declaration()
    {
        var result = Sanitize(".a{background-image:image-set(\"https://evil.example/x.png\" 1x;color:red}",
            CustomCssUrlPolicy.Block);

        result.Css.Should().Be(".a{background-image:;color:red}");
    }

    [Test]
    public void Background_image_property_is_not_mistaken_for_image_function()
    {
        const string css = ".a{background-image:linear-gradient(red, blue)}";

        Sanitize(css, CustomCssUrlPolicy.Block).Css.Should().Be(css);
    }

    #endregion

    #region Escapes that must survive

    [Test]
    public void Escaped_letters_are_written_as_letters()
    {
        Sanitize(@".\62 tn{color:red}").Css.Should().Be(".btn{color:red}");
    }

    [Test]
    public void Icon_font_code_points_are_kept()
    {
        var result = Sanitize(@".icon:before{content:""\f101""}");

        result.Css.Should().Contain(@"\f101");
        result.Warnings.Should().BeEmpty();
    }

    [Test]
    public void Escaped_digit_and_colon_in_selectors_are_kept_escaped()
    {
        var result = Sanitize(@".\31 0col{width:10%} .md\:flex{display:flex}");

        result.Css.Should().Be(@".\31 0col{width:10%} .md\:flex{display:flex}");
    }

    [Test]
    public void A_kept_hex_escape_never_runs_into_a_decoded_letter()
    {
        //\31 then an escaped "a": decoding the "a" next to "\31" would read as the single escape \31a
        Sanitize(@".\31\61{color:red}").Css.Should().Be(@".\31 a{color:red}");
    }

    #endregion
}
