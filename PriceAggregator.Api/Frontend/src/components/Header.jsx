import React, { useState, useEffect, useRef } from 'react';

export default function Header({ searchInput, setSearchInput, onSearchSubmit, activeTab, setActiveTab, apiBase }) {
  const [suggestions, setSuggestions] = useState([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const dropdownRef = useRef(null);

  useEffect(() => {
    if (!searchInput || searchInput.trim().length < 2) {
      setSuggestions([]);
      return;
    }

    const timer = setTimeout(() => {
      const base = apiBase || "http://localhost:5196/api";
      fetch(`${base}/suggestions?q=${encodeURIComponent(searchInput.trim())}&tab=${activeTab}`)
        .then((res) => res.json())
        .then((data) => {
          setSuggestions(Array.isArray(data) ? data : []);
          setShowSuggestions(true);
        })
        .catch(() => setSuggestions([]));
    }, 200);

    return () => clearTimeout(timer);
  }, [searchInput, activeTab, apiBase]);

  // Hide suggestions when clicking outside
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setShowSuggestions(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSuggestionClick = (itemText) => {
    setSearchInput(itemText);
    setShowSuggestions(false);
    onSearchSubmit({ preventDefault: () => {}, target: { value: itemText } }, itemText);
  };

  return (
    <header className="sticky top-0 z-[100] bg-white/85 backdrop-blur-md border-b border-slate-200/80 shadow-xs">
      <div className="max-w-[1280px] mx-auto px-6 min-[841px]:h-[72px] h-auto p-4 flex flex-col min-[841px]:flex-row items-center justify-between gap-3.5 min-[841px]:gap-6">
        <div className="flex items-center gap-6 w-full min-[841px]:w-auto justify-between min-[841px]:justify-start">
          <a href="/" className="group flex items-center gap-2.5 no-underline text-slate-900 shrink-0">
            <span className="flex items-center justify-center w-10 h-10 bg-gradient-to-br from-indigo-600 to-indigo-500 text-white rounded-xl font-display font-extrabold text-xl shadow-[0_4px_12px_rgba(79,70,229,0.3)] transition-transform duration-200 group-hover:scale-[1.08] group-hover:-rotate-4">
              ₺
            </span>
            <span className="font-display text-[1.4rem] font-extrabold tracking-tight bg-gradient-to-br from-slate-900 to-slate-700 bg-clip-text text-transparent">
              Tutumlu
            </span>
          </a>

          <nav className="flex items-center bg-slate-100 p-1 rounded-xl">
            <button
              type="button"
              onClick={() => setActiveTab('market')}
              className={`px-3 py-1.5 rounded-lg text-sm font-bold transition-all duration-200 cursor-pointer ${
                activeTab === 'market'
                  ? 'bg-white text-indigo-600 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              🛒 Süpermarket
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('mobilya')}
              className={`px-4 py-1.5 rounded-lg text-sm font-bold transition-all duration-200 cursor-pointer ${
                activeTab === 'mobilya'
                  ? 'bg-white text-indigo-600 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              🛋️ Mobilya
            </button>
          </nav>
        </div>

        <div className="relative flex-1 max-w-[520px] w-full" ref={dropdownRef}>
          <form id="searchForm" className="group/search w-full flex items-center bg-white border-[1.5px] border-slate-200 rounded-xl p-1 pl-3.5 shadow-xs transition-all duration-200 focus-within:border-indigo-600 focus-within:ring-4 focus-within:ring-indigo-600/12" role="search" onSubmit={(e) => { setShowSuggestions(false); onSearchSubmit(e); }}>
            <svg className="w-5 h-5 text-slate-400 shrink-0 transition-colors duration-200 group-focus-within/search:text-indigo-600" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <path d="m21 21-4.35-4.35" />
            </svg>
            <input
              id="searchInput"
              type="search"
              placeholder={activeTab === 'mobilya' ? "IKEA veya Özdilek ürünü ara (sandalye, masa, tekstil...)" : "Ürün veya marka ara..."}
              autoComplete="off"
              value={searchInput}
              onFocus={() => { if (suggestions.length > 0) setShowSuggestions(true); }}
              onChange={(e) => setSearchInput(e.target.value)}
              className="flex-1 border-0 bg-transparent px-3 py-2.5 text-[0.9375rem] font-sans text-slate-900 outline-none placeholder:text-slate-400"
            />
            <button type="submit" className="bg-gradient-to-br from-indigo-600 to-indigo-500 text-white border-0 rounded-lg px-5 py-2.5 text-sm font-bold font-sans cursor-pointer shadow-[0_2px_8px_rgba(79,70,229,0.25)] transition-all duration-200 hover:from-indigo-700 hover:to-indigo-600 hover:shadow-[0_4px_12px_rgba(79,70,229,0.35)] hover:-translate-y-0.5 active:translate-y-0">
              Ara
            </button>
          </form>

          {showSuggestions && suggestions.length > 0 && (
            <ul className="absolute left-0 right-0 top-[calc(100%+6px)] bg-white border border-slate-200 rounded-xl shadow-xl z-[120] overflow-hidden py-1.5 list-none m-0">
              <li className="px-4 py-1.5 text-[0.6875rem] font-bold uppercase tracking-wider text-slate-400 pointer-events-none">
                Öneriler
              </li>
              {suggestions.map((item, idx) => (
                <li key={idx}>
                  <button
                    type="button"
                    onClick={() => handleSuggestionClick(item)}
                    className="w-full text-left px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-indigo-50 hover:text-indigo-600 transition-colors duration-150 flex items-center gap-2.5 cursor-pointer border-0 bg-transparent"
                  >
                    <svg className="w-4 h-4 text-slate-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <circle cx="11" cy="11" r="8" />
                      <path d="m21 21-4.35-4.35" />
                    </svg>
                    <span className="line-clamp-1">{item}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </header>
  );
}
