import React from 'react';

export function formatPrice(price) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  }).format(price);
}

export default function ProductCard({ product, onSelectProduct }) {
  const handleKeyDown = (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onSelectProduct(product);
    }
  };

  const imageUrl = product.ImageUrl ?? product.imageUrl;
  const canonicalTitle = product.CanonicalTitle ?? product.canonicalTitle;
  const brand = product.Brand ?? product.brand;
  const lowestPrice = product.LowestPrice ?? product.lowestPrice;
  const offerCount = product.OfferCount ?? product.offerCount;

  return (
    <article
      className="group bg-white border border-slate-200 rounded-2xl overflow-hidden cursor-pointer transition-all duration-250 ease-[cubic-bezier(0.4,0,0.2,1)] flex flex-col shadow-xs relative hover:shadow-[0_20px_25px_-5px_rgba(79,70,229,0.1),0_8px_10px_-6px_rgba(79,70,229,0.04)] hover:-translate-y-1 hover:border-slate-300"
      role="button"
      tabIndex={0}
      onClick={() => onSelectProduct(product)}
      onKeyDown={handleKeyDown}
    >
      <div className="bg-gradient-to-b from-slate-50 to-white p-5 flex items-center justify-center h-[200px] border-b border-slate-100/80 overflow-hidden">
        {imageUrl ? (
          <img
            src={imageUrl}
            className="max-w-full max-h-full object-contain transition-transform duration-300 group-hover:scale-[1.06]"
            alt={canonicalTitle}
            loading="lazy"
          />
        ) : (
          <svg className="w-16 h-16 text-slate-400 opacity-35" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <rect x="3" y="3" width="18" height="18" rx="2" />
            <circle cx="8.5" cy="8.5" r="1.5" />
            <path d="m21 15-5-5L5 21" />
          </svg>
        )}
      </div>
      <div className="p-5 flex-1 flex flex-col">
        {brand ? <p className="text-[0.75rem] font-bold text-indigo-600 uppercase tracking-widest mb-1.5">{brand}</p> : null}
        <h3 className="font-sans text-[0.9375rem] font-semibold text-slate-900 leading-snug mb-4 flex-1 line-clamp-2 overflow-hidden">{canonicalTitle}</h3>
        <div className="flex items-end justify-between gap-2 pt-3 border-t border-slate-100/80">
          <div>
            <span className="block text-[0.6875rem] font-semibold text-slate-400 uppercase tracking-wider mb-0.5">En düşük fiyat</span>
            <span className="font-display text-xl font-extrabold text-emerald-600 tracking-tight">{formatPrice(lowestPrice)}</span>
          </div>
          <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-600 bg-slate-50 border border-slate-200 px-2.5 py-1.25 rounded-md whitespace-nowrap">{offerCount} mağaza</span>
        </div>
      </div>
    </article>
  );
}
