export interface MarketProduct {
  id: number;
  canonicalTitle: string;
  brand: string;
  lowestPrice: number;
  offerCount: number;
  imageUrl?: string;
  sources?: string;
}

export interface MobilyaProduct {
  id: number;
  externalId: number;
  title: string;
  imageUrl?: string;
  price: number;
  regularPrice: number;
  productUrl: string;
  brand?: string;
  source?: string;
  category?: string;
  midCategory?: string;
  subCategory?: string;
  color?: string;
  dimensions?: string;
  productType?: string;
  material?: string;
}
export type IkeaProduct = MobilyaProduct;

export interface ProductOffer {
  canonicalTitle: string;
  source: string;
  price: number;
  imageUrl?: string;
  productUrl: string;
}

export interface MobilyaFilters {
  brands?: string[];
  categoryTree: Array<{
    category: string;
    midCategory?: string;
    subCategory?: string;
  }>;
  topCategories: string[];
  midCategories?: string[];
  subCategories?: string[];
  colors: string[];
  dimensions: string[];
  productTypes: string[];
  materials: string[];
}
export type IkeaFilters = MobilyaFilters;

export interface MarketFilters {
  q: string;
  brand: string;
  source: string;
  sort: string;
  minPrice: string;
  maxPrice: string;
}

export interface MobilyaFilterState {
  q: string;
  brand: string;
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
export type IkeaFilterState = MobilyaFilterState;

export type Section = "market" | "mobilya";
