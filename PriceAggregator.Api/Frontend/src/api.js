/**
 * Centralized API client.
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

export async function fetchDeals({ q, categoryId }) {
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (categoryId) params.set("categoryId", categoryId);

  const res = await fetch(`${API_BASE}/deals?${params.toString()}`);
  if (!res.ok) throw new Error("Failed to fetch deals");
  return res.json();
}

export async function fetchCategories() {
  const res = await fetch(`${API_BASE}/categories`);
  if (!res.ok) throw new Error("Failed to fetch categories");
  return res.json();
}

/**
 * Fetch all store offers for a given master product ID.
 * Returns PascalCase objects (untyped Dapper QueryAsync).
 */
export async function fetchProductOffers(masterId) {
  const res = await fetch(`${API_BASE}/products/${masterId}`);
  if (!res.ok) throw new Error("Failed to fetch offers");
  return res.json();
}
