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

const state = {
  q: "",
  brand: "",
  sort: "name",
  minPrice: "",
  maxPrice: "",
};

const $ = (id) => document.getElementById(id);

function formatPrice(price) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  }).format(price);
}

function buildQueryParams() {
  const params = new URLSearchParams();
  if (state.q) params.set("q", state.q);
  if (state.brand) params.set("brand", state.brand);
  if (state.sort) params.set("sort", state.sort);
  if (state.minPrice) params.set("minPrice", state.minPrice);
  if (state.maxPrice) params.set("maxPrice", state.maxPrice);
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
    <div class="product-image-wrap">${imageHtml}</div>
    <div class="product-body">
      ${product.Brand ? `<p class="product-brand">${escapeHtml(product.Brand)}</p>` : ""}
      <h3 class="product-title">${escapeHtml(product.CanonicalTitle)}</h3>
      <div class="product-footer">
        <div>
          <span class="product-price-label">En düşük fiyat</span>
          <span class="product-price">${formatPrice(product.LowestPrice)}</span>
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
  if (state.minPrice) tags.push({ key: "minPrice", label: `Min ${state.minPrice} TL` });
  if (state.maxPrice) tags.push({ key: "maxPrice", label: `Max ${state.maxPrice} TL` });

  tags.forEach(({ key, label }) => {
    const tag = document.createElement("span");
    tag.className = "filter-tag";
    tag.innerHTML = `${escapeHtml(label)} <button type="button" aria-label="Kaldır">×</button>`;
    tag.querySelector("button").addEventListener("click", () => {
      state[key] = "";
      syncFilterInputs();
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
}

function readFilterInputs() {
  state.q = $("searchInput").value.trim();
  state.brand = $("brandFilter").value;
  state.sort = $("sortFilter").value;
  state.minPrice = $("minPrice").value;
  state.maxPrice = $("maxPrice").value;
}

async function loadBrands() {
  try {
    const res = await fetch(`${API_BASE}/brands`);
    const brands = await res.json();
    const select = $("brandFilter");
    brands.forEach((brand) => {
      const opt = document.createElement("option");
      opt.value = brand;
      opt.textContent = brand;
      select.appendChild(opt);
    });
  } catch (err) {
    console.error("Failed to load brands:", err);
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
    const products = await res.json();

    container.className = "product-grid";
    container.innerHTML = "";

    $("resultsCount").textContent =
      products.length === 0
        ? "Sonuç bulunamadı"
        : `${products.length} ürün bulundu`;

    if (products.length === 0) {
      showEmptyState(
        container,
        state.q || state.brand || state.minPrice || state.maxPrice
          ? "Filtreleri değiştirmeyi veya aramayı temizlemeyi deneyin."
          : "Henüz ürün eklenmemiş."
      );
      return;
    }

    products.forEach((p) => container.appendChild(renderProductCard(p)));
  } catch (err) {
    showErrorState(container);
    $("resultsCount").textContent = "";
    console.error(err);
  }
}

async function openProductDetail(masterId, title) {
  const modal = $("modal");
  const offersContainer = $("offers");
  $("modalTitle").textContent = title;
  offersContainer.innerHTML = `<div class="state-message"><p>Yükleniyor...</p></div>`;
  modal.classList.remove("hidden");
  document.body.style.overflow = "hidden";

  try {
    const res = await fetch(`${API_BASE}/products/${masterId}`);
    const offers = await res.json();

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
        <span class="offer-price">${formatPrice(offer.Price)}</span>
        <a href="${escapeHtml(offer.ProductUrl)}" target="_blank" rel="noopener" class="offer-link">
          Git
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>
            <polyline points="15 3 21 3 21 9"/>
            <line x1="10" y1="14" x2="21" y2="3"/>
          </svg>
        </a>`;
      offersContainer.appendChild(row);
    });
  } catch (err) {
    offersContainer.innerHTML = `<div class="state-message"><p>Fiyatlar yüklenemedi.</p></div>`;
    console.error(err);
  }
}

function closeModal() {
  $("modal").classList.add("hidden");
  document.body.style.overflow = "";
}

function clearFilters() {
  state.q = "";
  state.brand = "";
  state.sort = "name";
  state.minPrice = "";
  state.maxPrice = "";
  syncFilterInputs();
  loadProducts();
}

$("searchForm").addEventListener("submit", (e) => {
  e.preventDefault();
  readFilterInputs();
  loadProducts();
});

$("applyFilters").addEventListener("click", () => {
  readFilterInputs();
  loadProducts();
});

$("sortFilter").addEventListener("change", () => {
  readFilterInputs();
  loadProducts();
});

$("clearFilters").addEventListener("click", clearFilters);
$("closeModal").addEventListener("click", closeModal);
$("modal").querySelector(".modal-backdrop").addEventListener("click", closeModal);

document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") closeModal();
});

loadBrands();
loadProducts();
