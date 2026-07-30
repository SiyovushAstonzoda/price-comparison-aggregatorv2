import React, { useState, useEffect, useCallback } from 'react';
import Header from './components/Header';
import FilterPanel from './components/FilterPanel';
import ActiveFilters from './components/ActiveFilters';
import ProductGrid from './components/ProductGrid';
import ProductModal from './components/ProductModal';

function getApiBase() {
  const port = window.location.port;
  if (port === "5196" || port === "7249") {
    return `${window.location.origin}/api`;
  }
  return "http://localhost:5196/api";
}

const API_BASE = getApiBase();

function getTabEndpoints(tab) {
  if (tab === "mobilya") {
    return { products: "/mobilya/products", brands: "/mobilya/brands" };
  }
  return { products: "/products", brands: "/brands" };
}

export default function App() {
  const [activeTab, setActiveTab] = useState("market"); // "market" | "mobilya"

  // Input states (draft)
  const [searchInput, setSearchInput] = useState("");
  const [brand, setBrand] = useState("");
  const [sort, setSort] = useState("name");
  const [minPrice, setMinPrice] = useState("");
  const [maxPrice, setMaxPrice] = useState("");

  // Applied state for querying API
  const [appliedFilters, setAppliedFilters] = useState({
    q: "",
    brand: "",
    sort: "name",
    minPrice: "",
    maxPrice: "",
  });

  // Data states
  const [brands, setBrands] = useState([]);
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  // Modal state
  const [selectedProduct, setSelectedProduct] = useState(null);

  // Load brands when activeTab changes
  useEffect(() => {
    const { brands: brandsEndpoint } = getTabEndpoints(activeTab);
    fetch(`${API_BASE}${brandsEndpoint}`)
      .then((res) => {
        if (!res.ok) throw new Error("Failed to fetch brands");
        return res.json();
      })
      .then((data) => setBrands(data))
      .catch((err) => console.error("Failed to load brands:", err));
  }, [activeTab]);

  // Reset filters when tab changes
  const handleTabChange = (newTab) => {
    if (newTab === activeTab) return;
    setActiveTab(newTab);
    setSearchInput("");
    setBrand("");
    setSort("name");
    setMinPrice("");
    setMaxPrice("");
    setAppliedFilters({
      q: "",
      brand: "",
      sort: "name",
      minPrice: "",
      maxPrice: "",
    });
  };

  // Fetch products whenever appliedFilters or activeTab change
  const fetchProducts = useCallback(() => {
    setLoading(true);
    setError(false);

    const params = new URLSearchParams();
    if (appliedFilters.q) params.set("q", appliedFilters.q);
    if (appliedFilters.brand) params.set("brand", appliedFilters.brand);
    if (appliedFilters.sort) params.set("sort", appliedFilters.sort);
    if (appliedFilters.minPrice) params.set("minPrice", appliedFilters.minPrice);
    if (appliedFilters.maxPrice) params.set("maxPrice", appliedFilters.maxPrice);

    const queryStr = params.toString();
    const { products: productsEndpoint } = getTabEndpoints(activeTab);
    const url = queryStr ? `${API_BASE}${productsEndpoint}?${queryStr}` : `${API_BASE}${productsEndpoint}`;

    fetch(url)
      .then((res) => {
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        return res.json();
      })
      .then((data) => {
        setProducts(data);
        setLoading(false);
      })
      .catch((err) => {
        console.error(err);
        setError(true);
        setLoading(false);
      });
  }, [appliedFilters, activeTab]);

  useEffect(() => {
    fetchProducts();
  }, [fetchProducts]);

  // Handle Search Submit
  const handleSearchSubmit = (e, overrideQuery) => {
    if (e && e.preventDefault) e.preventDefault();
    const queryToApply = (overrideQuery !== undefined ? overrideQuery : searchInput).trim();
    setAppliedFilters((prev) => ({
      ...prev,
      q: queryToApply,
      brand,
      sort,
      minPrice,
      maxPrice,
    }));
  };

  // Handle Sort Change (immediate apply)
  const handleSortChange = (newSort) => {
    setSort(newSort);
    setAppliedFilters((prev) => ({
      ...prev,
      sort: newSort,
      q: searchInput.trim(),
      brand,
      minPrice,
      maxPrice,
    }));
  };

  // Handle Apply Filters Button
  const handleApplyFilters = () => {
    setAppliedFilters({
      q: searchInput.trim(),
      brand,
      sort,
      minPrice,
      maxPrice,
    });
  };

  // Handle Clear Filters Button
  const handleClearFilters = () => {
    setSearchInput("");
    setBrand("");
    setSort("name");
    setMinPrice("");
    setMaxPrice("");
    setAppliedFilters({
      q: "",
      brand: "",
      sort: "name",
      minPrice: "",
      maxPrice: "",
    });
  };

  // Handle Removing Individual Filter Tag
  const handleRemoveFilterTag = (key) => {
    if (key === "q") setSearchInput("");
    if (key === "brand") setBrand("");
    if (key === "minPrice") setMinPrice("");
    if (key === "maxPrice") setMaxPrice("");

    setAppliedFilters((prev) => ({
      ...prev,
      [key]: "",
    }));
  };

  // Results count text
  const resultsCountText = loading
    ? ""
    : error
    ? ""
    : products.length === 0
    ? "Sonuç bulunamadı"
    : `${products.length} ürün bulundu`;

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 font-sans antialiased">
      <Header
        searchInput={searchInput}
        setSearchInput={setSearchInput}
        onSearchSubmit={handleSearchSubmit}
        activeTab={activeTab}
        setActiveTab={handleTabChange}
        apiBase={API_BASE}
      />

      <main className="mx-auto max-w-[1280px] p-5 min-[841px]:p-8 grid grid-cols-1 min-[841px]:grid-cols-[260px_1fr] gap-8 items-start">
        <FilterPanel
          brands={brands}
          brand={brand}
          setBrand={setBrand}
          sort={sort}
          setSort={handleSortChange}
          minPrice={minPrice}
          setMinPrice={setMinPrice}
          maxPrice={maxPrice}
          setMaxPrice={setMaxPrice}
          onApplyFilters={handleApplyFilters}
          onClearFilters={handleClearFilters}
        />

        <section className="min-w-0">
          <div className="flex items-center justify-between flex-wrap gap-4 mb-6 py-1">
            <p id="resultsCount" className="font-display text-base font-semibold text-slate-600">
              {resultsCountText}
            </p>
            <ActiveFilters
              filters={appliedFilters}
              onRemoveFilter={handleRemoveFilterTag}
            />
          </div>

          <ProductGrid
            loading={loading}
            error={error}
            products={products}
            state={appliedFilters}
            onSelectProduct={setSelectedProduct}
          />
        </section>
      </main>

      <ProductModal
        product={selectedProduct}
        onClose={() => setSelectedProduct(null)}
        apiBase={API_BASE}
      />
    </div>
  );
}
