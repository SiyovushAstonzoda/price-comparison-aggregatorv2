import { $, escapeHtml, formatPrice, formatUnitPrice } from "../shared/utils.js";
import { API_BASE } from "../shared/api.js";

export async function openProductDetail(masterId, title) {
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

export function closeModal() {
  $("modal").classList.add("hidden");
  document.body.style.overflow = "";
}

export function initModal() {
  $("closeModal").addEventListener("click", closeModal);
  $("modal").querySelector(".modal-backdrop").addEventListener("click", closeModal);
}
