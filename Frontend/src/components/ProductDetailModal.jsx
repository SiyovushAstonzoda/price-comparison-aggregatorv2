import React, { useState, useEffect } from 'react';
import { fetchProductOffers, fetchSimilarProducts } from '../api';

export default function ProductDetailModal({ productId, onClose }) {
  const [offers, setOffers] = useState([]);
  const [similarProducts, setSimilarProducts] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    Promise.all([
      fetchProductOffers(productId).then(setOffers).catch(console.error),
      fetchSimilarProducts(productId).then(setSimilarProducts).catch(console.error)
    ]).finally(() => setLoading(false));
  }, [productId]);

  return (
    <div className="fixed inset-0 z-[200] flex items-center justify-center p-4 sm:p-6">
      <div 
        className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm transition-opacity"
        onClick={onClose}
      />
      
      <div className="relative w-full max-w-4xl bg-white rounded-2xl shadow-2xl flex flex-col max-h-[90vh] overflow-hidden animate-in fade-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <h2 className="text-xl font-bold text-slate-800">Fiyat Karşılaştırması</h2>
          <button 
            onClick={onClose}
            className="p-2 -mr-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-full transition-colors"
          >
            <svg width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18"></line>
              <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 bg-slate-50/50">
          {loading ? (
            <div className="flex items-center justify-center h-40 text-slate-400">Teklifler yükleniyor...</div>
          ) : offers.length === 0 ? (
            <div className="flex items-center justify-center h-40 text-slate-400">Bu ürün için güncel teklif bulunamadı.</div>
          ) : (
            <div className="space-y-3">
              {offers.map((offer, idx) => (
                <div key={idx} className="flex flex-col sm:flex-row items-center justify-between bg-white p-4 rounded-xl border border-slate-200 hover:border-indigo-300 transition-colors shadow-sm gap-4">
                  
                  <div className="flex items-center gap-4 w-full sm:w-auto">
                    <div className="w-16 h-16 shrink-0 bg-slate-50 border border-slate-100 rounded-lg flex items-center justify-center p-1">
                      {offer.externalImageUrl ? (
                         <img src={offer.externalImageUrl} alt={offer.sellerName} className="max-w-full max-h-full object-contain mix-blend-multiply" />
                      ) : (
                         <span className="text-[10px] text-slate-400 font-bold">{offer.sellerName}</span>
                      )}
                    </div>
                    <div>
                      <h4 className="font-bold text-slate-800 text-lg flex items-center gap-2">
                        {offer.sellerName}
                        {idx === 0 && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-green-100 text-green-700 uppercase tracking-wider">
                            En Ucuz
                          </span>
                        )}
                      </h4>
                      <div className="text-sm text-slate-500 mt-1 flex items-center gap-3">
                        <span className="flex items-center gap-1">
                          <svg width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-amber-400"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"></polygon></svg>
                          {offer.sellerRating} / 5
                        </span>
                        {offer.cargoPrice > 0 ? (
                           <span className="text-slate-400">Kargo: {offer.cargoPrice}₺</span>
                        ) : (
                           <span className="text-green-600 font-medium">Ücretsiz Kargo</span>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center justify-between w-full sm:w-auto sm:justify-end gap-6 border-t sm:border-t-0 border-slate-100 pt-4 sm:pt-0">
                    <div className="text-left sm:text-right">
                      <div className="text-2xl font-black text-indigo-600">
                        {offer.totalCost.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}
                      </div>
                    </div>
                    <a 
                      href={offer.externalUrl || '#'}
                      target="_blank"
                      rel="noreferrer"
                      className="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white font-semibold rounded-lg shadow-sm transition-colors text-sm whitespace-nowrap flex items-center gap-2"
                    >
                      Mağazaya Git
                      <svg width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
                    </a>
                  </div>
                </div>
              ))}
            </div>
          )}

          {!loading && similarProducts.length > 0 && (
            <div className="mt-8 pt-8 border-t border-slate-200">
              <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2">
                <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-pink-500"><path d="M20.24 12.24a6 6 0 0 0-8.49-8.49L5 10.5V19h8.5z"></path><line x1="16" y1="8" x2="2" y2="22"></line><line x1="17.5" y1="15" x2="9" y2="6.5"></line></svg>
                Benzer Ürün Önerileri
              </h3>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                {similarProducts.map(p => (
                  <div key={p.id} className="bg-white p-3 rounded-xl border border-slate-200 flex flex-col items-center text-center gap-2">
                    <div className="h-24 w-24 flex items-center justify-center">
                      <img src={p.imageUrl || 'https://via.placeholder.com/100'} alt={p.name} className="max-h-full max-w-full object-contain mix-blend-multiply" />
                    </div>
                    <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">{p.brand}</div>
                    <div className="text-xs font-semibold text-slate-800 line-clamp-2 leading-tight">{p.name}</div>
                    <div className="text-sm font-black text-indigo-600 mt-auto">{p.lowestPrice.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
