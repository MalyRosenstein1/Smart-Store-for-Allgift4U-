import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private api = 'http://localhost:5187/api/products';

  constructor(private http: HttpClient) {}

  getInventory(): Observable<Product[]> {
    return this.http.get<Product[]>(this.api);
  }

  getMissingProducts(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.api}/missing`);
  }
}
