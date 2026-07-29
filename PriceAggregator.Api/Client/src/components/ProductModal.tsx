import { useEffect, useState } from "react";
import { formatPrice, formatSource, getProductOffers } from "../api";
import type { ProductOffer } from "../types";

interface ProductModalProps {
  id: number;
  title: string;
  onClose: () => void;
}

export default function ProductModal({ id, title, onClose }: ProductModalProps) {
  const [offers, setOffers] = useState<ProductOffer[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    getProductOffers(id)
      .then(setOffers)
      .catch(() => setOffers([]))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = "";
    };
  }, [onClose]);

  return (
    <div className="modal" role="dialog" aria-modal="true">
      <div className="modal-backdrop" onClick={onClose} />
      <div className="modal-content">
        <button type="button" className="modal-close" aria-label="Kapat" onClick={onClose}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M18 6 6 18M6 6l12 12" />
          </svg>
        </button>
        <h2 className="modal-title">{title}</h2>
        <p className="modal-subtitle">Mağaza fiyatları</p>
        <div className="offers-list">
          {loading && <div className="state-message"><p>Yükleniyor...</p></div>}
          {!loading && offers.length === 0 && (
            <div className="state-message"><p>Fiyat bulunamadı.</p></div>
          )}
          {!loading &&
            offers.map((offer, index) => (
              <div key={`${offer.source}-${offer.price}`} className="offer-row">
                {offer.imageUrl && <img src={offer.imageUrl} alt={offer.source} />}
                <div className="offer-store">
                  <div className="offer-source">{formatSource(offer.source)}</div>
                  {index === 0 && <span className="offer-badge">En ucuz</span>}
                </div>
                <span className="offer-price">{formatPrice(offer.price)}</span>
                <a href={offer.productUrl} target="_blank" rel="noopener" className="offer-link">
                  Git
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                    <polyline points="15 3 21 3 21 9" />
                    <line x1="10" y1="14" x2="21" y2="3" />
                  </svg>
                </a>
              </div>
            ))}
        </div>
      </div>
    </div>
  );
}
