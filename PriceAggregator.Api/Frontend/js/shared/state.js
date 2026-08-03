export const state = {
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

export const PAGE_SIZE = 30;

// Mutated in place (categories.length = 0; categories.push(...)) rather than reassigned,
// so every module that imports this binding keeps seeing the live list — see
// categoryMenu.js's loadCategories.
export const categories = [];
