import React from 'react';

export default function Header({ searchInput, setSearchInput, onSearchSubmit, activeRoute }) {
  const isCosmetics = activeRoute === '#kozmetik';
  const isHome = !isCosmetics;

  const navLinkBase = "relative px-3 py-1.5 text-sm font-semibold rounded-lg transition-all duration-200 no-underline";
  const activeLink  = "text-indigo-600 bg-indigo-50";
  const inactiveLink = "text-slate-600 hover:text-indigo-600 hover:bg-slate-100";

  // Nav linkler kozmetik için pembe ton kullanır
  const cosmeticsActive  = "text-rose-600 bg-rose-50";
  const cosmeticsInactive = "text-slate-600 hover:text-rose-600 hover:bg-rose-50";

  return (
    <header className="sticky top-0 z-[100] bg-white/85 backdrop-blur-md border-b border-slate-200/80 shadow-xs">
      <div className="max-w-[1280px] mx-auto px-6 min-[841px]:h-[72px] h-auto p-4 flex flex-col min-[841px]:flex-row items-center gap-3.5 min-[841px]:gap-8">
        {/* Logo */}
        <a href="/" className="group flex items-center gap-2.5 no-underline text-slate-900 shrink-0">
          <span className="flex items-center justify-center w-10 h-10 bg-gradient-to-br from-indigo-600 to-indigo-500 text-white rounded-xl font-display font-extrabold text-xl shadow-[0_4px_12px_rgba(79,70,229,0.3)] transition-transform duration-200 group-hover:scale-[1.08] group-hover:-rotate-4">
            ₺
          </span>
          <span className="font-display text-[1.4rem] font-extrabold tracking-tight bg-gradient-to-br from-slate-900 to-slate-700 bg-clip-text text-transparent">
            Tutumlu
          </span>
        </a>

        {/* Navigasyon */}
        <nav className="flex items-center gap-1 shrink-0" aria-label="Ana navigasyon">
          <a
            id="nav-home"
            href="#"
            className={`${navLinkBase} ${isHome ? activeLink : inactiveLink}`}
            aria-current={isHome ? 'page' : undefined}
          >
            🏠 Tümü
          </a>
          <a
            id="nav-kozmetik"
            href="#kozmetik"
            className={`${navLinkBase} ${isCosmetics ? cosmeticsActive : cosmeticsInactive}`}
            aria-current={isCosmetics ? 'page' : undefined}
          >
            ✨ Kozmetik
          </a>
        </nav>

        {/* Arama — sadece ana sayfada göster */}
        {isHome && (
          <form
            id="searchForm"
            className="group/search flex-1 max-w-[580px] w-full flex items-center bg-white border-[1.5px] border-slate-200 rounded-xl p-1 pl-3.5 shadow-xs transition-all duration-200 focus-within:border-indigo-600 focus-within:ring-4 focus-within:ring-indigo-600/12"
            role="search"
            onSubmit={onSearchSubmit}
          >
            <svg className="w-5 h-5 text-slate-400 shrink-0 transition-colors duration-200 group-focus-within/search:text-indigo-600" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <path d="m21 21-4.35-4.35" />
            </svg>
            <input
              id="searchInput"
              type="search"
              placeholder="Ürün veya marka ara..."
              autoComplete="off"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="flex-1 border-0 bg-transparent px-3 py-2.5 text-[0.9375rem] font-sans text-slate-900 outline-none placeholder:text-slate-400"
            />
            <button
              type="submit"
              className="bg-gradient-to-br from-indigo-600 to-indigo-500 text-white border-0 rounded-lg px-5 py-2.5 text-sm font-bold font-sans cursor-pointer shadow-[0_2px_8px_rgba(79,70,229,0.25)] transition-all duration-200 hover:from-indigo-700 hover:to-indigo-600 hover:shadow-[0_4px_12px_rgba(79,70,229,0.35)] hover:-translate-y-0.5 active:translate-y-0"
            >
              Ara
            </button>
          </form>
        )}
      </div>
    </header>
  );
}
