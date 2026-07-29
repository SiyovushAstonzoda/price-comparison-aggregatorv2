export interface MarketProduct {
  id: number;
  canonicalTitle: string;
  brand: string;
  lowestPrice: number;
  offerCount: number;
  imageUrl?: string;
  sources?: string;
}

export interface IkeaProduct {
  id: number;
  externalId: number;
  title: string;
  imageUrl?: string;
  price: number;
  regularPrice: number;
  productUrl: string;
  category?: string;
  midCategory?: string;
  subCategory?: string;
  color?: string;
  dimensions?: string;
  productType?: string;
  material?: string;
}

export interface ProductOffer {
  canonicalTitle: string;
  source: string;
  price: number;
  imageUrl?: string;
  productUrl: string;
}

export interface IkeaFilters {
  categoryTree: Array<{
    category: string;
    midCategory?: string;
    subCategory?: string;
  }>;
  topCategories: string[];
  colors: string[];
  dimensions: string[];
  productTypes: string[];
  materials: string[];
}

export interface MarketFilters {
  q: string;
  brand: string;
  source: string;
  sort: string;
  minPrice: string;
  maxPrice: string;
}

export interface IkeaFilterState {
  q: string;
  category: string;
  midCategory: string;
  subCategory: string;
  color: string;
  dimensions: string;
  productType: string;
  material: string;
  sort: string;
  minPrice: string;
  maxPrice: string;
}

export type Section = "market" | "ikea";
