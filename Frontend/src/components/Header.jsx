import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';

export default function Header() {
  const [query, setQuery] = useState("");
  const navigate = useNavigate();

  const handleSearch = (e) => {
    e.preventDefault();
    if (query.trim()) {
      navigate(`/?q=${encodeURIComponent(query.trim())}`);
    } else {
      navigate(`/`);
    }
  };

  return (
    <header className="sticky top-0 z-[100] bg-white border-b border-slate-200 shadow-sm">
      <div className="max-w-[1400px] mx-auto px-6 h-[72px] flex items-center gap-8">
        <Link to="/" className="group flex items-center gap-2.5 no-underline shrink-0">
          <span className="flex items-center justify-center w-10 h-10 bg-gradient-to-br from-indigo-600 to-indigo-500 text-white rounded-xl font-display font-extrabold text-xl shadow-md">
            ₺
          </span>
          <span className="font-display text-2xl font-extrabold tracking-tight text-slate-800">
            Tutumlu
          </span>
        </Link>
        <div className="flex-1 max-w-2xl">
          <form onSubmit={handleSearch} className="relative flex items-center">
            <input 
              type="text"
              value={query}
              onChange={e => setQuery(e.target.value)}
              placeholder="Ürün, marka veya kategori ara..."
              className="w-full h-11 pl-4 pr-12 rounded-lg border border-slate-300 bg-slate-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all text-slate-700"
            />
            <button type="submit" className="absolute right-3 p-2 text-slate-400 hover:text-indigo-600">
              <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24">
                <circle cx="11" cy="11" r="8"></circle>
                <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
              </svg>
            </button>
          </form>
        </div>
      </div>
    </header>
  );
}
