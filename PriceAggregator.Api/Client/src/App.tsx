import { useCallback, useEffect, useState } from "react";
import Header from "./components/Header";
import MarketSection from "./components/MarketSection";
import IkeaSection from "./components/IkeaSection";
import ProductModal from "./components/ProductModal";
import type { MarketFilters, Section } from "./types";

const defaultMarketFilters: MarketFilters = {
  q: "",
  brand: "",
  source: "",
  sort: "name",
  minPrice: "",
  maxPrice: "",
};

export default function App() {
  const [section, setSection] = useState<Section>("market");
  const [searchQuery, setSearchQuery] = useState("");
  const [marketFilters, setMarketFilters] = useState<MarketFilters>(defaultMarketFilters);
  const [ikeaSearch, setIkeaSearch] = useState("");
  const [modal, setModal] = useState<{ id: number; title: string } | null>(null);

  const handleSearch = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      if (section === "market") {
        setMarketFilters((prev) => ({ ...prev, q: searchQuery.trim() }));
      } else {
        setIkeaSearch(searchQuery.trim());
      }
    },
    [section, searchQuery]
  );

  useEffect(() => {
    setSearchQuery(section === "market" ? marketFilters.q : ikeaSearch);
  }, [section, marketFilters.q, ikeaSearch]);

  return (
    <>
      <Header
        section={section}
        onSectionChange={setSection}
        searchQuery={searchQuery}
        onSearchQueryChange={setSearchQuery}
        onSearch={handleSearch}
      />

      {section === "market" ? (
        <MarketSection
          filters={marketFilters}
          onFiltersChange={setMarketFilters}
          onProductClick={(id, title) => setModal({ id, title })}
        />
      ) : (
        <IkeaSection searchQuery={ikeaSearch} />
      )}

      {modal && (
        <ProductModal
          id={modal.id}
          title={modal.title}
          onClose={() => setModal(null)}
        />
      )}
    </>
  );
}
