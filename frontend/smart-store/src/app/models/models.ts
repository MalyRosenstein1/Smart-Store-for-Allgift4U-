export interface Product {
  productID: number;
  productName: string;
  currentQuantity: number;
  minQuantity: number;
}

export interface PurchaseRecommendation {
  recommendationID?: number;
  productID: number;
  supplierName: string;
  price: number;
  purchaseUrl?: string;
  createdDate?: string;
  status?: string;
}

export interface AILog {
  logID?: number;
  transactionDate?: string;
  imageName?: string;
  ai_Analysis: string;
}
