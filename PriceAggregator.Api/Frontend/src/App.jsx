import React from 'react';
import DealsList from './components/DealsList';

/**
 * App — thin shell.
 *
 * The Header handles branding only (no search state here; search lives
 * inside DealsList so it can own its own loading/results cycle).
 */
export default function App() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 font-sans antialiased">
      {/* Sticky top nav — logo only */}
      <header className="sticky top-0 z-[100] bg-white/85 backdrop-blur-md border-b border-slate-200/80 shadow-xs">
        <div className="max-w-[1280px] mx-auto px-6 h-[64px] flex items-center">
          <a href="/" className="group flex items-center gap-2.5 no-underline text-slate-900 shrink-0">
            <span className="flex items-center justify-center w-10 h-10 bg-gradient-to-br from-indigo-600 to-indigo-500 text-white rounded-xl font-display font-extrabold text-xl shadow-[0_4px_12px_rgba(79,70,229,0.3)] transition-transform duration-200 group-hover:scale-[1.08] group-hover:-rotate-4">
              ₺
            </span>
            <span className="font-display text-[1.4rem] font-extrabold tracking-tight bg-gradient-to-br from-slate-900 to-slate-700 bg-clip-text text-transparent">
              Tutumlu
            </span>
          </a>
        </div>
      </header>

      <main>
        <DealsList />
      </main>
    </div>
  );
}
