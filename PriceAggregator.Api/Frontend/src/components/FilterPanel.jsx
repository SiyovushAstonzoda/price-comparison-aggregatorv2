import React from 'react';

export default function FilterPanel({
  brands,
  brand,
  setBrand,
  sort,
  setSort,
  minPrice,
  setMinPrice,
  maxPrice,
  setMaxPrice,
  onApplyFilters,
  onClearFilters,
}) {
  const inputSelectClasses =
    "w-full px-3 py-2.5 border-[1.5px] border-slate-200 rounded-lg text-sm font-sans text-slate-900 bg-white transition-all duration-150 focus:outline-none focus:border-indigo-600 focus:ring-3 focus:ring-indigo-600/12";
  const selectClasses =
    `${inputSelectClasses} appearance-none bg-[url("data:image/svg+xml,%3Csvg_xmlns='http://www.w3.org/2000/svg'_fill='none'_viewBox='0_0_24_24'_stroke='%2364748b'_stroke-width='2'%3E%3Cpath_stroke-linecap='round'_stroke-linejoin='round'_d='M19_9l-7_7-7-7'/%3E%3C/svg%3E")] bg-no-repeat bg-[right_0.75rem_center] bg-[length:16px] pr-9`;

  return (
    <aside className="bg-white border border-slate-200 rounded-2xl p-6 min-[841px]:sticky min-[841px]:top-[calc(72px+2rem)] shadow-[0_4px_12px_-2px_rgba(15,23,42,0.06),0_2px_6px_-1px_rgba(15,23,42,0.03)]">
      <div className="flex items-center justify-between mb-5 pb-3.5 border-b border-slate-100">
        <h2 className="font-display text-[1.05rem] font-bold text-slate-900 tracking-tight">Filtreler</h2>
        <button
          id="clearFilters"
          className="bg-transparent border-0 text-indigo-600 text-[0.8125rem] font-semibold font-sans cursor-pointer px-2 py-1 rounded-md transition-all duration-150 hover:bg-indigo-50 hover:text-indigo-700"
          type="button"
          onClick={onClearFilters}
        >
          Temizle
        </button>
      </div>

      <div className="mb-5">
        <label htmlFor="brandFilter" className="block text-[0.8125rem] font-semibold text-slate-600 mb-2 uppercase tracking-wider">
          Marka
        </label>
        <select
          id="brandFilter"
          value={brand}
          onChange={(e) => setBrand(e.target.value)}
          className={selectClasses}
        >
          <option value="">Tüm markalar</option>
          {brands.map((b) => (
            <option key={b} value={b}>
              {b}
            </option>
          ))}
        </select>
      </div>

      <div className="mb-5">
        <label htmlFor="sortFilter" className="block text-[0.8125rem] font-semibold text-slate-600 mb-2 uppercase tracking-wider">
          Sıralama
        </label>
        <select
          id="sortFilter"
          value={sort}
          onChange={(e) => setSort(e.target.value)}
          className={selectClasses}
        >
          <option value="name">İsme göre (A–Z)</option>
          <option value="name_desc">İsme göre (Z–A)</option>
          <option value="price_asc">Fiyat (Düşük → Yüksek)</option>
          <option value="price_desc">Fiyat (Yüksek → Düşük)</option>
        </select>
      </div>

      <div className="mb-5">
        <label className="block text-[0.8125rem] font-semibold text-slate-600 mb-2 uppercase tracking-wider">
          Fiyat aralığı (TL)
        </label>
        <div className="flex items-center gap-2">
          <input
            id="minPrice"
            type="number"
            placeholder="Min"
            min="0"
            step="0.01"
            value={minPrice}
            onChange={(e) => setMinPrice(e.target.value)}
            className={`${inputSelectClasses} flex-1 min-w-0`}
          />
          <span className="text-slate-400 font-semibold">—</span>
          <input
            id="maxPrice"
            type="number"
            placeholder="Max"
            min="0"
            step="0.01"
            value={maxPrice}
            onChange={(e) => setMaxPrice(e.target.value)}
            className={`${inputSelectClasses} flex-1 min-w-0`}
          />
        </div>
      </div>

      <button
        id="applyFilters"
        className="w-full mt-2 p-3 bg-slate-900 text-white border-0 rounded-lg text-sm font-bold font-sans cursor-pointer shadow-xs transition-all duration-200 hover:bg-indigo-600 hover:shadow-[0_4px_12px_rgba(79,70,229,0.3)] hover:-translate-y-0.5"
        type="button"
        onClick={onApplyFilters}
      >
        Filtreleri Uygula
      </button>
    </aside>
  );
}
