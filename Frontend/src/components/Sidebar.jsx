import React, { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { fetchCategories } from '../api';

export default function Sidebar() {
  const [categories, setCategories] = useState([]);
  const [searchParams] = useSearchParams();
  const activeCategoryId = searchParams.get('category');

  useEffect(() => {
    fetchCategories().then(setCategories).catch(console.error);
  }, []);

  return (
    <aside className="w-64 shrink-0 bg-white border-r border-slate-200 min-h-[calc(100vh-72px)] p-6 hidden md:block">
      <h3 className="text-sm font-bold text-slate-800 uppercase tracking-wider mb-4">Kategoriler</h3>
      <div className="space-y-4">
        {categories.map(cat => (
          <div key={cat.id} className="space-y-2">
            <Link 
              to={`/?category=${cat.id}`}
              className={`block font-semibold ${activeCategoryId === String(cat.id) ? 'text-indigo-600' : 'text-slate-700 hover:text-indigo-600'}`}
            >
              {cat.name} <span className="text-xs text-slate-400 font-normal ml-1">({cat.productCount})</span>
            </Link>
            {cat.children && cat.children.length > 0 && (
              <ul className="pl-3 space-y-1.5 border-l-2 border-slate-100 ml-1">
                {cat.children.map(child => (
                  <li key={child.id}>
                    <Link 
                      to={`/?category=${child.id}`}
                      className={`block text-sm ${activeCategoryId === String(child.id) ? 'text-indigo-600 font-medium' : 'text-slate-500 hover:text-indigo-600'}`}
                    >
                      {child.name}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>
        ))}
      </div>
    </aside>
  );
}
