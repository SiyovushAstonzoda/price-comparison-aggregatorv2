import { useEffect, useMemo, useState } from "react";
import { formatPrice, getMobilyaFilters, getMobilyaProducts } from "../api";
import type { MobilyaFilterState, MobilyaFilters, MobilyaProduct } from "../types";

const defaultFilters: MobilyaFilterState = {
  q: "",
  brand: "",
  category: "",
  midCategory: "",
  subCategory: "",
  color: "",
  dimensions: "",
  productType: "",
  material: "",
  sort: "name",
  minPrice: "",
  maxPrice: "",
};

interface MobilyaSectionProps {
  searchQuery: string;
}

function buildParams(filters: MobilyaFilterState) {
  const params = new URLSearchParams();
  if (filters.q) params.set("q", filters.q);
  if (filters.brand) params.set("brand", filters.brand);
  if (filters.category) params.set("category", filters.category);
  if (filters.midCategory) params.set("midCategory", filters.midCategory);
  if (filters.subCategory) params.set("subCategory", filters.subCategory);
  if (filters.color) params.set("color", filters.color);
  if (filters.dimensions) params.set("dimensions", filters.dimensions);
  if (filters.productType) params.set("productType", filters.productType);
  if (filters.material) params.set("material", filters.material);
  if (filters.sort) params.set("sort", filters.sort);
  if (filters.minPrice) params.set("minPrice", filters.minPrice);
  if (filters.maxPrice) params.set("maxPrice", filters.maxPrice);
  return params;
}

