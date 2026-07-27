import React from 'react';

export default function ActiveFilters({ filters, onRemoveFilter }) {
  const tags = [];
  if (filters.q) tags.push({ key: 'q', label: `"${filters.q}"` });
  if (filters.brand) tags.push({ key: 'brand', label: filters.brand });
  if (filters.minPrice) tags.push({ key: 'minPrice', label: `Min ${filters.minPrice} TL` });
  if (filters.maxPrice) tags.push({ key: 'maxPrice', label: `Max ${filters.maxPrice} TL` });

  if (tags.length === 0) return null;

  return (
    <div id="activeFilters" className="flex flex-wrap gap-2">
      {tags.map(({ key, label }) => (
        <span
          key={key}
          className="inline-flex items-center gap-1.5 bg-indigo-50 text-indigo-600 border border-indigo-200 text-[0.8125rem] font-semibold px-3 py-1.25 rounded-full animate-fade-in"
        >
          {label}{' '}
          <button
            type="button"
            aria-label="Kaldır"
            onClick={() => onRemoveFilter(key)}
            className="bg-transparent border-0 text-inherit cursor-pointer text-base leading-none p-0 opacity-75 hover:opacity-100 transition-opacity duration-150"
          >
            ×
          </button>
        </span>
      ))}
    </div>
  );
}
