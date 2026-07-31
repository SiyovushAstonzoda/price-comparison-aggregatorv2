import { useEffect, useState } from "react";
import { API_BASE } from "../api";

export default function AdminCategoryReview() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    loadItems();
  }, []);

  async function loadItems() {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/admin/category-review`);
      if (!res.ok) throw new Error("Failed to load");
      const data = await res.json();
      setItems(data);
    } catch (err) {
      setError("Yüklenemedi.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  async function handleAction(id, action) {
    try {
      const res = await fetch(`${API_BASE}/admin/category-review/${id}/${action}`, {
        method: "POST",
      });
      if (!res.ok) throw new Error("Action failed");
      setItems((prev) => prev.filter((item) => item.id !== id));
    } catch (err) {
      console.error(err);
      alert("İşlem başarısız oldu.");
    }
  }

  if (loading) return <p className="p-8 text-slate-500">Yükleniyor...</p>;
  if (error) return <p className="p-8 text-red-600">{error}</p>;

  return (
    <div className="max-w-3xl mx-auto px-4 py-8">
      <h1 className="text-xl font-bold text-slate-900 mb-1">Kategori İncelemesi</h1>
      <p className="text-sm text-slate-500 mb-6">
        Sistem emin olamadığı eşleşmeleri burada bekletir. Onayla veya reddet.
      </p>

      {items.length === 0 ? (
        <p className="text-slate-500">Bekleyen öğe yok. Her şey güncel.</p>
      ) : (
        <div className="flex flex-col gap-3">
          {items.map((item) => (
            <div
              key={item.id}
              className="flex items-center justify-between gap-4 p-4 bg-white border border-slate-200 rounded-xl"
            >
              <div className="flex-1 min-w-0">
                <p className="font-semibold text-slate-900">"{item.rawCategoryText}"</p>
                <p className="text-sm text-slate-500">
                  Önerilen: <span className="font-medium">{item.suggestedCategory ?? "—"}</span>
                  {" · "}Güven: {(item.score * 100).toFixed(0)}%
                </p>
              </div>
              <div className="flex gap-2 shrink-0">
                <button
                  onClick={() => handleAction(item.id, "approve")}
                  className="px-4 py-2 rounded-lg bg-emerald-600 text-white text-sm font-semibold hover:bg-emerald-700"
                >
                  Onayla
                </button>
                <button
                  onClick={() => handleAction(item.id, "reject")}
                  className="px-4 py-2 rounded-lg bg-red-100 text-red-700 text-sm font-semibold hover:bg-red-200"
                >
                  Reddet
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}