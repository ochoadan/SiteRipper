namespace SiteRipper.Variants;

using SiteRipper.Models;
using Microsoft.Playwright;

public class StateDetector
{
    private static readonly string[] StylePropsToCheck = {
        "background-color", "color", "border-color", "box-shadow",
        "outline", "opacity", "transform", "cursor", "text-decoration"
    };

    public async Task<List<StateVariant>> DetectStates(
        IPage page,
        DetectedComponent component)
    {
        var states = new List<StateVariant>();
        var selector = component.Selector;

        // Always add default state
        states.Add(new StateVariant
        {
            State = ComponentState.Default,
            StyleChanges = new(),
            IsDetected = true
        });

        // Get default styles
        var defaultStyles = await GetStyles(page, selector);
        if (defaultStyles == null) return states;

        // Detect hover state
        var hoverChanges = await DetectHoverState(page, selector, defaultStyles);
        if (hoverChanges != null && hoverChanges.Count > 0)
        {
            states.Add(new StateVariant
            {
                State = ComponentState.Hover,
                StyleChanges = hoverChanges,
                IsDetected = true
            });
        }

        // Detect focus state (for interactive elements)
        if (await IsInteractive(page, selector))
        {
            var focusChanges = await DetectFocusState(page, selector, defaultStyles);
            if (focusChanges != null && focusChanges.Count > 0)
            {
                states.Add(new StateVariant
                {
                    State = ComponentState.Focus,
                    StyleChanges = focusChanges,
                    IsDetected = true
                });
            }
        }

        // Detect disabled state
        var disabledChanges = await DetectDisabledState(page, selector, defaultStyles);
        if (disabledChanges != null && disabledChanges.Count > 0)
        {
            states.Add(new StateVariant
            {
                State = ComponentState.Disabled,
                StyleChanges = disabledChanges,
                IsDetected = true
            });
        }

        return states;
    }

    private async Task<Dictionary<string, string>?> GetStyles(IPage page, string selector)
    {
        try
        {
            var json = await page.EvaluateAsync<string>($@"
                () => {{
                    const el = document.querySelector('{EscapeSelector(selector)}');
                    if (!el) return null;

                    const cs = getComputedStyle(el);
                    const props = {System.Text.Json.JsonSerializer.Serialize(StylePropsToCheck)};
                    const result = {{}};

                    props.forEach(p => {{
                        const v = cs.getPropertyValue(p);
                        if (v) result[p] = v;
                    }});

                    return JSON.stringify(result);
                }}
            ");

            if (string.IsNullOrEmpty(json)) return null;
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, string>?> DetectHoverState(
        IPage page,
        string selector,
        Dictionary<string, string> defaultStyles)
    {
        try
        {
            // Move to element to trigger hover
            var element = await page.QuerySelectorAsync(selector);
            if (element == null) return null;

            await element.HoverAsync();
            await page.WaitForTimeoutAsync(100);

            var hoverStyles = await GetStyles(page, selector);
            if (hoverStyles == null) return null;

            var changes = GetStyleDifferences(defaultStyles, hoverStyles);

            // Move away to reset
            await page.Mouse.MoveAsync(0, 0);
            await page.WaitForTimeoutAsync(50);

            return changes;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, string>?> DetectFocusState(
        IPage page,
        string selector,
        Dictionary<string, string> defaultStyles)
    {
        try
        {
            await page.FocusAsync(selector);
            await page.WaitForTimeoutAsync(100);

            var focusStyles = await GetStyles(page, selector);
            if (focusStyles == null) return null;

            var changes = GetStyleDifferences(defaultStyles, focusStyles);

            // Blur to reset
            await page.EvaluateAsync($"document.querySelector('{EscapeSelector(selector)}')?.blur()");
            await page.WaitForTimeoutAsync(50);

            return changes;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, string>?> DetectDisabledState(
        IPage page,
        string selector,
        Dictionary<string, string> defaultStyles)
    {
        try
        {
            // Look for disabled variants in the DOM
            var disabledSelectors = new[]
            {
                $"{selector}[disabled]",
                $"{selector}[aria-disabled='true']",
                $"{selector}.disabled",
                $"{selector}:disabled"
            };

            foreach (var disabledSelector in disabledSelectors)
            {
                try
                {
                    var disabledEl = await page.QuerySelectorAsync(disabledSelector);
                    if (disabledEl != null)
                    {
                        var disabledStyles = await GetStyles(page, disabledSelector);
                        if (disabledStyles != null)
                        {
                            return GetStyleDifferences(defaultStyles, disabledStyles);
                        }
                    }
                }
                catch
                {
                    // Try next selector
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> IsInteractive(IPage page, string selector)
    {
        try
        {
            return await page.EvaluateAsync<bool>($@"
                () => {{
                    const el = document.querySelector('{EscapeSelector(selector)}');
                    if (!el) return false;

                    const tag = el.tagName.toLowerCase();
                    const role = el.getAttribute('role');

                    return tag === 'button' ||
                           tag === 'a' ||
                           tag === 'input' ||
                           tag === 'select' ||
                           tag === 'textarea' ||
                           role === 'button' ||
                           role === 'link' ||
                           el.hasAttribute('tabindex');
                }}
            ");
        }
        catch
        {
            return false;
        }
    }

    private Dictionary<string, string> GetStyleDifferences(
        Dictionary<string, string> before,
        Dictionary<string, string> after)
    {
        var changes = new Dictionary<string, string>();

        foreach (var prop in StylePropsToCheck)
        {
            var beforeVal = before.GetValueOrDefault(prop, "");
            var afterVal = after.GetValueOrDefault(prop, "");

            if (!string.IsNullOrEmpty(afterVal) && beforeVal != afterVal)
            {
                changes[prop] = afterVal;
            }
        }

        return changes;
    }

    private string EscapeSelector(string selector)
    {
        return selector.Replace("'", "\\'").Replace("\"", "\\\"");
    }
}
