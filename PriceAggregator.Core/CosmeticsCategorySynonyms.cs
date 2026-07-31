namespace PriceAggregator.Core;

// Migros and MacroCenter name the same real cosmetics departments differently — Migros'
// section root is "Kişisel Bakım, Kozmetik, Sağlık", MacroCenter's is just "Kozmetik"; a level
// down, Migros says "Duş, Banyo, Sabun" where MacroCenter says "Duş & Banyo". Unlike Market
// (see ResolveMarketAsync's "same platform" comment), these breadcrumbs don't coincide on
// their own, and FuzzyMatcher's spelling-variant tolerance isn't enough to catch a real
// naming difference like these — so every shared department was showing up twice in the
// menu. Applied to Root/Parent/Category breadcrumb text in CategoryResolver.
// ResolveBreadcrumbOnlyAsync before it reaches FindOrCreateAsync.
//
// Deliberately conservative: only names specific/unambiguous enough that they mean the same
// thing regardless of which parent category they show up under (e.g. "Tıraş Makineleri" is
// always razors). Broader phrases like "Krem & Peeling" or "Koku Önleyici" were left out
// because their real meaning depends on the parent they were seen under this one time
// (foot care vs. face care) — hardcoding that pairing here would silently mismatch the next
// store that reuses the same generic phrase under a different department.
public static class CosmeticsCategorySynonyms
{
    private static readonly Dictionary<string, string> Map = BuildMap();

    public static string Canonicalize(string name)
    {
        var normalized = TurkishNormalizer.Normalize(name);
        return Map.TryGetValue(normalized, out var canonical) ? canonical : name;
    }

    private static Dictionary<string, string> BuildMap()
    {
        (string Source, string Canonical)[] pairs =
        [
            // Store section roots
            ("Kozmetik", "Kişisel Bakım, Kozmetik, Sağlık"),

            // Department level (children of the section root)
            ("Ağdalar & Tüy Dökücüler", "Ağda, Epilasyon"),
            ("Ağız Bakımı", "Ağız Bakım Ürünleri"),
            ("Ayak Bakımı", "Ayak Bakım"),
            ("Cinsel Sağlık", "Prezervatif, Jeller"),
            ("Duş & Banyo", "Duş, Banyo, Sabun"),
            ("El & Vücut Bakımı", "El ve Vücut Bakım"),
            ("Kadın Hijyen", "Hijyenik Ped"),
            ("Saç Bakımı & Şampuan", "Saç Bakım"),
            ("Saç Şekillendirici", "Saç Şekillendiriciler"),
            ("Saç Tarayıcıları", "Saç Tarayıcılar"),
            ("Sağlık & Hijyen", "Sağlık Ürünleri"),
            ("Tıraş Aletleri", "Tıraş Malzemeleri"),
            ("Tıraş Jeli, Köpük, Sabun", "Tıraş Malzemeleri"),
            ("Tıraş Sonrası Bakım", "Tıraş Sonrası Ürünler"),
            ("Tırnak", "Tırnak Ürünleri"),
            ("Yüz Bakımı", "Yüz Bakım"),

            // Leaf level — specific enough to be safe regardless of parent
            ("Kullan At Bıçaklar", "Kullan At Tıraş Bıçağı"),
            ("Tıraş Makineleri", "Tıraş Makinaları"),
            ("Tıraş Bıçakları", "Tıraş Bıçağı, Yedekleri"),
            ("Tıraş Köpükleri", "Tıraş Köpüğü"),
            ("Tıraş Sabunları", "Tıraş Sabunu"),
            ("Tıraş Jelleri", "Tıraş Jeli"),
            ("Tıraş Losyonları", "Tıraş Losyonu"),
            ("Ağda Bantları", "Ağda Bandı"),
            ("Ağda Makinası", "Ağda Makinesi"),
            ("Vazelinler", "Vazelin"),
            ("Saç Fırçaları", "Saç Fırçası"),
            ("Takviye Edici Gıdalar", "Takviye Edici Gıda"),
            ("Kayganlaştırıcı Jel", "Kayganlaştırıcı"),
            ("El & Vücut Kremleri", "El ve Vücut Kremi"),
            ("Manikür & Pedikür", "Tırnak Bakım Ürünleri"),
            ("Dudak Koruyucuları", "Dudak Bakımı"),
            ("Yüz Bakım Maskesi", "Yüz Maskesi"),
            ("Gece Bakım Kremleri", "Gece Kremi"),
            ("Günlük Bakım Kremi", "Gündüz Kremi"),
            ("Temizleme Tonikleri & Suları", "Yüz Toniği"),
        ];

        var map = new Dictionary<string, string>();
        foreach (var (source, canonical) in pairs)
            map[TurkishNormalizer.Normalize(source)] = canonical;
        return map;
    }
}
