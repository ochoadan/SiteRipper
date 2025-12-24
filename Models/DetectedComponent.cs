namespace SiteRipper.Models;

using SiteRipper.Detection;
using AngleSharp.Dom;

public class DetectedComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Type { get; set; } = "";              // "button", "card", "navigation", etc.
    public double Confidence { get; set; }              // 0.0 - 1.0
    public string Selector { get; set; } = "";
    public string? ElementId { get; set; }
    public List<string> Classes { get; set; } = new();
    public string? Text { get; set; }

    public StructuralFingerprint Fingerprint { get; set; } = new();
    public VisualProperties VisualProperties { get; set; } = new();

    // Hierarchy
    public string? ParentId { get; set; }
    public List<string> ChildIds { get; set; } = new();
    public List<string> Composition { get; set; } = new();  // Child component types
    public bool IsAtomic { get; set; }                      // Has no component children
    public bool IsCompound { get; set; }                    // Has component children

    // Reference to actual element (not serialized)
    [System.Text.Json.Serialization.JsonIgnore]
    public IElement? Element { get; set; }

    // Index in the DOM (for style/box lookup)
    public int DomIndex { get; set; }

    // Section this component belongs to
    public string? SectionId { get; set; }

    // Outer HTML for AI replication (truncated to 500 chars)
    public string? OuterHtml { get; set; }
}

public enum ComponentType
{
    Unknown,
    Button,
    Card,
    Navigation,
    Hero,
    Footer,
    Modal,
    Tabs,
    Accordion,
    Table,
    Form,
    Input,
    Badge,
    Avatar,
    Alert,
    Breadcrumb,
    Pagination,
    Tooltip,
    Dropdown,
    Grid,
    List
}
