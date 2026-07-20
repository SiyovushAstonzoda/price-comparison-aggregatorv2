const API_URL = "http://localhost:5196/api/products"; // replace 5xxx with your actual API port

async function loadProducts() {
    const container = document.getElementById("products");
    container.innerHTML = "Loading...";

    try {
        const res = await fetch(API_URL);
        const products = await res.json();

        container.innerHTML = "";
        products.forEach(p => {
            const card = document.createElement("div");
            card.className = "card";
            card.innerHTML = `
        <img src="${p.imageUrl ?? ''}" alt="${p.title}" />
        <h3>${p.title}</h3>
        <p class="price">${p.price.toFixed(2)} TL</p>
        ${p.regularPrice > p.price ? `<p class="old-price">${p.regularPrice.toFixed(2)} TL</p>` : ''}
        <a href="${p.productUrl}" target="_blank">View on ${p.source}</a>
      `;
            container.appendChild(card);
        });
    } catch (err) {
        container.innerHTML = "Failed to load products.";
        console.error(err);
    }
}

loadProducts();