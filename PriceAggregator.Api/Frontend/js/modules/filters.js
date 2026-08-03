import { $, escapeHtml } from "../shared/utils.js";
import { API_BASE } from "../shared/api.js";
import { state, categories } from "../shared/state.js";
import { syncCategorySelectionHighlight, selectCategory, getCategoryChain } from "./categoryMenu.js";
import { loadProducts } from "./productGrid.js";

// Level 1 categories (e.g. Market's "Atıştırmalık", "İçecek") that the current search word's
// results actually sit in — powers the filter panel's category facet, see loadSearchCategories.
let searchCategoryFacet = [];

export function renderActiveFilters() {
  const container = $("activeFilters");
  container.innerHTML = "";

  const tags = [];
  if (state.q) tags.push({ key: "q", label: `"${state.q}"` });
  if (state.brand) tags.push({ key: "brand", label: state.brand });
  if (state.category) {
    const cat = categories.find((c) => c.Slug === state.category);
    tags.push({ key: "category", label: cat ? cat.Name : state.category });
  }
  if (state.minPrice) tags.push({ key: "minPrice", label: `Min ${state.minPrice} TL` });
  if (state.maxPrice) tags.push({ key: "maxPrice", label: `Max ${state.maxPrice} TL` });

  tags.forEach(({ key, label }) => {
    const tag = document.createElement("span");
    tag.className = "filter-tag";
    tag.innerHTML = `${escapeHtml(label)} <button type="button" aria-label="Kaldır">×</button>`;
    tag.querySelector("button").addEventListener("click", () => {
      state[key] = "";
      if (key === "category") {
        syncCategorySelectionHighlight();
        renderSearchCategoryFilter();
        loadBrands("");
        loadAttributes("");
      }
      if (key === "q") {
        loadSearchCategories("");
      }
      syncFilterInputs();
      loadProducts();
    });
    container.appendChild(tag);
  });

  if (state.onSale) {
    const tag = document.createElement("span");
    tag.className = "filter-tag";
    tag.innerHTML = `Kampanyalı <button type="button" aria-label="Kaldır">×</button>`;
    tag.querySelector("button").addEventListener("click", () => {
      state.onSale = false;
      syncFilterInputs();
      loadProducts();
    });
    container.appendChild(tag);
  }

  if (state.sort === "newest") {
    const tag = document.createElement("span");
    tag.className = "filter-tag";
    tag.innerHTML = `Yeni Ürünler <button type="button" aria-label="Kaldır">×</button>`;
    tag.querySelector("button").addEventListener("click", () => {
      state.sort = "name";
      syncFilterInputs();
      loadProducts();
    });
    container.appendChild(tag);
  }

  state.attrs.forEach((attrKey) => {
    const tag = document.createElement("span");
    tag.className = "filter-tag";
    tag.innerHTML = `${escapeHtml(attrKey.replace(":", ": "))} <button type="button" aria-label="Kaldır">×</button>`;
    tag.querySelector("button").addEventListener("click", () => {
      state.attrs = state.attrs.filter((a) => a !== attrKey);
      const checkbox = document.querySelector(`#attributeFilters input[value="${CSS.escape(attrKey)}"]`);
      if (checkbox) checkbox.checked = false;
      renderActiveFilters();
      loadProducts();
    });
    container.appendChild(tag);
  });
}

export function syncFilterInputs() {
  $("searchInput").value = state.q;
  $("brandFilter").value = state.brand;
  $("sortFilter").value = state.sort;
  $("minPrice").value = state.minPrice;
  $("maxPrice").value = state.maxPrice;
  $("onSaleFilter").checked = state.onSale;
  $("newFilter").checked = state.sort === "newest";
}

export function readFilterInputs() {
  state.q = $("searchInput").value.trim();
  state.brand = $("brandFilter").value;
  state.sort = $("sortFilter").value;
  state.minPrice = $("minPrice").value;
  state.maxPrice = $("maxPrice").value;
  state.onSale = $("onSaleFilter").checked;
}

