import React from 'react';

export default function CategoryNav({
  categories,
  selectedTopId,
  selectedSubId,
  onSelectTop,
  onSelectSub,
  onClearCategory,
}) {
  if (!categories || categories.length === 0) return null;

  const selectedTop = categories.find((c) => c.Id === selectedTopId);

  const pillBase =
    'inline-flex items-center gap-1.5 px-4 py-2 rounded-full text-sm font-semibold whitespace-nowrap cursor-pointer transition-all duration-150 border-[1.5px]';
  const pillActive = 'bg-indigo-600 border-indigo-600 text-white shadow-[0_2px_8px_rgba(79,70,229,0.25)]';
  const pillInactive =
    'bg-white border-slate-200 text-slate-700 hover:border-indigo-300 hover:text-indigo-600';

  return (
    <div className="mb-6 flex flex-col gap-3">
      <div className="flex flex-wrap gap-2.5">
        <button
          type="button"
          onClick={onClearCategory}
          className={`${pillBase} ${!selectedTopId ? pillActive : pillInactive}`}
        >
          Tüm Kategoriler
        </button>
        {categories.map((cat) => (
          <button
            key={cat.Id}
            type="button"
            onClick={() => onSelectTop(cat.Id)}
            className={`${pillBase} ${selectedTopId === cat.Id ? pillActive : pillInactive}`}
          >
            {cat.Name}
          </button>
        ))}
      </div>

      {selectedTop && selectedTop.Children && selectedTop.Children.length > 0 && (
        <div className="flex flex-wrap gap-2 pl-1 border-l-2 border-indigo-100 ml-1">
          <button
            type="button"
            onClick={() => onSelectSub(null)}
            className={`${pillBase} !px-3 !py-1.5 !text-[0.8125rem] ${
              !selectedSubId ? pillActive : pillInactive
            }`}
          >
            Tümü
          </button>
          {selectedTop.Children.map((sub) => (
            <button
              key={sub.Id}
              type="button"
              onClick={() => onSelectSub(sub.Id)}
              className={`${pillBase} !px-3 !py-1.5 !text-[0.8125rem] ${
                selectedSubId === sub.Id ? pillActive : pillInactive
              }`}
            >
              {sub.Name}
              {typeof sub.DirectProductCount === 'number' && (
                <span className="opacity-70">({sub.DirectProductCount})</span>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
