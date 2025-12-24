namespace SiteRipper.Detection;

public class StructuralFingerprint
{
    public string TagSignature { get; set; } = "";      // "div[img,h3,p,a]"
    public int ChildCount { get; set; }
    public int Depth { get; set; }
    public int TextNodeCount { get; set; }
    public int InteractiveCount { get; set; }           // buttons + links + inputs
    public int MediaCount { get; set; }                 // img, video, svg, canvas

    // Semantic indicators
    public bool HasHeading { get; set; }                // h1-h6
    public bool HasImage { get; set; }                  // img, svg, picture
    public bool HasLink { get; set; }                   // a[href]
    public bool HasButton { get; set; }                 // button, [role=button]
    public bool HasForm { get; set; }                   // form, input, textarea, select
    public bool HasList { get; set; }                   // ul, ol, dl
    public bool HasTable { get; set; }                  // table

    // Hash for fast comparison
    public string Hash { get; set; } = "";

    public string ComputeHash()
    {
        var parts = new List<string>
        {
            TagSignature,
            ChildCount.ToString(),
            InteractiveCount.ToString(),
            MediaCount.ToString(),
            HasHeading ? "H" : "",
            HasImage ? "I" : "",
            HasLink ? "L" : "",
            HasButton ? "B" : "",
            HasForm ? "F" : "",
            HasList ? "U" : "",
            HasTable ? "T" : ""
        };
        Hash = string.Join("|", parts.Where(p => !string.IsNullOrEmpty(p)));
        return Hash;
    }

    public double SimilarityTo(StructuralFingerprint other)
    {
        double score = 0;
        double total = 0;

        // Tag signature similarity
        total += 3;
        if (TagSignature == other.TagSignature) score += 3;
        else if (TagSignature.StartsWith(other.TagSignature.Split('[')[0])) score += 1;

        // Child count similarity
        total += 1;
        var childDiff = Math.Abs(ChildCount - other.ChildCount);
        if (childDiff == 0) score += 1;
        else if (childDiff <= 2) score += 0.5;

        // Semantic indicators (1 point each)
        total += 7;
        if (HasHeading == other.HasHeading) score += 1;
        if (HasImage == other.HasImage) score += 1;
        if (HasLink == other.HasLink) score += 1;
        if (HasButton == other.HasButton) score += 1;
        if (HasForm == other.HasForm) score += 1;
        if (HasList == other.HasList) score += 1;
        if (HasTable == other.HasTable) score += 1;

        return score / total;
    }
}
