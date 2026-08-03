// Entry point — wires up each module's event listeners and kicks off the initial data
// load. No rendering/business logic lives here; that stays in the module it belongs to.
import { initFilters, loadBrands, loadAttributes } from "../modules/filters.js";
import { initCategoryMenu, loadCategories, closeCategoryMenu } from "../modules/categoryMenu.js";
import { initModal, closeModal } from "../modules/modal.js";
import { initSearchBox } from "../modules/search.js";
import { loadProducts } from "../modules/productGrid.js";

initFilters();
initCategoryMenu();
initModal();
initSearchBox();

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