export async function loadBrands(category) {
  try {
    const url = category ? `${API_BASE}/brands?category=${encodeURIComponent(category)}` : `${API_BASE}/brands`;
    const res = await fetch(url);
    const brands = await res.json();

    const select = $("brandFilter");
    const previousValue = select.value;
    select.innerHTML = '<option value="">Tüm markalar</option>';
    brands.forEach((brand) => {
      const opt = document.createElement("option");
      opt.value = brand;
      opt.textContent = brand;
      select.appendChild(opt);
    });

    // Keep the current brand selected if it's still valid for this category, else reset it.
    if (brands.includes(previousValue)) {
      select.value = previousValue;
    } else {
      select.value = "";
      state.brand = "";
    }
  } catch (err) {
    console.error("Failed to load brands:", err);
  }
}

// Renders one checkbox group per attribute name (e.g. "Renk" under Mobilya, "Özellik" under
// Market) — only the ones that actually have data for the current category, per
// /api/attributes. Nothing is hardcoded: a new attribute type showing up in the data starts
// rendering here automatically the next time this runs.
//
// Only rendered when a specific category is selected — on the "all products" screen there's
// no single coherent product group for a Renk/Özellik checkbox to mean anything (a "Bej"
// checkbox spanning cheese AND chairs isn't a real filter), so the whole block stays empty.
export async function loadAttributes(category) {
  const container = $("attributeFilters");

  if (!category) {
    container.innerHTML = "";
    state.attrs = [];
    return;
  }

  try {
    const url = `${API_BASE}/attributes?category=${encodeURIComponent(category)}`;
    const res = await fetch(url);
    const groups = await res.json();

    container.innerHTML = "";

    const validKeys = new Set();

    groups.forEach((group) => {
      const wrap = document.createElement("div");
      wrap.className = "filter-group";

      const label = document.createElement("label");
      label.textContent = group.Name;
      wrap.appendChild(label);

      const list = document.createElement("div");
      list.className = "attribute-checkbox-list";

      // Groups with more options than fit comfortably (Renk, Özellik, ...) stay collapsed to
      // the first few until the shopper asks for more, same idea as Temizle's plain-link style
      // (.clear-btn) rather than introducing a new button look.
      const COLLAPSE_AFTER = 5;

      group.Values.forEach((v, index) => {
        const key = `${group.Name}:${v.AttributeValue}`;
        validKeys.add(key);

        const item = document.createElement("label");
        item.className = "attribute-checkbox";
        item.innerHTML = `<input type="checkbox" value="${escapeHtml(key)}"> <span>${escapeHtml(v.AttributeValue)}</span> <span class="attribute-count">${v.ProductCount}</span>`;

        const checkbox = item.querySelector("input");
        checkbox.checked = state.attrs.includes(key);
        checkbox.addEventListener("change", () => {
          if (checkbox.checked) {
            if (!state.attrs.includes(key)) state.attrs.push(key);
          } else {
            state.attrs = state.attrs.filter((a) => a !== key);
          }
          renderActiveFilters();
          loadProducts();
        });

        if (index >= COLLAPSE_AFTER) item.hidden = true;
        list.appendChild(item);
      });

      wrap.appendChild(list);

      const hiddenCount = group.Values.length - COLLAPSE_AFTER;
      if (hiddenCount > 0) {
        const toggle = document.createElement("button");
        toggle.type = "button";
        toggle.className = "clear-btn attribute-show-more";
        toggle.textContent = `Tümünü Göster (+${hiddenCount})`;
        toggle.addEventListener("click", () => {
          const expanding = toggle.dataset.expanded !== "true";
          list.querySelectorAll(".attribute-checkbox").forEach((item, index) => {
            if (index >= COLLAPSE_AFTER) item.hidden = !expanding;
          });
          toggle.dataset.expanded = expanding ? "true" : "false";
          toggle.textContent = expanding ? "Daha Az Göster" : `Tümünü Göster (+${hiddenCount})`;
        });
        wrap.appendChild(toggle);
      }

      container.appendChild(wrap);
    });

    // Drop selections that no longer apply after switching category (e.g. Mobilya -> Market).
    state.attrs = state.attrs.filter((a) => validKeys.has(a));
  } catch (err) {
    console.error("Failed to load attributes:", err);
  }
}

