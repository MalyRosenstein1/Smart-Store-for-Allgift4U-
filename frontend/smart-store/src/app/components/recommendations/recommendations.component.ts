import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RecommendationService } from '../../services/recommendation.service';
import { PurchaseRecommendation } from '../../models/models';

@Component({
  selector: 'app-recommendations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './recommendations.component.html',
  styleUrls: ['./recommendations.component.scss']
})
export class RecommendationsComponent implements OnInit {
  recommendations: PurchaseRecommendation[] = [];
  rec: PurchaseRecommendation = { productID: 0, supplierName: '', price: 0, purchaseUrl: '' };
  success = false;

  constructor(private recService: RecommendationService) {}

  ngOnInit(): void {
    this.loadRecommendations();
  }

  loadRecommendations(): void {
    this.recService.getRecommendations().subscribe(data => this.recommendations = data);
  }

  submit(): void {
    this.recService.addRecommendation(this.rec).subscribe(() => {
      this.success = true;
      this.rec = { productID: 0, supplierName: '', price: 0, purchaseUrl: '' };
      this.loadRecommendations();
    });
  }
}
