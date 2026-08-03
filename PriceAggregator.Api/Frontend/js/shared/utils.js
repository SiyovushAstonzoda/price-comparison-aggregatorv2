export const $ = (id) => document.getElementById(id);

export function formatPrice(price) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    minimumFractionDigits: 2,
  }).format(price);
}

// UnitPrice/UnitLabel are only present when the title had a parseable size (see
// SizeParser/UnitPriceCalculator on the API side) — absent for e.g. furniture.
export function formatUnitPrice(unitPrice, unitLabel) {
  if (unitPrice == null || !unitLabel) return "";
  return `<span class="product-unit-price">${formatPrice(unitPrice)} / ${escapeHtml(unitLabel)}</span>`;
}

export function escapeHtml(text) {
  const div = document.createElement("div");
  div.textContent = text ?? "";
  return div.innerHTML;
}
