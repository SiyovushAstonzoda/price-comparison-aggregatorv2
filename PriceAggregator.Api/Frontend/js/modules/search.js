import { $, escapeHtml } from "../shared/utils.js";
import { API_BASE } from "../shared/api.js";
import { state } from "../shared/state.js";
import { syncFilterInputs, renderActiveFilters, loadSearchCategories, readFilterInputs } from "./filters.js";
import { loadProducts } from "./productGrid.js";
import { openProductDetail } from "./modal.js";

// Type-ahead dropdown under the search bar: as the shopper types, shows matching
// categories/brands/products so they can jump straight there instead of always landing on
// a full-text product search.
let searchSuggestionsDebounce = null;

function closeSearchSuggestions() {
  $("searchSuggestions").innerHTML = "";
  $("searchSuggestions").classList.remove("open");
}

function renderSearchSuggestions(data) {
  const container = $("searchSuggestions");
  container.innerHTML = "";

  const hasAny = data.Brands.length > 0 || data.Products.length > 0;
  if (!hasAny) {
    closeSearchSuggestions();
    return;
  }

  if (data.Brands.length > 0) {
    const section = document.createElement("div");
    section.className = "search-suggestions-section";
    section.innerHTML = `<p class="search-suggestions-label">Markalar</p>`;
    data.Brands.forEach((brand) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "search-suggestion-item";
      btn.innerHTML = `${escapeHtml(brand)} <span class="search-suggestion-tag">Marka</span>`;
      btn.addEventListener("click", () => {
        closeSearchSuggestions();
        $("searchInput").value = "";
        state.q = "";
        state.brand = brand;
        loadSearchCategories("");
        syncFilterInputs();
        renderActiveFilters();
        loadProducts();
      });
      section.appendChild(btn);
    });
    container.appendChild(section);
  }

  if (data.Products.length > 0) {
    const section = document.createElement("div");
    section.className = "search-suggestions-section";
    section.innerHTML = `<p class="search-suggestions-label">Önerilen Ürünler</p>`;
    data.Products.forEach((p) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "search-suggestion-product";
      const imageHtml = p.ImageUrl
        ? `<img src="${escapeHtml(p.ImageUrl)}" alt="" loading="lazy">`
        : `<span class="search-suggestion-product-placeholder"></span>`;
      btn.innerHTML = `${imageHtml}<span>${escapeHtml(p.CanonicalTitle)}</span>`;
      btn.addEventListener("click", () => {
        closeSearchSuggestions();
        openProductDetail(p.Id, p.CanonicalTitle);
      });
      section.appendChild(btn);
    });
    container.appendChild(section);
  }

  container.classList.add("open");
}

async function loadSearchSuggestions(term) {
  try {
    const res = await fetch(`${API_BASE}/search-suggestions?q=${encodeURIComponent(term)}`);
    const data = await res.json();
    // The input may have changed (or been cleared) while this request was in flight.
    if ($("searchInput").value.trim() === term) renderSearchSuggestions(data);
  } catch (err) {
    console.error("Failed to load search suggestions:", err);
  }
}

export function initSearchBox() {
  $("searchForm").addEventListener("submit", (e) => {
    e.preventDefault();
    closeSearchSuggestions();
    readFilterInputs();
    loadSearchCategories(state.q);
    loadProducts();
  });

  $("searchInput").addEventListener("input", () => {
    const term = $("searchInput").value.trim();
    clearTimeout(searchSuggestionsDebounce);

    if (term.length < 2) {
      closeSearchSuggestions();
      return;
    }

    searchSuggestionsDebounce = setTimeout(() => loadSearchSuggestions(term), 250);
  });

  $("searchInput").addEventListener("keydown", (e) => {
    if (e.key === "Escape") closeSearchSuggestions();
  });

  document.addEventListener("click", (e) => {
    const wrap = document.querySelector(".search-bar-wrap");
    if (!wrap.contains(e.target)) closeSearchSuggestions();
  });
}
