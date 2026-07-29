import React from 'react';

// Alt kategori chip'leri — kozmetik sayfasının üstünde gösterilir
export default function SubCategoryChips({ subCategories, activeSlug, onSelect }) {
  return (
    <div className="flex flex-wrap gap-2 mb-6">
      {/* "Tümü" chip */}
      <button
        id="subcat-all"
        onClick={() => onSelect('')}
        className={`px-4 py-2 rounded-full text-sm font-semibold border transition-all duration-200 cursor-pointer
          ${!activeSlug
            ? 'bg-rose-500 text-white border-rose-500 shadow-[0_4px_12px_rgba(244,63,94,0.3)]'
            : 'bg-white text-slate-700 border-slate-200 hover:border-rose-400 hover:text-rose-600'
          }`}
      >
        Tümü
      </button>

      {subCategories.map((sc) => {
        const slug = sc.Slug ?? sc.slug;
        const name = sc.DisplayName ?? sc.displayName;
        const count = sc.ProductCount ?? sc.productCount ?? 0;
        const isActive = activeSlug === slug;

        return (
          <button
            key={`${slug}-${sc.Source ?? sc.source}`}
            id={`subcat-${slug}`}
            onClick={() => onSelect(slug)}
            className={`px-4 py-2 rounded-full text-sm font-semibold border transition-all duration-200 cursor-pointer
              ${isActive
                ? 'bg-rose-500 text-white border-rose-500 shadow-[0_4px_12px_rgba(244,63,94,0.3)]'
                : 'bg-white text-slate-700 border-slate-200 hover:border-rose-400 hover:text-rose-600'
              }`}
          >
            {name}
            {count > 0 && (
              <span className={`ml-1.5 text-xs font-bold ${isActive ? 'text-rose-100' : 'text-slate-400'}`}>
                {count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
