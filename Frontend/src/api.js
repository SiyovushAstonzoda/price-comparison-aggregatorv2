/**
 * Centralized API client for Monolithic Cimri-like eCommerce platform.
 *
 * Port detection:
 *   5196 / 7249 → served directly by dotnet  → same-origin /api
 *   3000        → Vite dev server with proxy  → same-origin /api (proxy forwards to :5196)
 *   anything else → direct fallback
 */
function getApiBase() {
  const port = window.location.port;
  if (port === "5196" || port === "7249" || port === "3000") {
    return "/api";
  }
  return "http://localhost:5196/api";
}

export const API_BASE = getApiBase();

/**
 * Fetch unified products list with filtering.
 * Supports: q, categoryId, brand, sort, minPrice, maxPrice
 */
export async function fetchProducts(filters = {}) {
  const params = new URLSearchParams();
  if (filters.q) params.set("q", filters.q);
  if (filters.categoryId) params.set("categoryId", filters.categoryId);
  if (filters.brand) params.set("brand", filters.brand);
  if (filters.sort) params.set("sort", filters.sort);
  if (filters.minPrice) params.set("minPrice", filters.minPrice);
  if (filters.maxPrice) params.set("maxPrice", filters.maxPrice);

  const res = await fetch(`${API_BASE}/products?${params.toString()}`);
  if (!res.ok) throw new Error("Failed to fetch products");
  return res.json();
}

/**
 * Fetch hierarchical category tree (Parent -> Children).
 */
export async function fetchCategories() {
  const res = await fetch(`${API_BASE}/categories`);
  if (!res.ok) throw new Error("Failed to fetch categories");
  return res.json();
}

/**
 * Fetch distinct brands, optionally filtered by category.
 */
export async function fetchBrands(categoryId) {
  const params = new URLSearchParams();
  if (categoryId) params.set("categoryId", categoryId);
  const res = await fetch(`${API_BASE}/brands?${params.toString()}`);
  if (!res.ok) throw new Error("Failed to fetch brands");
  return res.json();
}

/**
 * Fetch all store offers (prices & links) for a given product ID.
 */
export async function fetchProductOffers(productId) {
  const res = await fetch(`${API_BASE}/products/${productId}`);
  if (!res.ok) throw new Error("Failed to fetch offers");
  return res.json();
}

export async function fetchSimilarProducts(productId) {
  const res = await fetch(`${API_BASE}/products/${productId}/similar`);
  if (!res.ok) throw new Error('Failed to fetch similar products');
  return res.json();
}