// Fetches which Level 1 categories the given search word's matches sit in, then re-renders
// the filter panel's category facet. Scoped to q alone (ignores brand/price/attr/onSale) so
// the facet always reflects "categories this word can be found in", not the currently applied
// filter combo.
export async function loadSearchCategories(q) {
  const term = (q || "").trim();
  if (!term) {
    searchCategoryFacet = [];
    renderSearchCategoryFilter();
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/search-categories?q=${encodeURIComponent(term)}`);
    const rows = await res.json();

    const byLevel1 = new Map();
    rows.forEach(({ CategoryId, ProductCount }) => {
      const cat = categories.find((c) => c.Id === CategoryId);
      if (!cat) return;
      const chain = getCategoryChain(cat);
      const level1 = chain.length >= 2 ? chain[1] : null;
      if (!level1) return; // product filed directly on the sector root — no Level 1 to facet on

      const existing = byLevel1.get(level1.Id);
      if (existing) existing.Count += ProductCount;
      else byLevel1.set(level1.Id, { Slug: level1.Slug, Name: level1.Name, Count: ProductCount });
    });

    searchCategoryFacet = Array.from(byLevel1.values()).sort((a, b) => a.Name.localeCompare(b.Name, "tr"));
  } catch (err) {
    console.error("Failed to load search categories:", err);
    searchCategoryFacet = [];
  }

  renderSearchCategoryFilter();
}

export function renderSearchCategoryFilter() {
  const group = $("searchCategoryFilterGroup");
  const select = $("searchCategoryFilter");

  if (!state.q || searchCategoryFacet.length === 0) {
    group.hidden = true;
    return;
  }

  group.hidden = false;

  select.innerHTML = '<option value="">Tümü</option>';
  searchCategoryFacet.forEach((cat) => {
    const opt = document.createElement("option");
    opt.value = cat.Slug;
    opt.textContent = `${cat.Name} (${cat.Count})`;
    select.appendChild(opt);
  });
  select.value = searchCategoryFacet.some((c) => c.Slug === state.category) ? state.category : "";
}

export function clearFilters() {
  // Category is deliberately left alone — "Temizle" lives in the Filtreler panel, not the
  // category menu, so it should reset brand/price/sort/attrs without kicking the shopper
  // back out to "all products".
  state.q = "";
  state.brand = "";
  state.sort = "name";
  state.minPrice = "";
  state.maxPrice = "";
  state.attrs = [];
  state.onSale = false;
  loadSearchCategories("");
  syncFilterInputs();
  syncCategorySelectionHighlight();
  loadBrands(state.category);
  loadAttributes(state.category);
  loadProducts();
}

export function initFilters() {
  $("applyFilters").addEventListener("click", () => {
    readFilterInputs();
    loadProducts();
  });

  $("sortFilter").addEventListener("change", () => {
    readFilterInputs();
    syncFilterInputs(); // keep the "Sadece yeni ürünler" checkbox in sync with the sort select
    loadProducts();
  });

  $("searchCategoryFilter").addEventListener("change", () => {
    selectCategory($("searchCategoryFilter").value);
  });

  $("onSaleFilter").addEventListener("change", () => {
    state.onSale = $("onSaleFilter").checked;
    loadProducts();
  });

  $("newFilter").addEventListener("change", () => {
    state.sort = $("newFilter").checked ? "newest" : "name";
    syncFilterInputs();
    loadProducts();
  });

  $("clearFilters").addEventListener("click", clearFilters);
}
