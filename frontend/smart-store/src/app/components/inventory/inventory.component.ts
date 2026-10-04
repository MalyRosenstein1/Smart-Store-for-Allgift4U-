import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryOrderService } from '../../services/inventory-order.service';
import { ProductService } from '../../services/product.service';
import { Product } from '../../models/models';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './inventory.component.html',
  styleUrls: ['./inventory.component.scss']
})
export class InventoryComponent implements OnInit {
  products: Product[] = [];
  missingProducts: Product[] = [];
  loading = false;
  stockUpdateValues: Record<number, number> = {};
  newTemplateName = '';
  newTemplateDesc = '';
  newTemplateProductId = 0;
  newTemplateQuantity = 1;
  templateResultMessage = '';
  templateErrorMessage = '';

  constructor(
    private productService: ProductService,
    private inventoryOrderService: InventoryOrderService
  ) {}

  ngOnInit(): void {
    this.refreshInventory();
  }

  refreshInventory(): void {
    this.loading = true;
    this.productService.getInventory().subscribe({
      next: data => {
        this.products = data;
        if (data.length > 0 && !this.newTemplateProductId) {
          this.newTemplateProductId = data[0].productID;
        }
        for (const product of data) {
          if (this.stockUpdateValues[product.productID] === undefined) {
            this.stockUpdateValues[product.productID] = product.currentQuantity;
          }
        }
      },
      error: () => this.products = []
    });

    this.productService.getMissingProducts().subscribe({
      next: data => this.missingProducts = data,
      error: () => this.missingProducts = [],
      complete: () => this.loading = false
    });
  }

  updateStock(productId: number): void {
    const newQuantity = this.stockUpdateValues[productId];
    if (newQuantity == null || newQuantity < 0) return;

    this.loading = true;
    this.inventoryOrderService.updateProductStock(productId, newQuantity).subscribe({
      next: () => {
        this.templateResultMessage = '';
        this.templateErrorMessage = '';
        this.refreshInventory();
      },
      error: err => {
        this.loading = false;
        this.templateErrorMessage = err.error?.error ?? 'שגיאה בעדכון מלאי';
      }
    });
  }

  createTemplate(): void {
    if (!this.newTemplateName.trim() || this.newTemplateProductId <= 0 || this.newTemplateQuantity <= 0) {
      this.templateErrorMessage = 'יש למלא שם תבנית, מוצר וכמות תקינים.';
      return;
    }

    this.loading = true;
    this.templateResultMessage = '';
    this.templateErrorMessage = '';

    this.inventoryOrderService.createOrderTemplate(
      this.newTemplateName,
      this.newTemplateDesc,
      this.newTemplateProductId,
      this.newTemplateQuantity
    ).subscribe({
      next: result => {
        this.templateResultMessage = `תבנית '${result.orderName}' נוצרה בהצלחה (ID ${result.templateID})`;
        this.newTemplateName = '';
        this.newTemplateDesc = '';
        this.newTemplateQuantity = 1;
      },
      error: err => {
        this.templateErrorMessage = err.error?.error ?? 'שגיאה ביצירת תבנית';
      },
      complete: () => (this.loading = false)
    });
  }
}
