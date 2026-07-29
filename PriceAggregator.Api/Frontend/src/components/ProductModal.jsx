import React, { useEffect, useState } from 'react';
import { formatPrice } from './ProductCard';
import ProductCard from './ProductCard'; // Reuse ProductCard for similar products

export default function ProductModal({ product, onClose, apiBase, offersEndpoint, onSelectProduct }) {
  const baseOfferUrl = offersEndpoint ?? `${apiBase}/products`;
  // Assuming if offersEndpoint is provided (like /api/kozmetik/urunler), similar endpoint is {offersEndpoint}/{masterId}/benzer
  const [offers, setOffers] = useState([]);
  const [similarProducts, setSimilarProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!product) return;

    let isMounted = true;
    setLoading(true);
    setError(false);

    const masterId = product.Id ?? product.id;
    
    // Fetch offers
    const pOffers = fetch(`${baseOfferUrl}/${masterId}`).then(res => res.ok ? res.json() : Promise.reject("Failed offers"));
    
    // Fetch similar products if offersEndpoint ends with /urunler (like in cosmetics)
    let pSimilar = Promise.resolve([]);
    if (baseOfferUrl.includes('/urunler')) {
      pSimilar = fetch(`${baseOfferUrl}/${masterId}/benzer`).then(res => res.ok ? res.json() : Promise.resolve([]));
    }

    Promise.all([pOffers, pSimilar])
      .then(([offersData, similarData]) => {
        if (isMounted) {
          setOffers(offersData);
          setSimilarProducts(similarData);
          setLoading(false);
        }
      })
      .catch((err) => {
        console.error(err);
        if (isMounted) {
          setError(true);
          setLoading(false);
        }
      });

    const handleKeyDown = (e) => {
      if (e.key === "Escape") onClose();
    };

    document.addEventListener("keydown", handleKeyDown);
    document.body.style.overflow = "hidden";

    return () => {
      isMounted = false;
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "";
    };
  }, [product, baseOfferUrl, onClose]);

  if (!product) return null;

  const canonicalTitle = product.CanonicalTitle ?? product.canonicalTitle;

  return (
    <div id="modal" className="fixed inset-0 z-[200] flex items-center justify-center p-6" role="dialog" aria-modal="true">
      <div className="absolute inset-0 bg-slate-900/60 backdrop-blur-md animate-fade-in" onClick={onClose}></div>
      <div className="relative bg-white rounded-3xl p-7 w-full max-w-[800px] max-h-[85vh] overflow-y-auto shadow-[0_20px_30px_-10px_rgba(15,23,42,0.1),0_10px_15px_-5px_rgba(15,23,42,0.04)] border border-slate-200 animate-modal-scale flex flex-col md:flex-row gap-8">
        
        {/* Left Side: Offers */}
        <div className="flex-1">
          <button
            id="closeModal"
            className="absolute top-5 right-5 bg-slate-50 border border-slate-200 rounded-xl w-9 h-9 flex items-center justify-center cursor-pointer text-slate-600 transition-all duration-150 hover:bg-slate-200 hover:text-slate-900 hover:rotate-90 z-10"
            aria-label="Kapat"
            onClick={onClose}
          >
            <svg className="w-[18px] h-[18px]" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M18 6 6 18M6 6l12 12" />
            </svg>
          </button>
          
          <h2 id="modalTitle" className="font-display text-xl font-bold pr-12 mb-1 text-slate-900">
            {canonicalTitle}
          </h2>
          <p className="text-sm text-slate-600 mb-6 font-medium">Mağaza fiyatları</p>

          <div id="offers" className="flex flex-col gap-3">
            {loading && (
              <div className="col-span-full text-center py-6 text-slate-600">
                <p>Yükleniyor...</p>
              </div>
            )}

            {error && !loading && (
              <div className="col-span-full text-center py-6 text-slate-600">
                <p>Fiyatlar yüklenemedi.</p>
              </div>
            )}

            {!loading && !error && offers.map((offer, index) => {
              const imageUrl = offer.ImageUrl ?? offer.imageUrl;
              const source = offer.Source ?? offer.source;
              const price = offer.Price ?? offer.price;
              const productUrl = offer.ProductUrl ?? offer.productUrl;

              return (
                <div
                  key={index}
                  className={`flex items-center gap-4 p-4 border rounded-xl bg-white transition-all duration-200 ${
                    index === 0
                      ? 'border-emerald-300 bg-gradient-to-br from-emerald-50/80 to-emerald-50/50 shadow-[0_4px_12px_rgba(16,185,129,0.08)]'
                      : 'border-slate-200'
                  }`}
                >
                  {imageUrl && (
                    <img
                      src={imageUrl}
                      alt={source}
                      className="w-12 h-12 object-contain rounded-lg bg-white p-1 border border-slate-200 shrink-0"
                    />
                  )}
                  <div className="flex-1 min-w-0">
                    <div className="font-display font-bold text-[0.9375rem] text-slate-900 capitalize">{source}</div>
                    {index === 0 && (
                      <span className="inline-block text-[0.6875rem] font-bold text-white bg-gradient-to-br from-emerald-500 to-emerald-600 px-2 py-0.5 rounded-full mt-1 shadow-[0_2px_6px_rgba(16,185,129,0.25)] uppercase tracking-wider">
                        En ucuz
                      </span>
                    )}
                  </div>
                  <span className="font-display font-extrabold text-lg text-emerald-600 whitespace-nowrap">{formatPrice(price)}</span>
                  <a
                    href={productUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 text-[0.8125rem] font-bold text-white no-underline px-3.5 py-2 rounded-lg bg-gradient-to-br from-indigo-600 to-indigo-500 whitespace-nowrap shadow-[0_2px_6px_rgba(79,70,229,0.25)] transition-all duration-200 hover:from-indigo-700 hover:to-indigo-600 hover:shadow-[0_4px_10px_rgba(79,70,229,0.35)] hover:translate-x-0.5"
                  >
                    Git
                    <svg className="w-[14px] h-[14px]" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                      <polyline points="15 3 21 3 21 9" />
                      <line x1="10" y1="14" x2="21" y2="3" />
                    </svg>
                  </a>
                </div>
              );
            })}
          </div>
        </div>

        {/* Right Side: Similar Products */}
        {!loading && !error && similarProducts.length > 0 && (
          <div className="w-full md:w-[300px] border-t md:border-t-0 md:border-l border-slate-200 pt-6 md:pt-0 md:pl-6">
            <h3 className="font-display text-lg font-bold text-slate-900 mb-4">Buna Benzer Ürünler</h3>
            <div className="flex flex-col gap-4">
              {similarProducts.map((sp, idx) => {
                const spUrl = sp.ProductUrl ?? sp.productUrl;
                return (
                  <div key={idx} className="scale-95 origin-top-left hover:scale-100 transition-transform duration-200">
                    <a
                      href={spUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="flex gap-3 items-center border border-slate-100 rounded-xl p-3 hover:border-indigo-300 hover:bg-indigo-50/40 transition-all duration-200 bg-slate-50 cursor-pointer shadow-sm hover:shadow no-underline block text-left"
                    >
                      {sp.ImageUrl || sp.imageUrl ? (
                        <img src={sp.ImageUrl ?? sp.imageUrl} className="w-16 h-16 object-contain rounded-md bg-white border border-slate-200 p-1" />
                      ) : (
                        <div className="w-16 h-16 bg-white border border-slate-200 rounded-md"></div>
                      )}
                      <div className="flex-1">
                        <p className="text-[0.65rem] font-bold text-indigo-600 uppercase tracking-wider">{sp.Brand ?? sp.brand}</p>
                        <h4 className="text-sm font-semibold text-slate-900 line-clamp-2 leading-snug">{sp.CanonicalTitle ?? sp.canonicalTitle}</h4>
                        <p className="font-display text-emerald-600 font-bold text-sm mt-1">{formatPrice(sp.LowestPrice ?? sp.lowestPrice)}</p>
                      </div>
                    </a>
                  </div>
                );
              })}
            </div>
          </div>
        )}

      </div>
    </div>
  );
}
