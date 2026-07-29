import React from 'react';
import ProductCard from './ProductCard';

export default function ProductGrid({ loading, error, products, state, onSelectProduct }) {
  if (loading) {
    return (
      <div id="products" className="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-5 w-full">
        {Array(8)
          .fill(null)
          .map((_, i) => (
            <div key={i} className="bg-white border border-slate-100 rounded-2xl overflow-hidden shadow-xs">
              <div className="h-[200px] animate-shimmer"></div>
              <div className="p-5">
                <div className="h-[14px] animate-shimmer rounded-md mb-2.5 w-[35%]"></div>
                <div className="h-[14px] animate-shimmer rounded-md mb-2.5 w-[90%]"></div>
                <div className="h-[14px] animate-shimmer rounded-md mb-2.5 w-[65%]"></div>
              </div>
            </div>
          ))}
      </div>
    );
  }

  if (error) {
    return (
      <div id="products" className="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-5 w-full">
        <div className="col-span-full text-center py-20 px-8 bg-white border border-dashed border-slate-200 rounded-3xl text-slate-600">
          <svg className="w-14 h-14 text-indigo-600 opacity-70 mb-5 mx-auto" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <circle cx="12" cy="12" r="10" />
            <path d="M12 8v4M12 16h.01" />
          </svg>
          <h3 className="font-display text-xl font-bold text-slate-900 mb-2">Bir hata oluştu</h3>
          <p className="text-[0.9375rem] max-w-[420px] mx-auto">Ürünler yüklenemedi. API'nin çalıştığından emin olun.</p>
        </div>
      </div>
    );
  }

  if (products.length === 0) {
    const isFiltered = Boolean(state.q || state.brand || state.minPrice || state.maxPrice);
    return (
      <div id="products" className="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-5 w-full">
        <div className="col-span-full text-center py-20 px-8 bg-white border border-dashed border-slate-200 rounded-3xl text-slate-600">
          <svg className="w-14 h-14 text-indigo-600 opacity-70 mb-5 mx-auto" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.35-4.35" />
          </svg>
          <h3 className="font-display text-xl font-bold text-slate-900 mb-2">Ürün bulunamadı</h3>
          <p className="text-[0.9375rem] max-w-[420px] mx-auto">
            {isFiltered
              ? "Filtreleri değiştirmeyi veya aramayı temizlemeyi deneyin."
              : "Henüz ürün eklenmemiş."}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div id="products" className="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-5 w-full">
      {products.map((product) => (
        <ProductCard
          key={product.Id ?? product.id}
          product={product}
          onSelectProduct={onSelectProduct}
        />
      ))}
    </div>
  );
}
