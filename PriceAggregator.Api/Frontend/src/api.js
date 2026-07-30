<<<<<<< Updated upstream
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
=======
function getApiBase(){const port=window.location.port;if(port==="5196"||port==="7249"||port==="3000")return "/api";return "http://localhost:5196/api";}export const API_BASE=getApiBase();export async function fetchDeals(query){const r=await fetch(`${API_BASE}/deals?q=${encodeURIComponent(query)}`);if(!r.ok)throw new Error("Failed to fetch deals");return r.json();}export async function fetchProductOffers(id){const r=await fetch(`${API_BASE}/products/${id}`);if(!r.ok)throw new Error("Failed to fetch offers");return r.json();}export async function fetchFurnitureCategories(parentId){const q=parentId==null?"":`?parentId=${parentId}`;const r=await fetch(`${API_BASE}/categories${q}`);if(!r.ok)throw new Error("Categories failed");return r.json();}export async function fetchFurnitureProducts(id){const r=await fetch(`${API_BASE}/categories/${id}/products`);if(!r.ok)throw new Error("Products failed");return r.json();}export async function fetchBrands(categoryId=null){const q=categoryId==null?"":`?categoryId=${categoryId}`;const r=await fetch(`${API_BASE}/brands${q}`);if(!r.ok)throw new Error("Brands failed");return r.json();}export async function fetchAllProducts(brand=null){const q=brand?`?brand=${encodeURIComponent(brand)}`:"";const r=await fetch(`${API_BASE}/products${q}`);if(!r.ok)throw new Error("Products failed");return r.json();}
>>>>>>> Stashed changes
