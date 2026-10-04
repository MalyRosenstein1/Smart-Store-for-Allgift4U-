import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { timeout } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class InventoryOrderService {
  private api = 'http://localhost:5187/api/inventory';

  constructor(private http: HttpClient) {}

  getProducts(): Observable<any[]> {
    return this.http.get<any[]>('http://localhost:5187/api/products');
  }

  getOrderStock(orderName: string): Observable<any> {
    return this.http.get<any>(`${this.api}/order-stock/${encodeURIComponent(orderName)}`).pipe(timeout(30000));
  }

  processOrder(orderName: string): Observable<any> {
    return this.http.post<any>(`${this.api}/process-order`, { orderName: orderName }).pipe(
      timeout(120000)
    );
  }

  processChatMessage(userMessage: string, chatHistory: any[]): Observable<any> {
    const request = {
      userMessage: userMessage,
      chatHistory: chatHistory.map(msg => ({
        role: msg.role,
        text: msg.text
      }))
    };
    return this.http.post<any>(`${this.api}/process-chat`, request).pipe(
      timeout(120000)
    );
  }

  getInventoryStatus(): Observable<any> {
    return this.http.get<any>(`${this.api}/inventory-status`).pipe(timeout(30000));
  }

  processImage(base64Image: string, mimeType: string, fileName?: string): Observable<any> {
    return this.http.post<any>(`${this.api}/process-image`, { base64Image, mimeType, fileName }).pipe(timeout(120000));
  }

  updateProductStock(productId: number, newQuantity: number): Observable<any> {
    return this.http.put<any>(`${this.api}/products/${productId}/stock`, { newQuantity }).pipe(
      timeout(120000)
    );
  }

  createOrderTemplate(orderName: string, description: string, productId: number, quantityRequired: number): Observable<any> {
    const request = {
      OrderName: orderName,
      Description: description,
      Components: [
        {
          ProductID: productId,
          QuantityRequired: quantityRequired
        }
      ]
    };
    return this.http.post<any>(`${this.api}/templates`, request).pipe(
      timeout(120000)
    );
  }
}
