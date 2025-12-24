namespace SiteRipper.Detection;

using SiteRipper.Models;

public static class ComponentTypeRules
{
    public static readonly List<ComponentTypeRule> Rules = new()
    {
        // BUTTON: Small, interactive, minimal children, MUST have distinct background
        new ComponentTypeRule
        {
            ComponentType = "button",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // REQUIRED: Must have distinct background (buttons need visual presence)
                if (!vis.HasDistinctBackground && !vis.HasBorder) return 0;
                score += 0.25;

                // Size constraints
                if (vis.Width > 30 && vis.Width < 400 && vis.Height > 20 && vis.Height < 80)
                    score += 0.25;

                // Minimal children (buttons are leaf-ish)
                if (fp.ChildCount <= 3 && fp.InteractiveCount == 0)
                    score += 0.2;

                // Visual polish
                if (vis.HasBorderRadius) score += 0.1;
                if (vis.HasShadow) score += 0.05;

                // Text content (buttons usually have text)
                if (fp.TextNodeCount > 0 && fp.TextNodeCount <= 2)
                    score += 0.15;

                return score;
            }
        },

        // CARD: Container with image + heading + text, shadow/border
        new ComponentTypeRule
        {
            ComponentType = "card",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // REQUIRED: Must have BOTH image AND heading to be a card
                if (!fp.HasImage || !fp.HasHeading) return 0;
                score += 0.35;

                // Has text content
                if (fp.TextNodeCount > 0) score += 0.15;

                // Optional button/link (CTA)
                if (fp.HasLink || fp.HasButton) score += 0.1;

                // Visual container (shadow or border)
                if (vis.HasShadow || vis.HasBorder) score += 0.2;
                if (vis.HasBorderRadius) score += 0.05;

                // Reasonable child count
                if (fp.ChildCount >= 3 && fp.ChildCount <= 15)
                    score += 0.15;

                return score;
            }
        },

        // NAVIGATION: Horizontal list of links at top
        new ComponentTypeRule
        {
            ComponentType = "navigation",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // REQUIRED: Multiple links
                if (!fp.HasLink || fp.InteractiveCount < 3) return 0;
                score += 0.3;

                // Position at top (stricter check)
                if (vis.BoundingBox.Y < 150)
                    score += 0.25;
                else if (vis.BoundingBox.Y < 300)
                    score += 0.1;
                else
                    return 0; // Not navigation if too far down

                // Has list structure or flex display
                if (fp.HasList || vis.Display == "flex")
                    score += 0.15;

                // May have logo (image)
                if (fp.HasImage) score += 0.1;

                // Horizontal layout
                if (vis.FlexDirection == "row" || vis.Width > vis.Height * 3)
                    score += 0.15;

                // Spans most of viewport width
                if (vis.Width > 800)
                    score += 0.05;

                return score;
            }
        },

        // HERO: Large section with h1, usually first major section
        new ComponentTypeRule
        {
            ComponentType = "hero",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Must be near top
                if (vis.BoundingBox.Y < 600)
                    score += 0.2;

                // Large height
                if (vis.Height > 300)
                    score += 0.2;

                // Has heading (preferably h1)
                if (fp.HasHeading) score += 0.25;

                // Full width
                if (vis.Width > 900)
                    score += 0.1;

                // Has CTA button
                if (fp.HasButton) score += 0.15;

                // Has some text
                if (fp.TextNodeCount > 0) score += 0.1;

                return score;
            }
        },

        // FOOTER: At bottom, has links, copyright-ish content
        new ComponentTypeRule
        {
            ComponentType = "footer",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // At bottom (Y > 1000 typically)
                if (vis.BoundingBox.Y > 800)
                    score += 0.3;

                // Full width
                if (vis.Width > 900)
                    score += 0.15;

                // Has links
                if (fp.HasLink && fp.InteractiveCount >= 2)
                    score += 0.25;

                // Has list or multiple sections
                if (fp.HasList || fp.ChildCount >= 3)
                    score += 0.15;

                // Has text (copyright, etc)
                if (fp.TextNodeCount > 0) score += 0.15;

                return score;
            }
        },

        // MODAL: Fixed position, high z-index, centered
        new ComponentTypeRule
        {
            ComponentType = "modal",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Fixed or absolute position
                if (vis.Position == "fixed" || vis.Position == "absolute")
                    score += 0.3;

                // High z-index
                if (vis.ParsedZIndex > 100)
                    score += 0.2;

                // Centered
                if (vis.IsCentered)
                    score += 0.15;

                // Has close button
                if (fp.HasButton)
                    score += 0.15;

                // Has shadow (overlay effect)
                if (vis.HasShadow)
                    score += 0.1;

                // Reasonable size
                if (vis.Width > 200 && vis.Height > 150 && vis.Width < 800)
                    score += 0.1;

                return score;
            }
        },

        // TABS: Horizontal list of clickables with one active
        new ComponentTypeRule
        {
            ComponentType = "tabs",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Multiple interactive elements
                if (fp.InteractiveCount >= 2 && fp.InteractiveCount <= 10)
                    score += 0.3;

                // Horizontal layout
                if (vis.FlexDirection == "row" || vis.Display == "flex")
                    score += 0.2;

                // Has list structure
                if (fp.HasList)
                    score += 0.15;

                // Relatively flat (not tall)
                if (vis.Width > vis.Height * 2)
                    score += 0.2;

                // Has links or buttons
                if (fp.HasLink || fp.HasButton)
                    score += 0.15;

                return score;
            }
        },

        // ACCORDION: Stacked sections with toggle headers
        new ComponentTypeRule
        {
            ComponentType = "accordion",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Multiple children (sections)
                if (fp.ChildCount >= 2 && fp.ChildCount <= 20)
                    score += 0.25;

                // Has headings
                if (fp.HasHeading)
                    score += 0.2;

                // Has interactive elements (toggles)
                if (fp.InteractiveCount >= 1)
                    score += 0.2;

                // Vertical layout (taller than wide)
                if (vis.Height > vis.Width * 0.5 || vis.FlexDirection == "column")
                    score += 0.2;

                // Has text content
                if (fp.TextNodeCount > 0)
                    score += 0.15;

                return score;
            }
        },

        // TABLE: Grid of cells, has rows
        new ComponentTypeRule
        {
            ComponentType = "table",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Has table tag
                if (fp.HasTable)
                    score += 0.6;

                // Grid display
                if (vis.Display == "table" || vis.Display == "grid")
                    score += 0.2;

                // Multiple children
                if (fp.ChildCount >= 3)
                    score += 0.1;

                // Wide element
                if (vis.Width > 400)
                    score += 0.1;

                return score;
            }
        },

        // FORM: Has inputs, labels, submit button
        new ComponentTypeRule
        {
            ComponentType = "form",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Has form elements
                if (fp.HasForm)
                    score += 0.4;

                // Has button (submit)
                if (fp.HasButton)
                    score += 0.2;

                // Multiple children (fields)
                if (fp.ChildCount >= 2)
                    score += 0.15;

                // Vertical layout typically
                if (vis.Height > 100)
                    score += 0.1;

                // Has text (labels)
                if (fp.TextNodeCount > 0)
                    score += 0.15;

                return score;
            }
        },

        // BADGE: Very small, inline, distinct background
        new ComponentTypeRule
        {
            ComponentType = "badge",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // REQUIRED: Must be very small (stricter size check)
                if (vis.Width >= 120 || vis.Height >= 35) return 0;
                score += 0.35;

                // Has border radius (pill-like)
                if (vis.HasBorderRadius && vis.ParsedBorderRadius > 4)
                    score += 0.25;

                // Distinct background
                if (vis.HasDistinctBackground)
                    score += 0.2;

                // Minimal children (badges are simple)
                if (fp.ChildCount <= 1)
                    score += 0.15;

                // Has text
                if (fp.TextNodeCount > 0)
                    score += 0.05;

                return score;
            }
        },

        // AVATAR: Small, round image
        new ComponentTypeRule
        {
            ComponentType = "avatar",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // REQUIRED: Must be roughly round (border-radius >= 50% of width)
                if (!vis.HasBorderRadius || vis.ParsedBorderRadius < vis.Width / 2 * 0.9)
                    return 0;
                score += 0.35;

                // REQUIRED: Small and roughly square
                if (vis.Width >= 100 || vis.Height >= 100) return 0;
                if (Math.Abs(vis.Width - vis.Height) >= 15) return 0;
                score += 0.3;

                // Has image (avatars usually do)
                if (fp.HasImage)
                    score += 0.25;

                // Minimal children
                if (fp.ChildCount <= 2)
                    score += 0.1;

                return score;
            }
        },

        // ALERT: Distinct background, may have icon and close
        new ComponentTypeRule
        {
            ComponentType = "alert",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Has distinct background
                if (vis.HasDistinctBackground)
                    score += 0.25;

                // Has border or border-radius
                if (vis.HasBorder || vis.HasBorderRadius)
                    score += 0.15;

                // Has text
                if (fp.TextNodeCount > 0)
                    score += 0.2;

                // May have close button
                if (fp.HasButton)
                    score += 0.15;

                // Reasonable width (usually full-width or near)
                if (vis.Width > 200)
                    score += 0.1;

                // Not too tall
                if (vis.Height < 200)
                    score += 0.15;

                return score;
            }
        },

        // INPUT: Single form field
        new ComponentTypeRule
        {
            ComponentType = "input",
            Calculate = (fp, vis) =>
            {
                double score = 0;

                // Has form element
                if (fp.HasForm)
                    score += 0.4;

                // Small height
                if (vis.Height < 60 && vis.Height > 20)
                    score += 0.25;

                // Has border
                if (vis.HasBorder)
                    score += 0.2;

                // Minimal children
                if (fp.ChildCount <= 2)
                    score += 0.15;

                return score;
            }
        }
    };
}

public class ComponentTypeRule
{
    public string ComponentType { get; set; } = "";
    public Func<StructuralFingerprint, VisualProperties, double> Calculate { get; set; } = (_, _) => 0;
}
