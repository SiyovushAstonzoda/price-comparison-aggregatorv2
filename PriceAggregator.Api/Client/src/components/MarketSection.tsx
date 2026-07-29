import { useEffect, useMemo, useState } from "react";
import {
  formatPrice,
  formatSource,
  getBrands,
  getMarketProducts,
  getSources,
} from "../api";
import type { MarketFilters, MarketProduct } from "../types";

interface MarketSectionProps {
  filters: MarketFilters;
  onFiltersChange: (filters: MarketFilters) => void;
  onProductClick: (id: number, title: string) => void;
}

function buildParams(filters: MarketFilters) {
  const params = new URLSearchParams();
  if (filters.q) params.set("q", filters.q);
  if (filters.brand) params.set("brand", filters.brand);
  if (filters.source) params.set("source", filters.source);
  if (filters.sort) params.set("sort", filters.sort);
  if (filters.minPrice) params.set("minPrice", filters.minPrice);
  if (filters.maxPrice) params.set("maxPrice", filters.maxPrice);
  return params;
}

export default function MarketSection({
  filters,
  onFiltersChange,
  onProductClick,
}: MarketSectionProps) {
  const [brands, setBrands] = useState<string[]>([]);
  const [sources, setSources] = useState<string[]>([]);
  const [products, setProducts] = useState<MarketProduct[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const paramsKey = useMemo(() => buildParams(filters).toString(), [filters]);

  useEffect(() => {
    Promise.all([getBrands(), getSources()])
      .then(([brandList, sourceList]) => {
        setBrands(brandList);
        setSources(sourceList);
      })
      .catch(console.error);
  }, []);

  useEffect(() => {
    setLoading(true);
    setError(false);
    getMarketProducts(buildParams(filters))
      .then(setProducts)
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, [paramsKey]);

  const activeTags = [
    filters.q && { key: "q" as const, label: `"${filters.q}"` },
    filters.source && { key: "source" as const, label: formatSource(filters.source) },
    filters.brand && { key: "brand" as const, label: filters.brand },
    filters.minPrice && { key: "minPrice" as const, label: `Min ${filters.minPrice} TL` },
    filters.maxPrice && { key: "maxPrice" as const, label: `Max ${filters.maxPrice} TL` },
  ].filter(Boolean) as Array<{ key: keyof MarketFilters; label: string }>;

  const clearTag = (key: keyof MarketFilters) => {
    onFiltersChange({ ...filters, [key]: "" });
  };

  const clearAll = () => onFiltersChange({ ...filters, q: "", brand: "", source: "", sort: "name", minPrice: "", maxPrice: "" });

  return (
    <main className="main-content">
      <aside className="filters-panel">
        <div className="filters-header">
          <h2>Filtreler</h2>
          <button type="button" className="clear-btn" onClick={clearAll}>
            Temizle
          </button>
        </div>

        <div className="filter-group">
          <label htmlFor="sourceFilter">Mağaza</label>
          <select
            id="sourceFilter"
            value={filters.source}
            onChange={(e) => onFiltersChange({ ...filters, source: e.target.value })}
          >
            <option value="">Tüm mağazalar</option>
            {sources.map((source) => (
              <option key={source} value={source}>
                {formatSource(source)}
              </option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="brandFilter">Marka</label>
          <select
            id="brandFilter"
            value={filters.brand}
            onChange={(e) => onFiltersChange({ ...filters, brand: e.target.value })}
          >
            <option value="">Tüm markalar</option>
            {brands.map((brand) => (
              <option key={brand} value={brand}>
                {brand}
              </option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="sortFilter">Sıralama</label>
          <select
            id="sortFilter"
            value={filters.sort}
            onChange={(e) => onFiltersChange({ ...filters, sort: e.target.value })}
          >
            <option value="name">İsme göre (A–Z)</option>
            <option value="name_desc">İsme göre (Z–A)</option>
            <option value="price_asc">Fiyat (Düşük → Yüksek)</option>
            <option value="price_desc">Fiyat (Yüksek → Düşük)</option>
          </select>
        </div>

        <div className="filter-group">
          <label>Fiyat aralığı (TL)</label>
          <div className="price-range">
            <input
              type="number"
              placeholder="Min"
              min={0}
              step="0.01"
              value={filters.minPrice}
              onChange={(e) => onFiltersChange({ ...filters, minPrice: e.target.value })}
            />
            <span className="range-sep">—</span>
            <input
              type="number"
              placeholder="Max"
              min={0}
              step="0.01"
              value={filters.maxPrice}
              onChange={(e) => onFiltersChange({ ...filters, maxPrice: e.target.value })}
            />
          </div>
        </div>

        <button
          type="button"
          className="apply-btn"
          onClick={() => onFiltersChange({ ...filters })}
        >
          Filtreleri Uygula
        </button>
      </aside>

      <section className="products-section">
        <div className="results-bar">
          <p className="results-count">
            {loading ? "" : error ? "" : products.length === 0 ? "Sonuç bulunamadı" : `${products.length} ürün bulundu`}
          </p>
          <div className="active-filters">
            {activeTags.map(({ key, label }) => (
              <span key={key} className="filter-tag">
                {label}{" "}
                <button type="button" aria-label="Kaldır" onClick={() => clearTag(key)}>
                  ×
                </button>
              </span>
            ))}
          </div>
        </div>

        <div className="product-grid">
          {loading && <SkeletonGrid />}
          {!loading && error && <ErrorState />}
          {!loading && !error && products.length === 0 && (
            <EmptyState hasFilters={Boolean(filters.q || filters.brand || filters.source || filters.minPrice || filters.maxPrice)} />
          )}
          {!loading &&
            !error &&
            products.map((product) => (
              <article
                key={product.id}
                className="product-card"
                role="button"
                tabIndex={0}
                onClick={() => onProductClick(product.id, product.canonicalTitle)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    onProductClick(product.id, product.canonicalTitle);
                  }
                }}
              >
                <div className="product-image-wrap">
                  {product.imageUrl ? (
                    <img src={product.imageUrl} className="product-image" alt={product.canonicalTitle} loading="lazy" />
                  ) : (
                    <svg className="product-image-placeholder" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                      <rect x="3" y="3" width="18" height="18" rx="2" />
                      <circle cx="8.5" cy="8.5" r="1.5" />
                      <path d="m21 15-5-5L5 21" />
                    </svg>
                  )}
                </div>
                <div className="product-body">
                  {product.brand && <p className="product-brand">{product.brand}</p>}
                  <h3 className="product-title">{product.canonicalTitle}</h3>
                  <div className="product-footer">
                    <div>
                      <span className="product-price-label">En düşük fiyat</span>
                      <span className="product-price">{formatPrice(product.lowestPrice)}</span>
                    </div>
                    <span className="product-stores">
                      {product.offerCount} mağaza
                      {product.sources && product.offerCount > 1 ? "" : product.sources ? ` · ${formatSource(product.sources.split(",")[0])}` : ""}
                    </span>
                  </div>
                </div>
              </article>
            ))}
        </div>
      </section>
    </main>
  );
}

function SkeletonGrid() {
  return Array.from({ length: 8 }).map((_, i) => (
    <div key={i} className="skeleton-card">
      <div className="skeleton-image" />
      <div className="skeleton-body">
        <div className="skeleton-line short" />
        <div className="skeleton-line long" />
        <div className="skeleton-line medium" />
      </div>
    </div>
  ));
}

function EmptyState({ hasFilters }: { hasFilters: boolean }) {
  return (
    <div className="state-message">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
        <circle cx="11" cy="11" r="8" />
        <path d="m21 21-4.35-4.35" />
      </svg>
      <h3>Ürün bulunamadı</h3>
      <p>
        {hasFilters
          ? "Filtreleri değiştirmeyi veya aramayı temizlemeyi deneyin."
          : "Henüz ürün eklenmemiş. Scraper'ı çalıştırın."}
      </p>
    </div>
  );
}

function ErrorState() {
  return (
    <div className="state-message">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
        <circle cx="12" cy="12" r="10" />
        <path d="M12 8v4M12 16h.01" />
      </svg>
      <h3>Bir hata oluştu</h3>
      <p>Ürünler yüklenemedi. API'nin çalıştığından emin olun.</p>
    </div>
  );
}
