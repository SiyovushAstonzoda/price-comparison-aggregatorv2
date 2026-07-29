import React, { useState, useEffect, useCallback } from 'react';
import SubCategoryChips from './SubCategoryChips';
import ProductGrid from './ProductGrid';
import ProductModal from './ProductModal';

/**
 * Kozmetik & Kişisel Bakım kategorisi sayfası.
 * /api/kozmetik/* endpoint'lerini kullanır.
 * Tüm filtreler anlık (reactive) çalışır — "Uygula" butonu gerekmez.
 */
export default function CosmeticsPage({ apiBase }) {
  // Alt kategori
  const [subCategories, setSubCategories] = useState([]);
  const [activeSubCat, setActiveSubCat]   = useState('');

  // Filtre state'leri (her değişimde anında fetch tetiklenir)
  const [searchInput, setSearchInput] = useState('');
  const [brand, setBrand]             = useState('');
  const [sort, setSort]               = useState('name');
  const [minPrice, setMinPrice]       = useState('');
  const [maxPrice, setMaxPrice]       = useState('');
  const [cosmeticBrands, setCosmeticBrands] = useState([]);

  // Data
  const [products, setProducts]         = useState([]);
  const [loading, setLoading]           = useState(true);
  const [error, setError]               = useState(false);
  const [selectedProduct, setSelectedProduct] = useState(null);

  // ── Alt kategorileri yükle ────────────────────────────────────────────────
  useEffect(() => {
    fetch(`${apiBase}/kozmetik/kategoriler`)
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(data => {
        // Aynı slug'ı tek chip olarak birleştir
        const unique = [];
        const seen = new Set();
        for (const sc of data) {
          const slug = sc.Slug ?? sc.slug;
          if (!seen.has(slug)) { seen.add(slug); unique.push(sc); }
        }
        setSubCategories(unique);
      })
      .catch(err => console.error('Alt kategoriler yüklenemedi:', err));
  }, [apiBase]);

  // ── Kozmetik markalarını yükle (subCat veya brand değişince) ─────────────
  useEffect(() => {
    const qs = activeSubCat ? `?subCategory=${encodeURIComponent(activeSubCat)}` : '';
    fetch(`${apiBase}/kozmetik/markalar${qs}`)
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(setCosmeticBrands)
      .catch(err => console.error('Markalar yüklenemedi:', err));
  }, [apiBase, activeSubCat]);

  // ── Ürünleri getir — tüm filtreler değişince tetiklenir ──────────────────
  const fetchProducts = useCallback(() => {
    setLoading(true);
    setError(false);

    const params = new URLSearchParams();
    if (activeSubCat)                         params.set('subCategory', activeSubCat);
    if (searchInput.trim())                   params.set('q',           searchInput.trim());
    if (brand)                                params.set('brand',       brand);
    if (sort)                                 params.set('sort',        sort);
    if (minPrice !== '' && !isNaN(minPrice))  params.set('minPrice',    minPrice);
    if (maxPrice !== '' && !isNaN(maxPrice))  params.set('maxPrice',    maxPrice);

    fetch(`${apiBase}/kozmetik/urunler?${params}`)
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(data => { setProducts(data); setLoading(false); })
      .catch(() => { setError(true); setLoading(false); });
  }, [apiBase, activeSubCat, searchInput, brand, sort, minPrice, maxPrice]);

  useEffect(() => { fetchProducts(); }, [fetchProducts]);

  // ── Handler'lar ──────────────────────────────────────────────────────────

  const handleSubCatSelect = (slug) => {
    setActiveSubCat(slug);
    setBrand('');  // alt kategori değişince marka sıfırla
  };

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    fetchProducts();  // searchInput zaten state'te, mevcut fetch tetikler
  };

  const handleClearFilters = () => {
    setSearchInput('');
    setBrand('');
    setSort('name');
    setMinPrice('');
    setMaxPrice('');
  };

  const resultsText = loading ? '' : error ? '' :
    products.length === 0 ? 'Sonuç bulunamadı' : `${products.length} ürün bulundu`;

  // Ortak stil sınıfları
  const inputCls = "w-full px-3 py-2.5 border-[1.5px] border-slate-200 rounded-lg text-sm font-sans text-slate-900 bg-white transition-all duration-150 focus:outline-none focus:border-rose-500 focus:ring-3 focus:ring-rose-500/12";
  const selectCls = `${inputCls} appearance-none bg-[url("data:image/svg+xml,%3Csvg_xmlns='http://www.w3.org/2000/svg'_fill='none'_viewBox='0_0_24_24'_stroke='%2364748b'_stroke-width='2'%3E%3Cpath_stroke-linecap='round'_stroke-linejoin='round'_d='M19_9l-7_7-7-7'/%3E%3C/svg%3E")] bg-no-repeat bg-[right_0.75rem_center] bg-[length:16px] pr-9`;
  const labelCls  = "block text-[0.8125rem] font-semibold text-slate-600 mb-2 uppercase tracking-wider";

  return (
    <div className="min-h-screen bg-rose-50/30">

      {/* ── Hero Banner ──────────────────────────────────────────────── */}
      <div className="bg-gradient-to-br from-rose-600 via-pink-600 to-purple-600 text-white">
        <div className="max-w-[1280px] mx-auto px-6 py-10">
          <div className="flex items-center gap-3 mb-3">
            <span className="text-3xl">✨</span>
            <h1 className="font-display text-3xl font-extrabold tracking-tight">
              Kozmetik & Kişisel Bakım
            </h1>
          </div>
          <p className="text-rose-100 text-[0.9375rem] max-w-[540px]">
            Migros, MacroCenter ve Mion'dan en uygun kozmetik fiyatlarını karşılaştır.
          </p>

          <form id="cosmeticsSearchForm" className="mt-6 flex items-center gap-2 max-w-[500px]" onSubmit={handleSearchSubmit}>
            <input
              id="cosmeticsSearchInput"
              type="search"
              placeholder="Ürün veya marka ara…"
              value={searchInput}
              onChange={e => setSearchInput(e.target.value)}
              className="flex-1 px-4 py-2.5 rounded-xl border-0 bg-white/20 backdrop-blur-sm text-white placeholder:text-rose-200 outline-none focus:bg-white/30 transition-all text-sm font-medium"
            />
            <button type="submit" className="px-5 py-2.5 bg-white text-rose-600 rounded-xl font-bold text-sm shadow-md hover:shadow-lg hover:-translate-y-0.5 transition-all duration-200">
              Ara
            </button>
          </form>
        </div>
      </div>

      {/* ── Alt Kategori Chips ───────────────────────────────────────── */}
      <div className="max-w-[1280px] mx-auto px-6 pt-6">
        <SubCategoryChips subCategories={subCategories} activeSlug={activeSubCat} onSelect={handleSubCatSelect} />
      </div>

      {/* ── Ana İçerik ──────────────────────────────────────────────── */}
      <main className="max-w-[1280px] mx-auto px-6 pb-10 grid grid-cols-1 min-[841px]:grid-cols-[260px_1fr] gap-8 items-start">

        {/* Filtre Paneli — tamamen yeniden yazıldı (rose teması, anlık uygulama) */}
        <aside className="bg-white border border-slate-200 rounded-2xl p-6 min-[841px]:sticky min-[841px]:top-[calc(72px+2rem)] shadow-[0_4px_12px_-2px_rgba(15,23,42,0.06)]">
          <div className="flex items-center justify-between mb-5 pb-3.5 border-b border-slate-100">
            <h2 className="font-display text-[1.05rem] font-bold text-slate-900 tracking-tight">Filtreler</h2>
            <button
              id="clearCosmeticsFilters"
              className="bg-transparent border-0 text-rose-600 text-[0.8125rem] font-semibold font-sans cursor-pointer px-2 py-1 rounded-md transition-all duration-150 hover:bg-rose-50"
              type="button"
              onClick={handleClearFilters}
            >
              Temizle
            </button>
          </div>

          {/* Marka */}
          <div className="mb-5">
            <label htmlFor="cosmeticsBrandFilter" className={labelCls}>Marka</label>
            <select
              id="cosmeticsBrandFilter"
              value={brand}
              onChange={e => setBrand(e.target.value)}   // anlık — state değişir → fetch tetiklenir
              className={selectCls}
            >
              <option value="">Tüm markalar</option>
              {cosmeticBrands.map(b => (
                <option key={b} value={b}>{b}</option>
              ))}
            </select>
          </div>

          {/* Sıralama */}
          <div className="mb-5">
            <label htmlFor="cosmeticsSortFilter" className={labelCls}>Sıralama</label>
            <select
              id="cosmeticsSortFilter"
              value={sort}
              onChange={e => setSort(e.target.value)}   // anlık
              className={selectCls}
            >
              <option value="name">İsme göre (A–Z)</option>
              <option value="name_desc">İsme göre (Z–A)</option>
              <option value="price_asc">Fiyat (Düşük → Yüksek)</option>
              <option value="price_desc">Fiyat (Yüksek → Düşük)</option>
            </select>
          </div>

          {/* Fiyat Aralığı */}
          <div className="mb-5">
            <label className={labelCls}>Fiyat aralığı (TL)</label>
            <div className="flex items-center gap-2">
              <input
                id="cosmeticsMinPrice"
                type="number"
                placeholder="Min"
                min="0"
                step="0.01"
                value={minPrice}
                onChange={e => setMinPrice(e.target.value)}
                onBlur={fetchProducts}   // alan terk edilince uygula
                className={`${inputCls} flex-1 min-w-0`}
              />
              <span className="text-slate-400 font-semibold">—</span>
              <input
                id="cosmeticsMaxPrice"
                type="number"
                placeholder="Max"
                min="0"
                step="0.01"
                value={maxPrice}
                onChange={e => setMaxPrice(e.target.value)}
                onBlur={fetchProducts}   // alan terk edilince uygula
                className={`${inputCls} flex-1 min-w-0`}
              />
            </div>
            <p className="text-[0.75rem] text-slate-400 mt-1.5">Alandan çıkınca otomatik uygulanır</p>
          </div>
        </aside>

        {/* Ürün Listesi */}
        <section className="min-w-0">
          <div className="flex items-center justify-between flex-wrap gap-4 mb-6 py-1">
            <p id="cosmeticsResultsCount" className="font-display text-base font-semibold text-slate-600">
              {resultsText}
            </p>
            {/* Aktif filtre etiketleri */}
            <div className="flex flex-wrap gap-2">
              {brand && (
                <span className="inline-flex items-center gap-1.5 bg-rose-50 text-rose-700 border border-rose-200 text-xs font-semibold px-2.5 py-1 rounded-full">
                  {brand}
                  <button onClick={() => setBrand('')} className="text-rose-400 hover:text-rose-700 cursor-pointer border-0 bg-transparent p-0 leading-none">×</button>
                </span>
              )}
              {searchInput && (
                <span className="inline-flex items-center gap-1.5 bg-rose-50 text-rose-700 border border-rose-200 text-xs font-semibold px-2.5 py-1 rounded-full">
                  "{searchInput}"
                  <button onClick={() => setSearchInput('')} className="text-rose-400 hover:text-rose-700 cursor-pointer border-0 bg-transparent p-0 leading-none">×</button>
                </span>
              )}
              {(minPrice || maxPrice) && (
                <span className="inline-flex items-center gap-1.5 bg-rose-50 text-rose-700 border border-rose-200 text-xs font-semibold px-2.5 py-1 rounded-full">
                  {minPrice && `${minPrice} TL`}{minPrice && maxPrice && ' – '}{maxPrice && `${maxPrice} TL`}
                  <button onClick={() => { setMinPrice(''); setMaxPrice(''); }} className="text-rose-400 hover:text-rose-700 cursor-pointer border-0 bg-transparent p-0 leading-none">×</button>
                </span>
              )}
            </div>
          </div>

          <ProductGrid
            loading={loading}
            error={error}
            products={products}
            state={{ q: searchInput, brand, minPrice, maxPrice }}
            onSelectProduct={setSelectedProduct}
          />
        </section>
      </main>

      {/* Ürün Modal */}
      <ProductModal
        product={selectedProduct}
        onClose={() => setSelectedProduct(null)}
        apiBase={apiBase}
        offersEndpoint={`${apiBase}/kozmetik/urunler`}
        onSelectProduct={setSelectedProduct}
      />
    </div>
  );
}