export default function IkeaSection({ searchQuery }: MobilyaSectionProps) {
  const [filters, setFilters] = useState<MobilyaFilterState>(defaultFilters);
  const [meta, setMeta] = useState<MobilyaFilters | null>(null);
  const [products, setProducts] = useState<MobilyaProduct[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    setFilters((prev) => ({ ...prev, q: searchQuery }));
  }, [searchQuery]);

  useEffect(() => {
    getMobilyaFilters().then(setMeta).catch(console.error);
  }, []);

  const paramsKey = useMemo(() => buildParams(filters).toString(), [filters]);

  useEffect(() => {
    setLoading(true);
    setError(false);
    getMobilyaProducts(buildParams(filters))
      .then(setProducts)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, [paramsKey]);

  const midOptions = useMemo(() => {
    if (!meta) return [];
    const rows = meta.categoryTree.filter((row) => !filters.category || row.category === filters.category);
    return [...new Set(rows.map((r) => r.midCategory).filter(Boolean))].sort() as string[];
  }, [meta, filters.category]);

  const subOptions = useMemo(() => {
    if (!meta) return [];
    const rows = meta.categoryTree.filter((row) => {
      if (filters.category && row.category !== filters.category) return false;
      if (filters.midCategory && row.midCategory !== filters.midCategory) return false;
      return Boolean(row.subCategory);
    });
    return [...new Set(rows.map((r) => r.subCategory))].sort() as string[];
  }, [meta, filters.category, filters.midCategory]);

  const setCategory = (category: string) => {
    setFilters((prev) => ({ ...prev, category, midCategory: "", subCategory: "" }));
  };

  const clearAll = () => setFilters({ ...defaultFilters, q: filters.q });

  const activeTags = [
    filters.q && { key: "q" as const, label: `"${filters.q}"` },
    filters.brand && { key: "brand" as const, label: `Mağaza: ${filters.brand}` },
    filters.category && { key: "category" as const, label: filters.category },
    filters.midCategory && { key: "midCategory" as const, label: filters.midCategory },
    filters.subCategory && { key: "subCategory" as const, label: filters.subCategory },
    filters.color && { key: "color" as const, label: `Renk: ${filters.color}` },
    filters.dimensions && { key: "dimensions" as const, label: `Ebat: ${filters.dimensions}` },
    filters.productType && { key: "productType" as const, label: `Tip: ${filters.productType}` },
    filters.material && { key: "material" as const, label: `Malzeme: ${filters.material}` },
    filters.minPrice && { key: "minPrice" as const, label: `Min ${filters.minPrice} TL` },
    filters.maxPrice && { key: "maxPrice" as const, label: `Max ${filters.maxPrice} TL` },
  ].filter(Boolean) as Array<{ key: keyof MobilyaFilterState; label: string }>;

  const clearTag = (key: keyof MobilyaFilterState) => {
    setFilters((prev) => {
      const next = { ...prev, [key]: "" };
      if (key === "category") {
        next.midCategory = "";
        next.subCategory = "";
      } else if (key === "midCategory") {
        next.subCategory = "";
      }
      return next;
    });
  };

  return (
    <main className="main-content">
      <div className="ikea-hero-banner" style={{ background: "linear-gradient(135deg, #1e293b 0%, #0f172a 100%)" }}>
        <div style={{ display: "flex", gap: "0.5rem", flexShrink: 0 }}>
          <span style={{ background: "#ffcc00", color: "#0058a3", fontWeight: 800, padding: "0.4rem 0.8rem", borderRadius: "8px", fontSize: "1.1rem" }}>IKEA</span>
          <span style={{ background: "#059669", color: "white", fontWeight: 800, padding: "0.4rem 0.8rem", borderRadius: "8px", fontSize: "1.1rem" }}>Özdilek</span>
        </div>
        <div>
          <h2>Mobilya & Ev Yaşam Kataloğu</h2>
          <p>IKEA ve Özdilek mobilya, ev tekstili, mutfak ve ev yaşam ürünlerini tek bölümde karşılaştırın</p>
        </div>
      </div>

      <div className="category-pills-bar">
        <button
          type="button"
          className={`pill-btn${!filters.category ? " active" : ""}`}
          onClick={() => setCategory("")}
        >
          Tüm Kategoriler
        </button>
        {(meta?.topCategories ?? []).map((cat) => (
          <button
            key={cat}
            type="button"
            className={`pill-btn${filters.category === cat ? " active" : ""}`}
            onClick={() => setCategory(cat)}
          >
            {cat}
          </button>
        ))}
      </div>

      <div className="ikea-layout">
        <aside className="filters-panel">
          <div className="filters-header">
            <h2>Mobilya Filtreleri</h2>
            <button type="button" className="clear-btn" onClick={clearAll}>
              Temizle
            </button>
          </div>

          <FilterSelect
            label="Mağaza / Marka"
            value={filters.brand}
            onChange={(v) => setFilters((p) => ({ ...p, brand: v }))}
          >
            <option value="">Tüm Mağazalar (IKEA & Özdilek)</option>
            {(meta?.brands && meta.brands.length > 0 ? meta.brands : ["IKEA", "Özdilek"]).map((b) => (
              <option key={b} value={b}>{b}</option>
            ))}
          </FilterSelect>

          <FilterSelect label="Ana kategori" value={filters.category} onChange={(v) => setCategory(v)}>
            <option value="">Tüm ana kategoriler</option>
            {(meta?.topCategories ?? []).map((cat) => (
              <option key={cat} value={cat}>{cat}</option>
            ))}
          </FilterSelect>

          <FilterSelect
            label="Alt grup"
            value={filters.midCategory}
            onChange={(v) => setFilters((p) => ({ ...p, midCategory: v, subCategory: "" }))}
          >
            <option value="">Tüm alt gruplar</option>
            {midOptions.map((mid) => (
              <option key={mid} value={mid}>{mid}</option>
            ))}
          </FilterSelect>

          <FilterSelect
            label="Alt kategori"
            value={filters.subCategory}
            onChange={(v) => setFilters((p) => ({ ...p, subCategory: v }))}
          >
            <option value="">Tüm alt kategoriler</option>
            {subOptions.map((sub) => (
              <option key={sub} value={sub}>{sub}</option>
            ))}
          </FilterSelect>

          <FilterSelect label="Renk" value={filters.color} onChange={(v) => setFilters((p) => ({ ...p, color: v }))}>
            <option value="">Tüm renkler</option>
            {(meta?.colors ?? []).map((c) => <option key={c} value={c}>{c}</option>)}
          </FilterSelect>

          <FilterSelect label="Ebat / boyut" value={filters.dimensions} onChange={(v) => setFilters((p) => ({ ...p, dimensions: v }))}>
            <option value="">Tüm ebatlar</option>
            {(meta?.dimensions ?? []).map((d) => <option key={d} value={d}>{d}</option>)}
          </FilterSelect>

          <FilterSelect label="Ürün tipi" value={filters.productType} onChange={(v) => setFilters((p) => ({ ...p, productType: v }))}>
            <option value="">Tüm ürün tipleri</option>
            {(meta?.productTypes ?? []).map((t) => <option key={t} value={t}>{t}</option>)}
          </FilterSelect>

          <FilterSelect label="Malzeme" value={filters.material} onChange={(v) => setFilters((p) => ({ ...p, material: v }))}>
            <option value="">Tüm malzemeler</option>
            {(meta?.materials ?? []).map((m) => <option key={m} value={m}>{m}</option>)}
          </FilterSelect>

          <div className="filter-group">
            <label htmlFor="ikeaSort">Sıralama</label>
            <select id="ikeaSort" value={filters.sort} onChange={(e) => setFilters((p) => ({ ...p, sort: e.target.value }))}>
              <option value="name">İsme göre (A–Z)</option>
              <option value="name_desc">İsme göre (Z–A)</option>
              <option value="price_asc">Fiyat (Düşük → Yüksek)</option>
              <option value="price_desc">Fiyat (Yüksek → Düşük)</option>
            </select>
          </div>

          <div className="filter-group">
            <label>Fiyat aralığı (TL)</label>
            <div className="price-range">
              <input type="number" placeholder="Min" min={0} step="0.01" value={filters.minPrice} onChange={(e) => setFilters((p) => ({ ...p, minPrice: e.target.value }))} />
              <span className="range-sep">—</span>
              <input type="number" placeholder="Max" min={0} step="0.01" value={filters.maxPrice} onChange={(e) => setFilters((p) => ({ ...p, maxPrice: e.target.value }))} />
            </div>
          </div>
        </aside>

        <section className="products-section">
          <div className="results-bar">
            <p className="results-count">
              {loading ? "" : error ? "" : products.length === 0 ? "Sonuç bulunamadı" : `${products.length} mobilya ürünü bulundu`}
            </p>
            <div className="active-filters">
              {activeTags.map(({ key, label }) => (
                <span key={key} className="filter-tag">
                  {label}{" "}
                  <button type="button" aria-label="Kaldır" onClick={() => clearTag(key)}>×</button>
                </span>
              ))}
            </div>
          </div>

          <div className="product-grid">
            {loading && Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="skeleton-card">
                <div className="skeleton-image" />
                <div className="skeleton-body">
                  <div className="skeleton-line short" />
                  <div className="skeleton-line long" />
                </div>
              </div>
            ))}
            {!loading && error && (
              <div className="state-message">
                <h3>Bir hata oluştu</h3>
                <p>Mobilya ürünleri yüklenemedi.</p>
              </div>
            )}
            {!loading && !error && products.length === 0 && (
              <div className="state-message">
                <h3>Ürün bulunamadı</h3>
                <p>Seçilen filtrelerde ürün yok.</p>
              </div>
            )}
            {!loading && !error && products.map((product) => {
              const storeBrand = product.brand || (product.source === "ozdilek" ? "Özdilek" : "IKEA");
              const isOzdilek = storeBrand.toLowerCase().includes("ozdilek") || storeBrand.toLowerCase().includes("özdilek") || product.source === "ozdilek";
              const accentColor = isOzdilek ? "#059669" : "#0058a3";

              return (
                <article key={product.id} className="product-card">
                  <div className="product-image-wrap">
                    {product.imageUrl ? (
                      <img src={product.imageUrl} className="product-image" alt={product.title} loading="lazy" />
                    ) : (
                      <svg className="product-image-placeholder" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                        <rect x="3" y="3" width="18" height="18" rx="2" />
                        <circle cx="8.5" cy="8.5" r="1.5" />
                      </svg>
                    )}
                  </div>
                  <div className="product-body">
                    <p className="product-brand" style={{ color: accentColor, fontWeight: 700 }}>
                      {storeBrand}
                    </p>
                    <h3 className="product-title">{product.title}</h3>
                    <div className="tag-badge-group">
                      {product.subCategory && <span className="tag-badge tag-category">{product.subCategory}</span>}
                      {product.midCategory && <span className="tag-badge tag-category-dim">{product.midCategory}</span>}
                      {product.color && <span className="tag-badge tag-color">🎨 {product.color}</span>}
                      {product.productType && <span className="tag-badge tag-type">🪑 {product.productType}</span>}
                      {product.material && <span className="tag-badge tag-material">🧱 {product.material}</span>}
                      {product.dimensions && <span className="tag-badge tag-dimensions">📏 {product.dimensions}</span>}
                    </div>
                    <div className="product-footer" style={{ marginTop: "0.75rem" }}>
                      <div>
                        <span className="product-price-label">Fiyat</span>
                        <span className="product-price" style={{ color: accentColor }}>{formatPrice(product.price)}</span>
                      </div>
                      <a href={product.productUrl} target="_blank" rel="noopener" className="offer-link" style={{ background: accentColor, color: "white" }}>
                        İncele ↗
                      </a>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        </section>
      </div>
    </main>
  );
}

function FilterSelect({
  label,
  value,
  onChange,
  children,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  children: React.ReactNode;
}) {
  const id = label.replace(/\s+/g, "-").toLowerCase();
  return (
    <div className="filter-group">
      <label htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        {children}
      </select>
    </div>
  );
}
