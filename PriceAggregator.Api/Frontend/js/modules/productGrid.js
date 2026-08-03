import { $, formatPrice, formatUnitPrice, escapeHtml } from "../shared/utils.js";
import { API_BASE } from "../shared/api.js";
import { state, PAGE_SIZE } from "../shared/state.js";
import { renderActiveFilters } from "./filters.js";
import { openProductDetail } from "./modal.js";

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

export async function loadProducts() {
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
