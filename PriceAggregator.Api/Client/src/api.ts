const API_BASE = "/api";

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`API returned ${res.status}`);
  return res.json() as Promise<T>;
}

export function formatPrice(price: number) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  }).format(price);
}

export function formatSource(source: string) {
  return source
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/^./, (c) => c.toUpperCase());
}

export async function getSources() {
  return fetchJson<string[]>(`${API_BASE}/sources`);
}

export async function getBrands() {
  return fetchJson<string[]>(`${API_BASE}/brands`);
}

export async function getMarketProducts(params: URLSearchParams) {
  const query = params.toString();
  const url = query ? `${API_BASE}/products?${query}` : `${API_BASE}/products`;
  return fetchJson<import("./types").MarketProduct[]>(url);
}

export async function getProductOffers(id: number) {
  return fetchJson<import("./types").ProductOffer[]>(`${API_BASE}/products/${id}`);
}

export async function getIkeaProducts(params: URLSearchParams) {
  const query = params.toString();
  const url = query ? `${API_BASE}/ikea/products?${query}` : `${API_BASE}/ikea/products`;
  return fetchJson<import("./types").IkeaProduct[]>(url);
}

export async function getIkeaFilters() {
  return fetchJson<import("./types").IkeaFilters>(`${API_BASE}/ikea/filters`);
}
