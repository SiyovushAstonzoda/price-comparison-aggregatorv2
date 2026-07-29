import { useEffect, useState } from "react";
import { fetchCategories } from "../api";

const CATEGORY_ICONS = {
  Su: "💧",
  Çay: "🍵",
  Kahve: "☕",
  Makarna: "🍝",
  Zeytinyağı: "🫒",
  Peynir: "🧀",
  Yumurta: "🥚",
  Kahvaltılık: "🍯",
};

export default function CategoryGrid({ onSelectCategory }) {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchCategories()
      .then(setCategories)
      .catch((err) => console.error(err))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return null;
  if (categories.length === 0) return null;

  return (
    <div className="mb-10">
      <h2 className="text-sm font-bold text-slate-500 uppercase tracking-wider mb-3">
        Kategoriler
      </h2>
      <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-8 gap-3">
        {categories.map((cat) => (
          <button
            key={cat.name}
            onClick={() => onSelectCategory(cat.id)}
            className="flex flex-col items-center gap-2 p-4 bg-white border border-slate-200
                       rounded-2xl hover:border-indigo-300 hover:shadow-md transition-all"
          >
            <span className="text-2xl">{cat.icon ?? "🛒"}</span>
            <span className="text-sm font-semibold text-slate-900">{cat.name}</span>
            <span className="text-xs text-slate-400">{cat.productCount} ürün</span>
          </button>
        ))}
      </div>
    </div>
  );
}