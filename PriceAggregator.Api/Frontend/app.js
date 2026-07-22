const API_BASE = "http://localhost:5196/api"; // replace 5xxx with your actual API port

async function loadProducts() {
  const container = document.getElementById("products");
  container.innerHTML = "Loading...";

  try {
    const res = await fetch(`${API_BASE}/products`);
    const masterProducts = await res.json();

    container.innerHTML = "";

    if (masterProducts.length === 0) {
      container.innerHTML = "No products found.";
      return;
    }

  masterProducts.forEach(mp => {
  const card = document.createElement("div");
  card.className = "card";
  card.innerHTML = `
    <h3>${mp.CanonicalTitle}</h3>
    <p class="brand">${mp.Brand ?? ""}</p>
    <p class="price">From ${mp.LowestPrice.toFixed(2)} TL</p>
    <p class="offer-count">${mp.OfferCount} store${mp.OfferCount > 1 ? "s" : ""}</p>
  `;
  card.addEventListener("click", () => openProductDetail(mp.Id, mp.CanonicalTitle));
  container.appendChild(card);
  });
  } catch (err) {
    container.innerHTML = "Failed to load products.";
    console.error(err);
  }
}

async function openProductDetail(masterId, title) {
  const modal = document.getElementById("modal");
  const offersContainer = document.getElementById("offers");
  document.getElementById("modalTitle").textContent = title;
  offersContainer.innerHTML = "Loading...";
  modal.classList.remove("hidden");

  try {
    const res = await fetch(`${API_BASE}/products/${masterId}`);
    const offers = await res.json();

    offersContainer.innerHTML = "";
    offers.forEach(o => {
    const row = document.createElement("div");
    row.className = "offer-row";
    row.innerHTML = `
      <img src="${o.ImageUrl ?? ''}" alt="${o.Source}" />
      <span class="source">${o.Source}</span>
      <span class="offer-price">${o.Price.toFixed(2)} TL</span>
      <a href="${o.ProductUrl}" target="_blank">View →</a>
    `;
    offersContainer.appendChild(row);
  });
  } catch (err) {
    offersContainer.innerHTML = "Failed to load offers.";
    console.error(err);
  }
}

document.getElementById("closeModal").addEventListener("click", () => {
  document.getElementById("modal").classList.add("hidden");
});

loadProducts();