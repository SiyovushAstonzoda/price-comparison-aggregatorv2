import type { Section } from "../types";

interface HeaderProps {
  section: Section;
  onSectionChange: (section: Section) => void;
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  onSearch: (e: React.FormEvent) => void;
}

export default function Header({
  section,
  onSectionChange,
  searchQuery,
  onSearchQueryChange,
  onSearch,
}: HeaderProps) {
  return (
    <header className="site-header">
      <div className="header-inner">
        <a href="/" className="logo">
          <span className="logo-icon">₺</span>
          <span className="logo-text">Tutumlu</span>
        </a>

        <nav className="nav-tabs" role="tablist">
          <button
            type="button"
            className={`nav-tab${section === "market" ? " active" : ""}`}
            role="tab"
            aria-selected={section === "market"}
            onClick={() => onSectionChange("market")}
          >
            <span className="tab-icon">🛒</span> Süpermarketler
          </button>
          <button
            type="button"
            id="tabMobilya"
            className={`nav-tab${section === "mobilya" ? " active" : ""}`}
            role="tab"
            aria-selected={section === "mobilya"}
            onClick={() => onSectionChange("mobilya")}
          >
            <span className="tab-icon">🛋️</span> Mobilya
          </button>
        </nav>

        <form className="search-bar" role="search" onSubmit={onSearch}>
          <svg className="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.35-4.35" />
          </svg>
          <input
            type="search"
            placeholder={
              section === "market"
                ? "Ürün veya marka ara..."
                : "IKEA veya Özdilek ürünü ara (sandalye, masa, nevresim...)"
            }
            value={searchQuery}
            onChange={(e) => onSearchQueryChange(e.target.value)}
            autoComplete="off"
          />
          <button type="submit" className="search-btn">
            Ara
          </button>
        </form>
      </div>
    </header>
  );
}
