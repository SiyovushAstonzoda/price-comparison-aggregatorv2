namespace PriceAggregator.Core;

// Maps ANY furniture-sector category signal — a real store's leaf/parent/root category
// name, or (as a last resort) the product title — onto ONE shared canonical bucket.
// This exists because Evidea/İkea/Mion each organize their own catalog completely
// differently (İkea: "Mobilyalar > Masalar > Çalışma Masaları"; Evidea: "Tamamlayıcı
// Mobilya > Çalışma Odası > Çalışma Sandalyesi"; Mion: Migros-style categories) — trusting
// each store's own hierarchy literally (as CategoryResolver briefly did) produced a dozen
// separate top-level "furniture" roots with the same real product types (tables, chairs,
// storage...) duplicated across them. Routing every source through this same mapper first
// is what keeps "Masa"/"Masalar"/"Çalışma Masası" all landing under one "Masa" node instead.
public static class FurnitureCategoryClassifier
{
    // Furniture-sector sources (Evidea in particular) also carry non-furniture home
    // essentials — soap, cosmetics, small appliances — that don't belong under a furniture
    // department at all. Named to exactly match the real category Market already has
    // (see CategoryResolver.ResolveMobilyaAsync) so these get filed there instead of
    // spawning their own confusing "Kişisel Bakım" tab inside the furniture menu.
    public static readonly HashSet<string> MarketCrossoverBuckets = new()
    {
        "Kişisel Bakım",
        "Elektrikli Ev Aletleri",
    };

    private static readonly (string Name, string[] Keywords, string[] Exclude)[] Rules =
    [
        // Checked first since e.g. "nasır yastığı" (a foot-care corn pad) would otherwise
        // false-match "Yastık ve Tekstil" on "yastık", and "koltuk altı" (underarm, as in
        // deodorant/wax-strip titles) would otherwise false-match "Koltuk" on "koltuk". See
        // MarketCrossoverBuckets above — these two aren't real furniture buckets.
        ("Kişisel Bakım", ["sabun", "allik", "nasir", "deodorant", "agda"], []),
        // "firin" here (not a furniture word at all) catches ovens the same way "supurge"
        // catches vacuums — both are small appliances a furniture-sector source (Evidea)
        // also carries, that would otherwise fall through to Title-based guessing and, since
        // an oven's own "Lambalı" (has-a-lamp) spec matches "Aydınlatma"'s "lamba" keyword,
        // get misfiled as a lighting fixture.
        ("Elektrikli Ev Aletleri", ["supurge", "firin"], []),
        // Checked before "Dolap ve Depolama" — "ayakkabı dolabı" would otherwise match "dolap".
        ("Ayakkabılık", ["ayakkabi"], []),
        // Textile/cushion signals are checked early because they modify a furniture word
        // rather than naming the furniture itself — "Koltuk Örtüsü" (sofa throw) and
        // "Sandalye Minderi" (chair cushion) are textile products, not the koltuk/sandalye
        // itself, so they must be caught before those rules below.
        ("Yastık ve Tekstil", ["yastik", "yastig", "kirlent", "battaniye", "minder", "nevresim", "yorgan", "ortu"], []),
        // Checked before "Koltuk" — "Koltuk Sehpası" (a coffee/side table next to a sofa)
        // is a table, not seating.
        ("Sehpa", ["sehpa"], []),
        ("Koltuk", ["koltuk", "koltug", "kanepe", "berjer", "divan", "sedir"], []),
        ("Sandalye ve Tabure", ["sandalye", "tabure"], []),
        // "ütü" (the iron itself, or an "ütü masası" ironing board) gets its own bucket —
        // it's neither a small appliance nor a real dining/work table, and used to have no
        // home at all (just excluded from "Masa" below, which left it uncategorized).
        ("Ütü ve Ütü Masası", ["utu"], []),
        // "masaj" (massage) contains a near-match for "masa" once suffixed — excluded so
        // it doesn't get filed as a table (a "masaj koltuğu" is already caught by "Koltuk"
        // above; this only matters for standalone masaj products with no koltuk/yatak word).
        ("Masa", ["masa", "bistro"], ["masaj"]),
        ("Yatak", ["yatak", "yatag"], []),
        ("Dolap ve Depolama", ["dolap", "dolab", "gardirop", "gardrop", "bufe", "konsol", "keson", "depolama"], []),
        ("Raf ve Kitaplık", ["raf", "kitaplik"], []),
        ("Aydınlatma", ["lamba", "ampul", "aydinlat", "led", "avize", "aplik", "spot"], []),
        ("Halı", ["hali", "yolluk", "kilim", "post"], []),
        // Generic "aksesuar" is a catch-all, so it's both last in this list and the last rule.
        // Excluded when "sac" (hair) is also present — a furniture-sector source's "Saç
        // Aksesuarları" (hair accessories: toka/firkete/tarak) breadcrumb otherwise matches
        // this same generic word and files a hair clip as a home-decor accessory.
        ("Dekoratif Aksesuar", ["ayna", "kulp", "dekor", "obje", "aksesuar"], ["sac"]),
    ];

    public static string? Classify(string text)
    {
        var tokens = Tokenize(text);
        if (tokens.Count == 0) return null;

        foreach (var (name, keywords, exclude) in Rules)
        {
            if (exclude.Any(ex => tokens.Any(token => TokenMatchesKeyword(token, ex)))) continue;

            if (keywords.Any(keyword => tokens.Any(token => TokenMatchesKeyword(token, keyword))))
                return name;
        }

        return null;
    }

    private static bool TokenMatchesKeyword(string token, string keyword)
    {
        if (token == keyword) return true;

        // Category names are controlled vocabulary (unlike noisy product titles), so a
        // wider suffix tolerance is safe here — it's what's needed to reach Turkish's
        // stacked plural+possessive suffix ("-ları"/"-leri", e.g. "sandalyeleri"), which
        // runs 4 characters past the bare root instead of the usual 3.
        return token.Length > keyword.Length
            && token.StartsWith(keyword, StringComparison.Ordinal)
            && (token.Length - keyword.Length) <= 4;
    }

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var normalized = TurkishNormalizer.Normalize(text);
        var cleaned = new string(normalized.Select(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());

        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
