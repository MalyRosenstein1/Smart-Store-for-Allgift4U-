import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PurchaseRecommendation } from '../models/models';

@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private api = 'http://localhost:5187/api/recommendations';

  constructor(private http: HttpClient) {}

  getRecommendations(): Observable<PurchaseRecommendation[]> {
    return this.http.get<PurchaseRecommendation[]>(this.api);
  }

  addRecommendation(rec: PurchaseRecommendation): Observable<void> {
    return this.http.post<void>(this.api, rec);
  }
}
