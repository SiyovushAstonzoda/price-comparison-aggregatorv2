function getApiBase() {
  const port = window.location.port;
  // Served by dotnet — API is on the same origin
  if (port === "5196" || port === "7249") {
    return `${window.location.origin}/api`;
  }
  // Opened via Live Server / Five Server / file preview
  return "http://localhost:5196/api";
}

const API_BASE = getApiBase();

const PAGE_SIZE = 30;

const state = {
  q: "",
  brand: "",
  category: "",
  sort: "name",
  minPrice: "",
  maxPrice: "",
  attrs: [], // ["Renk:Bej", "Özellik:Organik", ...]
  onSale: false,
  allProducts: [],
  page: 1,
};

const $ = (id) => document.getElementById(id);

let categories = [];

// Level 1 categories (e.g. Market's "Atıştırmalık", "İçecek") that the current search word's
// results actually sit in — powers the filter panel's category facet, see loadSearchCategories.
let searchCategoryFacet = [];

function formatPrice(price) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  }).format(price);
}

// UnitPrice/UnitLabel are only present when the title had a parseable size (see
// SizeParser/UnitPriceCalculator on the API side) — absent for e.g. furniture.
function formatUnitPrice(unitPrice, unitLabel) {
  if (unitPrice == null || !unitLabel) return "";
  return `<span class="product-unit-price">${formatPrice(unitPrice)} / ${escapeHtml(unitLabel)}</span>`;
}

function buildQueryParams() {
  const params = new URLSearchParams();
  if (state.q) params.set("q", state.q);
  if (state.brand) params.set("brand", state.brand);
  if (state.category) params.set("category", state.category);
  if (state.sort) params.set("sort", state.sort);
  if (state.minPrice) params.set("minPrice", state.minPrice);
  if (state.maxPrice) params.set("maxPrice", state.maxPrice);
  if (state.onSale) params.set("onSale", "true");
  state.attrs.forEach((a) => params.append("attr", a));
  return params.toString();
}

function showSkeletonLoading(container) {
  container.className = "product-grid";
  container.innerHTML = Array(8)
    .fill(
      `<div class="skeleton-card">
        <div class="skeleton-image"></div>
        <div class="skeleton-body">
          <div class="skeleton-line short"></div>
          <div class="skeleton-line long"></div>
          <div class="skeleton-line medium"></div>
        </div>
      </div>`
    )
    .join("");
}

function showEmptyState(container, message) {
  container.className = "product-grid";
  container.innerHTML = `
    <div class="state-message">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
        <circle cx="11" cy="11" r="8"/>
        <path d="m21 21-4.35-4.35"/>
      </svg>
      <h3>Ürün bulunamadı</h3>
      <p>${message}</p>
    </div>`;
}

function showErrorState(container) {
  container.className = "product-grid";
  container.innerHTML = `
    <div class="state-message">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
        <circle cx="12" cy="12" r="10"/>
        <path d="M12 8v4M12 16h.01"/>
      </svg>
      <h3>Bir hata oluştu</h3>
      <p>Ürünler yüklenemedi. API'nin çalıştığından emin olun.</p>
    </div>`;
}

