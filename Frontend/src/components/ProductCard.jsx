import React from 'react';

export default function ProductCard({ product, onClick }) {
  return (
    <div 
      onClick={onClick}
      className="bg-white border border-slate-200 rounded-xl overflow-hidden hover:shadow-xl hover:border-indigo-300 transition-all cursor-pointer group flex flex-col h-full"
    >
      <div className="p-4 bg-white flex items-center justify-center aspect-square relative">
        <img 
          src={product.imageUrl || 'https://via.placeholder.com/200'} 
          alt={product.canonicalTitle || product.name}
          className="max-h-full max-w-full object-contain group-hover:scale-105 transition-transform duration-300"
        />
        {product.offerCount > 0 && (
          <div className="absolute top-3 left-3 bg-indigo-50 text-indigo-700 text-xs font-bold px-2 py-1 rounded-md border border-indigo-100">
            {product.offerCount} Satıcı
          </div>
        )}
      </div>
      <div className="p-4 border-t border-slate-100 flex flex-col flex-1 bg-slate-50/50">
        {product.brand && (
          <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider mb-1">{product.brand}</div>
        )}
        <h3 className="font-semibold text-slate-800 text-sm leading-snug line-clamp-2 mb-4 group-hover:text-indigo-600 transition-colors">
          {product.canonicalTitle || product.name}
        </h3>
        <div className="mt-auto">
          <div className="text-xs text-slate-500 mb-0.5">En ucuz:</div>
          <div className="text-xl font-bold text-indigo-600">
            {product.lowestPrice.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}
          </div>
        </div>
      </div>
    </div>
  );
}
