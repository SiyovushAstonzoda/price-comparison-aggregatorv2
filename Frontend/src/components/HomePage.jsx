import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { fetchProducts } from '../api';
import ProductCard from './ProductCard';
import ProductDetailModal from './ProductDetailModal';

export default function HomePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedProductId, setSelectedProductId] = useState(null);

  const categoryId = searchParams.get('category');
  const q = searchParams.get('q');
  const minPrice = searchParams.get('minPrice');
  const maxPrice = searchParams.get('maxPrice');
  const sort = searchParams.get('sort') || 'name_asc';

  const [localMin, setLocalMin] = useState(minPrice || '');
  const [localMax, setLocalMax] = useState(maxPrice || '');

  // Update local state when URL params change
  useEffect(() => {
    setLocalMin(minPrice || '');
    setLocalMax(maxPrice || '');
  }, [minPrice, maxPrice]);

  useEffect(() => {
    setLoading(true);
    fetchProducts({ categoryId, q, minPrice, maxPrice, sort })
      .then(setProducts)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [categoryId, q, minPrice, maxPrice, sort]);

  const applyFilters = () => {
    const params = new URLSearchParams(searchParams);
    if (localMin) params.set('minPrice', localMin);
    else params.delete('minPrice');
    
    if (localMax) params.set('maxPrice', localMax);
    else params.delete('maxPrice');
    
    setSearchParams(params);
  };

  const handleSortChange = (e) => {
    const params = new URLSearchParams(searchParams);
    params.set('sort', e.target.value);
    setSearchParams(params);
  };

  return (
    <div className="flex-1 p-6 overflow-y-auto bg-slate-50 flex flex-col h-[calc(100vh-73px)]">
      <div className="mb-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">
            {q ? `"${q}" için arama sonuçları` : (categoryId ? 'Kategori Sonuçları' : 'Tüm Ürünler')}
          </h1>
          <div className="text-sm text-slate-500 mt-1">{products.length} ürün listeleniyor</div>
        </div>

        {/* Filter and Sort Bar */}
        <div className="flex flex-wrap items-center gap-3 bg-white p-2 sm:p-3 rounded-xl border border-slate-200 shadow-sm">
          <div className="flex items-center gap-2">
            <input 
              type="number" 
              placeholder="En Az" 
              value={localMin}
              onChange={(e) => setLocalMin(e.target.value)}
              className="w-20 sm:w-24 px-3 py-1.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
            <span className="text-slate-400">-</span>
            <input 
              type="number" 
              placeholder="En Çok" 
              value={localMax}
              onChange={(e) => setLocalMax(e.target.value)}
              className="w-20 sm:w-24 px-3 py-1.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
            <button 
              onClick={applyFilters}
              className="px-3 py-1.5 bg-indigo-50 text-indigo-600 font-semibold rounded-lg hover:bg-indigo-100 transition-colors text-sm"
            >
              Uygula
            </button>
          </div>

          <div className="w-px h-6 bg-slate-200 hidden sm:block"></div>

          <select 
            value={sort}
            onChange={handleSortChange}
            className="px-3 py-1.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
          >
            <option value="name_asc">A'dan Z'ye</option>
            <option value="name_desc">Z'den A'ya</option>
            <option value="price_asc">En Düşük Fiyat</option>
            <option value="price_desc">En Yüksek Fiyat</option>
          </select>
        </div>
      </div>

      {loading ? (
        <div className="flex items-center justify-center flex-1 text-slate-400">Yükleniyor...</div>
      ) : products.length === 0 ? (
        <div className="flex items-center justify-center flex-1 text-slate-400">Aradığınız kriterlere uygun ürün bulunamadı.</div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-6">
          {products.map(product => (
            <ProductCard 
              key={product.id} 
              product={product} 
              onClick={() => setSelectedProductId(product.id)}
            />
          ))}
        </div>
      )}

      {selectedProductId && (
        <ProductDetailModal 
          productId={selectedProductId}
          onClose={() => setSelectedProductId(null)}
        />
      )}
    </div>
  );
}
