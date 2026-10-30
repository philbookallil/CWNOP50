using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Helpers;
using System.Text.RegularExpressions;

namespace Nop.Plugin.CW.Core.Extensions;

/// <summary>
/// HTML helper extensions for rendering CW SVG sprite icons.
/// </summary>
public static class CWIconHtmlHelperExtensions
{
    private const string SvgSpritePath = "/Plugins/CW.Core/Content/icons/cw-icons.svg";
    private const string PgSvgSpritePath = "/Plugins/CW.Core/Content/icons/pg-icons.svg";

    /// <summary>
    /// Renders a single CW SVG icon.
    /// </summary>
    /// <param name="helper">The IHtmlHelper.</param>
    /// <param name="iconId">The symbol ID in cw-icons.svg (e.g. "cw-icon-truck").</param>
    /// <param name="extraClass">Optional additional CSS class(es) (e.g. "cw-icon--sm").</param>
    public static IHtmlContent CWIcon(this IHtmlHelper helper, string iconId, string? extraClass = null)
    {
        var classes = string.IsNullOrWhiteSpace(extraClass)
            ? "cw-icon"
            : $"cw-icon {extraClass}";

        return new HtmlString(
            $"""<svg class="{classes}" aria-hidden="true"><use href="{SvgSpritePath}#{iconId}"/></svg>""");
    }

    /// <summary>
    /// Renders the three-state PackName type icon container (group / customer / no-customer).
    /// Visibility is controlled by adding one of these classes to the container div:
    /// "packname-group", "packname-customer", or "packname-no-customer".
    /// </summary>
    /// <param name="helper">The IHtmlHelper.</param>
    /// <param name="activeClass">The packname type class on the container (e.g. "packname-group").</param>
    /// <param name="containerCssClass">Additional CSS class(es) for the outer div.</param>
    /// <param name="style">Optional inline style attribute value (e.g. "color: #ff0000").</param>
    public static IHtmlContent PackNameTypeIcon(
        this IHtmlHelper helper,
        string activeClass,
        string? containerCssClass = null,
        string? style = null)
    {
        var classAttr = string.IsNullOrWhiteSpace(containerCssClass)
            ? activeClass
            : $"{activeClass} {containerCssClass}";

        var styleAttr = string.IsNullOrWhiteSpace(style) ? "" : $""" style="{style}" """;

        return new HtmlString($"""
            <div class="{classAttr}"{styleAttr}>
                <svg class="packname-group cw-icon" aria-hidden="true"><use href="{SvgSpritePath}#cw-icon-group"/></svg>
                <svg class="packname-customer cw-icon" aria-hidden="true"><use href="{SvgSpritePath}#cw-icon-customer"/></svg>
                <svg class="packname-no-customer cw-icon" aria-hidden="true"><use href="{SvgSpritePath}#cw-icon-person"/></svg>
            </div>
            """);
    }

    /// <summary>
    /// Returns the raw JS string literals for the three PackName SVG icons.
    /// Use these in Razor-embedded JavaScript where you need to inject the icons dynamically.
    /// </summary>
    public static (string Group, string Customer, string NoCustomer) PackNameIconJsStrings(this IHtmlHelper helper)
    {
        return (
            Group: $"""'<svg class="packname-group cw-icon" aria-hidden="true"><use href="{SvgSpritePath}#cw-icon-group"/></svg>'""",
            Customer: $"""'<svg class="packname-customer cw-icon" aria-hidden="true"><use href="{SvgSpritePath}#cw-icon-customer"/></svg>'""",
            NoCustomer: $"""'<svg class="packname-no-customer cw-icon" aria-hidden="true"><use href="{SvgSpritePath}#cw-icon-person"/></svg>'"""
        );
    }

    /// <summary>
    /// Renders a product group SVG icon from the pg-icons.svg sprite.
    /// Returns empty when <paramref name="iconId"/> is null or empty (no icon assigned).
    /// </summary>
    /// <param name="helper">The IHtmlHelper.</param>
    /// <param name="iconId">Symbol ID in pg-icons.svg (e.g. "pg-icon-Shirts").</param>
    /// <param name="extraClass">Optional additional CSS class(es) beyond "pg-icon".</param>
    /// <param name="color">Optional inline color value (e.g. "#3a7bd5"). Uses fill:currentColor if omitted.</param>
    public static IHtmlContent PgIcon(this IHtmlHelper helper, string? iconId, string? extraClass = null, string? color = null)
    {
        //the icon and colour are typed in the admin product group form: an icon is a symbol id of the sprite and a
        //colour a hex colour, and anything else is left out rather than written into the page
        if (string.IsNullOrWhiteSpace(iconId) || !Regex.IsMatch(iconId, "^[A-Za-z0-9_-]+$"))
            return HtmlString.Empty;

        var classes = string.IsNullOrWhiteSpace(extraClass)
            ? "pg-icon"
            : $"pg-icon {extraClass}";

        var hexColour = PackNameColourHelper.Normalize(color);
        var styleAttr = hexColour == null ? "" : $""" style="color:{hexColour}" """;

        return new HtmlString(
            $"""<svg class="{classes}" aria-hidden="true"{styleAttr}><use href="{PgSvgSpritePath}#{iconId}"/></svg>""");
    }
}