function renderProductCard(product) {
  const card = document.createElement("article");
  card.className = "product-card";
  card.setAttribute("role", "button");
  card.tabIndex = 0;

  const imageHtml = product.ImageUrl
    ? `<img src="${escapeHtml(product.ImageUrl)}" class="product-image" alt="${escapeHtml(product.CanonicalTitle)}" loading="lazy">`
    : `<svg class="product-image-placeholder" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
         <rect x="3" y="3" width="18" height="18" rx="2"/>
         <circle cx="8.5" cy="8.5" r="1.5"/>
         <path d="m21 15-5-5L5 21"/>
       </svg>`;

  card.innerHTML = `
    <div class="product-image-wrap">
      ${imageHtml}
      ${product.DiscountPercent ? `<span class="product-discount-badge">-%${product.DiscountPercent}</span>` : ""}
    </div>
    <div class="product-body">
      ${product.Brand ? `<p class="product-brand">${escapeHtml(product.Brand)}</p>` : ""}
      <h3 class="product-title">${escapeHtml(product.CanonicalTitle)}</h3>
      <div class="product-footer">
        <div>
          <span class="product-price-label">En düşük fiyat</span>
          ${product.DiscountPercent ? `<span class="product-old-price">${formatPrice(product.OldPrice)}</span>` : ""}
          <span class="product-price">${formatPrice(product.LowestPrice)}</span>
          ${formatUnitPrice(product.UnitPrice, product.UnitLabel)}
        </div>
        <span class="product-stores">${product.OfferCount} mağaza</span>
      </div>
    </div>`;

  const open = () => openProductDetail(product.Id, product.CanonicalTitle);
  card.addEventListener("click", open);
  card.addEventListener("keydown", (e) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      open();
    }
  });

  return card;
}

function escapeHtml(text) {
  const div = document.createElement("div");
  div.textContent = text ?? "";
  return div.innerHTML;
}

