import { formatPrice } from './ProductCard';

const RANK_STYLES = [
  "border-amber-300 bg-gradient-to-br from-amber-50/90 to-amber-50/50 shadow-[0_4px_12px_rgba(245,158,11,0.12)]",
  "border-slate-300 bg-gradient-to-br from-slate-50/90 to-slate-50/50 shadow-[0_4px_12px_rgba(100,116,139,0.1)]",
  "border-orange-200 bg-gradient-to-br from-orange-50/90 to-orange-50/50 shadow-[0_4px_12px_rgba(234,88,12,0.08)]",
];

const RANK_BADGE_STYLES = [
  "bg-gradient-to-br from-amber-400 to-amber-500 text-white shadow-[0_2px_6px_rgba(245,158,11,0.35)]",
  "bg-gradient-to-br from-slate-400 to-slate-500 text-white shadow-[0_2px_6px_rgba(100,116,139,0.3)]",
  "bg-gradient-to-br from-orange-400 to-orange-500 text-white shadow-[0_2px_6px_rgba(234,88,12,0.3)]",
];

const RANK_LABELS = ["#1 En İyi", "#2", "#3"];

function unitLabel(sizeUnit) {
  if (sizeUnit === "ML") return "L";
  if (sizeUnit === "G") return "kg";
  return "adet";
}

export default function DealCard({ deal, rank, onClick }) {
  const isTop3 = rank !== null && rank <= 3;

  const cardClasses = [
    "group relative bg-white border rounded-2xl overflow-hidden cursor-pointer",
    "flex flex-col transition-all duration-250 ease-[cubic-bezier(0.4,0,0.2,1)]",
    "hover:-translate-y-1",
    isTop3
      ? `${RANK_STYLES[rank - 1]} hover:shadow-[0_20px_25px_-5px_rgba(245,158,11,0.15)]`
      : "border-slate-200 shadow-xs hover:shadow-[0_20px_25px_-5px_rgba(79,70,229,0.1),0_8px_10px_-6px_rgba(79,70,229,0.04)] hover:border-slate-300",
  ].join(" ");

  const handleKeyDown = (e) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      onClick();
    }
  };

  return (
    <article
      className={cardClasses}
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={handleKeyDown}
    >
      {isTop3 && (
        <span
          className={`absolute top-3 left-3 z-10 text-[0.6875rem] font-extrabold uppercase tracking-wider px-2.5 py-1 rounded-full ${RANK_BADGE_STYLES[rank - 1]}`}
        >
          {RANK_LABELS[rank - 1]}
        </span>
      )}

      <div className="bg-gradient-to-b from-slate-50 to-white p-5 flex items-center justify-center h-[180px] border-b border-slate-100/80 overflow-hidden">
        {deal.imageUrl ? (
          <img
            src={deal.imageUrl}
            alt={deal.canonicalTitle}
            className="max-w-full max-h-full object-contain transition-transform duration-300 group-hover:scale-[1.06]"
            loading="lazy"
          />
        ) : (
          <svg
            className="w-14 h-14 text-slate-400 opacity-30"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
          >
            <rect x="3" y="3" width="18" height="18" rx="2" />
            <circle cx="8.5" cy="8.5" r="1.5" />
            <path d="m21 15-5-5L5 21" />
          </svg>
        )}
      </div>

      <div className="p-4 flex-1 flex flex-col gap-1">
        {deal.brand && (
          <p className="text-[0.6875rem] font-bold text-indigo-600 uppercase tracking-widest">
            {deal.brand}
          </p>
        )}
        <h3 className="font-sans text-[0.875rem] font-semibold text-slate-900 leading-snug flex-1 line-clamp-2">
          {deal.canonicalTitle}
        </h3>

        <span className="self-start text-[0.75rem] font-semibold text-slate-500 bg-slate-100 px-2 py-0.5 rounded-md capitalize mt-1">
          {deal.source}
        </span>

        <div className="flex items-end justify-between gap-2 pt-3 mt-auto border-t border-slate-100/80">
          <div>
            <span className="block text-[0.6rem] font-bold text-slate-400 uppercase tracking-wider mb-0.5">
              Fiyat
            </span>
            <span className="font-display text-lg font-extrabold text-emerald-600 tracking-tight">
              {formatPrice(deal.price)}
            </span>
          </div>
          {deal.pricePerUnit != null && (
            <div className="text-right">
              <span className="block text-[0.6rem] font-bold text-slate-400 uppercase tracking-wider mb-0.5">
                Birim fiyat
              </span>
              <span className="font-display text-sm font-bold text-slate-700">
                {formatPrice(deal.pricePerUnit)}/{unitLabel(deal.sizeUnit)}
              </span>
            </div>
          )}
        </div>
      </div>
    </article>
  );
}