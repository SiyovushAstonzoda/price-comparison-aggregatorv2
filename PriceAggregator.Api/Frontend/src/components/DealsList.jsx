import { useState, useMemo } from "react";
import { fetchDeals } from "../api";
import DealCard from "./DealCard";
import ProductDetailModal from "./ProductDetailModal";
import CategoryGrid from "./CategoryGrid";

const SECTION_LABELS = {
  weight_or_volume: "Ağırlık / Hacme Göre En İyi Fiyat",
  count: "Adete Göre En İyi Fiyat",
};

const OTHER_LABELS = {
  weight_or_volume: "Adetle Satılanlar",
  count: "Ağırlık / Hacimle Satılanlar",
};

const SORT_OPTIONS = [
  { value: "unitPrice", label: "Birim Fiyata Göre (En Ucuz)" },
  { value: "price", label: "Fiyata Göre (En Ucuz)" },
  { value: "priceDesc", label: "Fiyata Göre (En Pahalı)" },
];

function applySortAndFilter(list, { selectedBrands, sortBy }) {
  let result = list;

  if (selectedBrands.size > 0) {
    result = result.filter((d) => selectedBrands.has(d.brand));
  }

  result = [...result].sort((a, b) => {
    if (sortBy === "price") return a.price - b.price;
    if (sortBy === "priceDesc") return b.price - a.price;
    return a.pricePerUnit - b.pricePerUnit;
  });

  return result;
}

export default function DealsList() {
  const [query, setQuery] = useState("");
  const [activeLabel, setActiveLabel] = useState("");
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [selectedMasterId, setSelectedMasterId] = useState(null);
  const [selectedBrands, setSelectedBrands] = useState(new Set());
  const [sortBy, setSortBy] = useState("unitPrice");

  async function runSearch({ q, categoryId }) {
    setLoading(true);
    setError(null);
    setSelectedBrands(new Set());
    setActiveLabel(categoryId ?? q);

    try {
      const result = await fetchDeals({ q, categoryId });
      setData(result);
    } catch (err) {
      setError("Arama başarısız oldu.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  function handleSearch(e) {
    e.preventDefault();
    if (!query.trim()) return;
    runSearch({ q: query.trim() });
  }

  function handleCategoryClick(categoryId) {
    setQuery("");
    runSearch({ categoryId });
  }

  const availableBrands = useMemo(() => {
    if (!data?.results) return [];
    const brands = new Set(data.results.map((d) => d.brand).filter(Boolean));
    return [...brands].sort();
  }, [data]);

  const filteredPrimary = useMemo(() => {
    if (!data?.results) return [];
    return applySortAndFilter(data.results, { selectedBrands, sortBy });
  }, [data, selectedBrands, sortBy]);

  const sortedSecondary = useMemo(() => {
    if (!data?.otherUnitResults) return [];
    return applySortAndFilter(data.otherUnitResults, { selectedBrands: new Set(), sortBy });
  }, [data, sortBy]);

  function toggleBrand(brand) {
    setSelectedBrands((prev) => {
      const next = new Set(prev);
      if (next.has(brand)) next.delete(brand);
      else next.add(brand);
      return next;
    });
  }

  const hasResults = data && (data.results?.length > 0 || data.otherUnitResults?.length > 0);

  return (
    <div className="max-w-6xl mx-auto px-4 py-8">
      <form onSubmit={handleSearch} className="flex gap-2 mb-6">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Örn: su, çay, makarna"
          className="flex-1 px-4 py-2.5 rounded-xl border border-slate-300 text-sm
                     focus:outline-none focus:ring-2 focus:ring-indigo-400 focus:border-transparent"
        />
        <button
          type="submit"
          className="px-5 py-2.5 rounded-xl bg-indigo-600 text-white text-sm font-semibold
                     hover:bg-indigo-700 transition-colors"
        >
          Ara
        </button>
      </form>

      {!data && !loading && <CategoryGrid onSelectCategory={handleCategoryClick} />}

      {loading && <p className="text-slate-500">Yükleniyor...</p>}
      {error && <p className="text-red-600">{error}</p>}
      {!loading && !error && data && !hasResults && (
        <p className="text-slate-500">Sonuç bulunamadı.</p>
      )}

      {!loading && !error && data && hasResults && (
        <div className="flex gap-8">
          {availableBrands.length > 0 && (
            <aside className="w-48 shrink-0">
              <h3 className="text-sm font-bold text-slate-900 mb-3">Marka</h3>
              <div className="flex flex-col gap-2">
                {availableBrands.map((brand) => (
                  <label
                    key={brand}
                    className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer"
                  >
                    <input
                      type="checkbox"
                      checked={selectedBrands.has(brand)}
                      onChange={() => toggleBrand(brand)}
                      className="rounded border-slate-300 text-indigo-600 focus:ring-indigo-400"
                    />
                    {brand}
                  </label>
                ))}
              </div>
              {selectedBrands.size > 0 && (
                <button
                  onClick={() => setSelectedBrands(new Set())}
                  className="text-xs text-indigo-600 font-semibold mt-3 hover:underline"
                >
                  Filtreleri temizle
                </button>
              )}
            </aside>
          )}

          <div className="flex-1 min-w-0">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-bold text-slate-900">
                {SECTION_LABELS[data.primaryUnit]}
              </h2>
              <select
                value={sortBy}
                onChange={(e) => setSortBy(e.target.value)}
                className="text-sm border border-slate-300 rounded-lg px-3 py-1.5
                           focus:outline-none focus:ring-2 focus:ring-indigo-400"
              >
                {SORT_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>

            {filteredPrimary.length === 0 ? (
              <p className="text-slate-500">Seçili markalarla sonuç bulunamadı.</p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mb-10">
                {filteredPrimary.map((deal, index) => (
                  <DealCard
                    key={`${deal.masterProductId}-${deal.source}-${deal.productUrl}`}
                    deal={deal}
                    rank={sortBy === "unitPrice" ? index + 1 : null}
                    onClick={() => setSelectedMasterId(deal.masterProductId)}
                  />
                ))}
              </div>
            )}

            {sortedSecondary.length > 0 && (
              <section>
                <h2 className="text-lg font-bold text-slate-900 mb-1">
                  {OTHER_LABELS[data.primaryUnit]}
                </h2>
                <p className="text-sm text-slate-500 mb-4">
                  Bu ürünler farklı bir birimle satıldığı için yukarıdaki listeyle karşılaştırılamaz.
                </p>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                  {sortedSecondary.map((deal) => (
                    <DealCard
                      key={`${deal.masterProductId}-${deal.source}-${deal.productUrl}`}
                      deal={deal}
                      rank={null}
                      onClick={() => setSelectedMasterId(deal.masterProductId)}
                    />
                  ))}
                </div>
              </section>
            )}
          </div>
        </div>
      )}

      {selectedMasterId && (
        <ProductDetailModal
          masterId={selectedMasterId}
          onClose={() => setSelectedMasterId(null)}
        />
      )}
    </div>
  );
}