function renderActiveFilters() {
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

function syncFilterInputs() {
  $("searchInput").value = state.q;
  $("brandFilter").value = state.brand;
  $("sortFilter").value = state.sort;
  $("minPrice").value = state.minPrice;
  $("maxPrice").value = state.maxPrice;
  $("onSaleFilter").checked = state.onSale;
  $("newFilter").checked = state.sort === "newest";
}

function readFilterInputs() {
  state.q = $("searchInput").value.trim();
  state.brand = $("brandFilter").value;
  state.sort = $("sortFilter").value;
  state.minPrice = $("minPrice").value;
  state.maxPrice = $("maxPrice").value;
  state.onSale = $("onSaleFilter").checked;
}

async function loadBrands(category) {
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
async function loadAttributes(category) {
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

function buildCategoryChildrenMap() {
  const map = new Map();
  categories.forEach((c) => {
    const key = c.ParentCategoryId ?? "root";
    if (!map.has(key)) map.set(key, []);
    map.get(key).push(c);
  });
  return map;
}

const CATEGORY_ICONS = [
  // Level 0 (sector) labels — checked first since a couple of Level 1 names could
  // otherwise collide with these (e.g. neither "market" nor "mobilya" appear elsewhere,
  // but keeping sector icons distinct and first avoids relying on that by accident).
  { keywords: ["market"], icon: "🛒" },
  { keywords: ["mobilya"], icon: "🛋️" },
  { keywords: ["meyve", "sebze"], icon: "🥦" },
  { keywords: ["süt", "kahvaltı"], icon: "🥛" },
  { keywords: ["et,", "et &", "tavuk", "balık", "şarküteri"], icon: "🥩" },
  { keywords: ["fırın", "pastane", "ekmek"], icon: "🥐" },
  { keywords: ["içecek"], icon: "🧃" },
  { keywords: ["temel gıda", "kuru gıda", "bakliyat", "yağ"], icon: "🌾" },
  { keywords: ["atıştırmalık", "çerez", "cips"], icon: "🍿" },
  { keywords: ["dondur"], icon: "❄️" },
  { keywords: ["temizlik"], icon: "🧴" },
  { keywords: ["kişisel bakım", "kozmetik"], icon: "🪥" },
  { keywords: ["bebek"], icon: "🍼" },
  // Was previously reached only by the "Market"/"Mobilya" sector-root labels, which never
  // showed up as menu rows themselves. Now that their real children (İçecek, Kozmetik,
  // Koltuk, Yatak, ...) are promoted to Level 1 (see renderMegaMenu), these siblings from
  // the same real category set need their own icons too.
  { keywords: ["elektronik"], icon: "📱" },
  { keywords: ["ev, yaşam", "ev &"], icon: "🏠" },
  { keywords: ["kitap"], icon: "📚" },
  { keywords: ["aydınlatma", "lamba"], icon: "💡" },
  { keywords: ["koltuk", "kanepe"], icon: "🛋️" },
  { keywords: ["sandalye", "tabure"], icon: "🪑" },
  { keywords: ["yatak"], icon: "🛏️" },
  { keywords: ["halı"], icon: "🧶" },
  { keywords: ["dolap", "depolama"], icon: "🗄️" },
  { keywords: ["masa"], icon: "🍽️" },
  { keywords: ["ayakkabı"], icon: "👞" },
  { keywords: ["yastık", "tekstil"], icon: "🧵" },
  { keywords: ["dekoratif", "aksesuar"], icon: "🖼️" },
];
const DEFAULT_CATEGORY_ICON = "📦";

function getCategoryIcon(name) {
  const normalized = (name || "").toLocaleLowerCase("tr-TR");
  const match = CATEGORY_ICONS.find(({ keywords }) => keywords.some((k) => normalized.includes(k)));
  return match ? match.icon : DEFAULT_CATEGORY_ICON;
}

// 4-level mega menu state: Level-0 (sector, e.g. Market/Mobilya) drives which Level-1
// (category) list shows, which drives Level-2 (sub), which drives what Level-3 (item)
// column shows.
let activeSectorId = null;
let activeRootId = null;
let activeSubId = null;

// Walks from `cat` up to its sector root (Market/Mobilya — the real top of the tree),
// returning [sectorRoot, ...ancestors..., cat].
function getCategoryChain(cat) {
  const chain = [cat];
  let current = cat;
  while (current.ParentCategoryId != null) {
    const parent = categories.find((c) => c.Id === current.ParentCategoryId);
    if (!parent) break;
    chain.unshift(parent);
    current = parent;
  }
  return chain;
}

function initMegaMenuActiveFromSelection() {
  if (!state.category) return;
  const selected = categories.find((c) => c.Slug === state.category);
  if (!selected) return;

  const chain = getCategoryChain(selected);
  // chain[0] is always the sector root, even when selected IS the sector root itself
  // (chain.length === 1) — e.g. products CategoryResolver couldn't classify past the
  // sector level (see CategoryClassifier's null-match fallback).
  activeSectorId = chain[0].Id;
  activeRootId = chain.length >= 2 ? chain[1].Id : null;
  activeSubId = chain.length >= 3 ? chain[2].Id : null;
}

function renderMegaMenu() {
  const byParent = buildCategoryChildrenMap();

  // Resolve the active path top-down: Level 0 (sector) -> Level 1 -> Level 2, each
  // constrained to be a real child of the previous one (falls back to the first option
  // whenever the current selection doesn't belong under the newly active parent).
  const sectors = (byParent.get("root") || []).slice().sort((a, b) => a.Name.localeCompare(b.Name, "tr"));

  if ((activeSectorId == null || !sectors.some((s) => s.Id === activeSectorId)) && sectors.length) {
    activeSectorId = sectors[0].Id;
  }
  const currentSector = sectors.find((s) => s.Id === activeSectorId) ?? null;

  const roots = currentSector
    ? (byParent.get(currentSector.Id) || []).slice().sort((a, b) => a.Name.localeCompare(b.Name, "tr"))
    : [];

  if ((activeRootId == null || !roots.some((r) => r.Id === activeRootId)) && roots.length) {
    activeRootId = roots[0].Id;
  }
  const currentRoot = roots.find((r) => r.Id === activeRootId) ?? null;

  const subs = currentRoot
    ? (byParent.get(currentRoot.Id) || []).slice().sort((a, b) => a.Name.localeCompare(b.Name, "tr"))
    : [];
  if (activeSubId != null && !subs.some((s) => s.Id === activeSubId)) activeSubId = null;
  const currentSub = subs.find((s) => s.Id === activeSubId) ?? null;

  const items = currentSub
    ? (byParent.get(currentSub.Id) || []).slice().sort((a, b) => a.Name.localeCompare(b.Name, "tr"))
    : [];

  // Level 0
  const col0 = document.querySelector("#categoryMegaMenu .mega-col-0");
  col0.innerHTML = "";
  sectors.forEach((sector) => {
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "mega-l0-item" + (sector.Id === activeSectorId ? " active" : "");
    btn.dataset.slug = sector.Slug;
    btn.innerHTML = `
      <span class="mega-l0-icon">${getCategoryIcon(sector.Name)}</span>
      <span class="mega-l0-label">${escapeHtml(sector.Name)}</span>`;
    btn.addEventListener("mouseenter", () => {
      if (activeSectorId !== sector.Id) {
        activeSectorId = sector.Id;
        activeRootId = null;
        activeSubId = null;
        renderMegaMenu();
      }
    });
    btn.addEventListener("click", () => selectCategory(sector.Slug));
    li.appendChild(btn);
    col0.appendChild(li);
  });

  // Level 1
  const col1 = document.querySelector("#categoryMegaMenu .mega-col-1");
  col1.innerHTML = "";
  roots.forEach((root) => {
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "mega-l1-item" + (root.Id === activeRootId ? " active" : "");
    btn.dataset.slug = root.Slug;
    btn.innerHTML = `
      <span class="mega-l1-icon">${getCategoryIcon(root.Name)}</span>
      <span class="mega-l1-label">${escapeHtml(root.Name)}</span>
      <svg class="mega-l1-arrow" viewBox="0 0 12 12" fill="none"><path d="M4 2.5L8 6L4 9.5" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></svg>`;
    btn.addEventListener("mouseenter", () => {
      if (activeRootId !== root.Id) {
        activeRootId = root.Id;
        activeSubId = null;
        renderMegaMenu();
      }
    });
    btn.addEventListener("click", () => selectCategory(root.Slug));
    li.appendChild(btn);
    col1.appendChild(li);
  });

  // Level 2
  document.querySelector("#categoryMegaMenu .mega-col-2-title").textContent = currentRoot ? currentRoot.Name : "";
  const col2List = document.querySelector("#categoryMegaMenu .mega-col-2-list");
  col2List.innerHTML = "";
  subs.forEach((sub) => {
    const childCount = (byParent.get(sub.Id) || []).length;
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "mega-l2-item" + (sub.Id === activeSubId ? " active" : "");
    btn.dataset.slug = sub.Slug;
    btn.innerHTML = `
      <span class="mega-l2-label">${escapeHtml(sub.Name)}</span>
      <span class="mega-l2-meta">
        ${childCount ? `<span class="mega-l2-count">${childCount}</span>` : ""}
        <svg viewBox="0 0 10 10" fill="none"><path d="M3 2L7 5L3 8" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>`;
    btn.addEventListener("mouseenter", () => {
      if (activeSubId !== sub.Id) {
        activeSubId = sub.Id;
        renderMegaMenu();
      }
    });
    btn.addEventListener("click", () => selectCategory(sub.Slug));
    li.appendChild(btn);
    col2List.appendChild(li);
  });

  // Level 3
  const col3 = document.querySelector("#categoryMegaMenu .mega-col-3");
  col3.innerHTML = "";
  if (currentSub) {
    const breadcrumb = document.createElement("div");
    breadcrumb.className = "mega-breadcrumb";
    breadcrumb.innerHTML = `
      <span>${getCategoryIcon(currentRoot.Name)}</span>
      <span>${escapeHtml(currentRoot.Name)}</span>
      <svg viewBox="0 0 10 10" fill="none"><path d="M3 2L7 5L3 8" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round"/></svg>
      <span class="mega-breadcrumb-current">${escapeHtml(currentSub.Name)}</span>`;
    col3.appendChild(breadcrumb);

    const title = document.createElement("p");
    title.className = "mega-col-3-title";
    title.textContent = `${currentSub.Name} ürünleri`;
    col3.appendChild(title);

    // currentSub is sometimes a leaf itself — its products are filed directly on it, not on
    // some further subcategory (e.g. "Su" has no children, the 65 water products sit on
    // "Su" itself). Falling back to [currentSub] here means it still shows up as one normal
    // clickable entry instead of leaving the column blank.
    const displayItems = items.length > 0 ? items : [currentSub];

    const grid = document.createElement("ul");
    grid.className = "mega-item-grid";
    displayItems.forEach((item) => {
      const li = document.createElement("li");
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "mega-l3-item";
      btn.dataset.slug = item.Slug;
      btn.innerHTML = `<span class="mega-l3-dot"></span>${escapeHtml(item.Name)}`;
      btn.addEventListener("click", () => selectCategory(item.Slug));
      li.appendChild(btn);
      grid.appendChild(li);
    });
    col3.appendChild(grid);
  } else {
    const empty = document.createElement("div");
    empty.className = "mega-empty-state";
    empty.innerHTML = `
      <span class="mega-empty-icon">${currentRoot ? getCategoryIcon(currentRoot.Name) : DEFAULT_CATEGORY_ICON}</span>
      <p>Bir alt kategori seçin</p>`;
    col3.appendChild(empty);
  }

  syncCategorySelectionHighlight();
}

function selectCategory(slug) {
  state.category = slug;
  closeCategoryMenu();
  syncCategorySelectionHighlight();
  renderSearchCategoryFilter();
  loadBrands(slug);
  loadAttributes(slug);
  loadProducts();
}

// Fetches which Level 1 categories the given search word's matches sit in, then re-renders
// the filter panel's category facet. Scoped to q alone (ignores brand/price/attr/onSale) so
// the facet always reflects "categories this word can be found in", not the currently applied
// filter combo.
async function loadSearchCategories(q) {
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

function renderSearchCategoryFilter() {
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

function syncCategorySelectionHighlight() {
  document
    .querySelectorAll(".mega-l0-item, .mega-l1-item, .mega-l2-item, .mega-l3-item")
    .forEach((el) => el.classList.toggle("selected", el.dataset.slug === state.category));
}

function openCategoryMenu() {
  initMegaMenuActiveFromSelection();
  renderMegaMenu();
  $("categoryMegaMenu").classList.add("open");
  $("categoryMenuTrigger").classList.add("open");
}

function closeCategoryMenu() {
  $("categoryMegaMenu").classList.remove("open");
  $("categoryMenuTrigger").classList.remove("open");
}

async function loadCategories() {
  try {
    const res = await fetch(`${API_BASE}/categories`);
    categories = await res.json();
    renderMegaMenu();
  } catch (err) {
    console.error("Failed to load categories:", err);
  }
}

async function loadProducts() {
  const container = $("products");
  showSkeletonLoading(container);
  renderActiveFilters();

  try {
    const query = buildQueryParams();
    const url = query ? `${API_BASE}/products?${query}` : `${API_BASE}/products`;
    const res = await fetch(url);
    if (!res.ok) {
      throw new Error(`API returned ${res.status}`);
    }
    state.allProducts = await res.json();
    state.page = 1;
    renderProductsPage();
  } catch (err) {
    showErrorState(container);
    $("resultsCount").textContent = "";
    $("pagination").innerHTML = "";
    console.error(err);
  }
}

// Renders the current page slice of state.allProducts (already fetched in full by
// loadProducts) plus the pagination controls — keeps products from stacking endlessly
// down a single page.
function renderProductsPage() {
  const container = $("products");
  const products = state.allProducts;

  container.className = "product-grid";
  container.innerHTML = "";

  $("resultsCount").textContent =
    products.length === 0
      ? "Sonuç bulunamadı"
      : `${products.length} ürün bulundu`;

  if (products.length === 0) {
    $("pagination").innerHTML = "";
    showEmptyState(
      container,
      state.q || state.brand || state.category || state.minPrice || state.maxPrice
        ? "Filtreleri değiştirmeyi veya aramayı temizlemeyi deneyin."
        : "Henüz ürün eklenmemiş."
    );
    return;
  }

  const start = (state.page - 1) * PAGE_SIZE;
  products.slice(start, start + PAGE_SIZE).forEach((p) => container.appendChild(renderProductCard(p)));

  renderPagination(products.length);
}

// Builds [1, "...", 4, 5, 6, "...", 20]-style page lists so large result sets don't render
// a button per page.
function getPageNumbers(current, total) {
  const pages = [];
  for (let i = 1; i <= total; i++) {
    if (i === 1 || i === total || Math.abs(i - current) <= 1) pages.push(i);
  }

  const withEllipsis = [];
  let previous = null;
  pages.forEach((i) => {
    if (previous !== null && i - previous > 1) withEllipsis.push("...");
    withEllipsis.push(i);
    previous = i;
  });
  return withEllipsis;
}

function renderPagination(totalItems) {
  const container = $("pagination");
  container.innerHTML = "";

  const totalPages = Math.ceil(totalItems / PAGE_SIZE);
  if (totalPages <= 1) return;

  const goToPage = (page) => {
    if (page < 1 || page > totalPages || page === state.page) return;
    state.page = page;
    renderProductsPage();
    $("products").scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const prevBtn = document.createElement("button");
  prevBtn.type = "button";
  prevBtn.className = "pagination-btn pagination-nav";
  prevBtn.textContent = "‹ Önceki";
  prevBtn.disabled = state.page === 1;
  prevBtn.addEventListener("click", () => goToPage(state.page - 1));
  container.appendChild(prevBtn);

  getPageNumbers(state.page, totalPages).forEach((p) => {
    if (p === "...") {
      const span = document.createElement("span");
      span.className = "pagination-ellipsis";
      span.textContent = "…";
      container.appendChild(span);
      return;
    }

    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "pagination-btn" + (p === state.page ? " active" : "");
    btn.textContent = p;
    btn.addEventListener("click", () => goToPage(p));
    container.appendChild(btn);
  });

  const nextBtn = document.createElement("button");
  nextBtn.type = "button";
  nextBtn.className = "pagination-btn pagination-nav";
  nextBtn.textContent = "Sonraki ›";
  nextBtn.disabled = state.page === totalPages;
  nextBtn.addEventListener("click", () => goToPage(state.page + 1));
  container.appendChild(nextBtn);
}

async function openProductDetail(masterId, title) {
  const modal = $("modal");
  const offersContainer = $("offers");
  const similarContainer = $("similarProducts");
  $("modalTitle").textContent = title;
  offersContainer.innerHTML = `<div class="state-message"><p>Yükleniyor...</p></div>`;
  similarContainer.innerHTML = `<div class="state-message"><p>Yükleniyor...</p></div>`;
  modal.classList.remove("hidden");
  document.body.style.overflow = "hidden";

  const offersLoaded = fetch(`${API_BASE}/products/${masterId}`)
    .then((res) => res.json())
    .then((offers) => {
      offersContainer.innerHTML = "";
      offers.forEach((offer, index) => {
        const row = document.createElement("div");
        row.className = "offer-row";
        row.innerHTML = `
          ${offer.ImageUrl ? `<img src="${escapeHtml(offer.ImageUrl)}" alt="${escapeHtml(offer.Source)}">` : ""}
          <div class="offer-store">
            <div class="offer-source">${escapeHtml(offer.Source)}</div>
            ${index === 0 ? '<span class="offer-badge">En ucuz</span>' : ""}
          </div>
          <div class="offer-price-wrap">
            <span class="offer-price">${formatPrice(offer.Price)}</span>
            ${formatUnitPrice(offer.UnitPrice, offer.UnitLabel)}
          </div>
          <a href="${escapeHtml(offer.ProductUrl)}" target="_blank" rel="noopener noreferrer" class="offer-link">
            Git
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>
              <polyline points="15 3 21 3 21 9"/>
              <line x1="10" y1="14" x2="21" y2="3"/>
            </svg>
          </a>`;
        offersContainer.appendChild(row);
      });
    })
    .catch((err) => {
      offersContainer.innerHTML = `<div class="state-message"><p>Fiyatlar yüklenemedi.</p></div>`;
      console.error(err);
    });

  const similarLoaded = fetch(`${API_BASE}/products/${masterId}/similar`)
    .then((res) => res.json())
    .then((products) => renderSimilarProducts(similarContainer, products))
    .catch((err) => {
      similarContainer.innerHTML = `<div class="state-message"><p>Benzer ürünler yüklenemedi.</p></div>`;
      console.error(err);
    });

  await Promise.all([offersLoaded, similarLoaded]);
}

function renderSimilarProductCard(p) {
  const card = document.createElement("div");
  card.className = "similar-product-card";
  card.setAttribute("role", "button");
  card.tabIndex = 0;

  const imageHtml = p.ImageUrl
    ? `<img src="${escapeHtml(p.ImageUrl)}" class="similar-product-image" alt="${escapeHtml(p.CanonicalTitle)}" loading="lazy">`
    : `<svg class="similar-product-image-placeholder" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
         <rect x="3" y="3" width="18" height="18" rx="2"/>
         <circle cx="8.5" cy="8.5" r="1.5"/>
         <path d="m21 15-5-5L5 21"/>
       </svg>`;

  card.innerHTML = `
    ${imageHtml}
    <div class="similar-product-info">
      <p class="similar-product-title">${escapeHtml(p.CanonicalTitle)}</p>
      <span class="similar-product-price">${formatPrice(p.LowestPrice)}</span>
    </div>`;

  const open = () => openProductDetail(p.Id, p.CanonicalTitle);
  card.addEventListener("click", open);
  card.addEventListener("keydown", (e) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      open();
    }
  });

  return card;
}

// Backend groups results into SameCategory (exact same leaf — e.g. other cheese types) and
// the rest of the widened bucket (e.g. jam/honey — still "kahvaltılık" but a different leaf).
// Rendered as two labeled groups so the mix doesn't look arbitrary.
function renderSimilarProducts(container, products) {
  if (products.length === 0) {
    container.innerHTML = `<div class="state-message"><p>Benzer ürün bulunamadı.</p></div>`;
    return;
  }

  container.innerHTML = "";

  const sameCategory = products.filter((p) => p.SameCategory);
  const related = products.filter((p) => !p.SameCategory);

  if (sameCategory.length > 0) {
    const heading = document.createElement("p");
    heading.className = "similar-group-heading";
    heading.textContent = "Aynı ürün grubu";
    container.appendChild(heading);
    sameCategory.forEach((p) => container.appendChild(renderSimilarProductCard(p)));
  }

  if (related.length > 0) {
    const heading = document.createElement("p");
    heading.className = "similar-group-heading";
    heading.textContent = "İlgili diğer ürünler";
    container.appendChild(heading);
    related.forEach((p) => container.appendChild(renderSimilarProductCard(p)));
  }
}

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

function closeModal() {
  $("modal").classList.add("hidden");
  document.body.style.overflow = "";
}

function clearFilters() {
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

$("categoryMenuTrigger").addEventListener("click", () => {
  if ($("categoryMegaMenu").classList.contains("open")) closeCategoryMenu();
  else openCategoryMenu();
});

document.addEventListener("click", (e) => {
  const menu = $("categoryMegaMenu");
  const trigger = $("categoryMenuTrigger");
  if (!menu.contains(e.target) && !trigger.contains(e.target)) {
    closeCategoryMenu();
  }
});

$("clearFilters").addEventListener("click", clearFilters);
$("closeModal").addEventListener("click", closeModal);
$("modal").querySelector(".modal-backdrop").addEventListener("click", closeModal);

document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") {
    closeCategoryMenu();
    closeModal();
  }



  
});

loadBrands();
loadAttributes();
loadCategories();
loadProducts();
