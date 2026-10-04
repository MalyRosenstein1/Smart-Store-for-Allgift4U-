import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryOrderService } from '../../services/inventory-order.service';

@Component({
  selector: 'app-order',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './order.component.html',
  styleUrls: ['./order.component.scss']
})
export class OrderComponent {
  orderName = '';
  result: any = null;
  error = '';
  loading = false;

  constructor(private inventoryOrderService: InventoryOrderService) {}

  processOrder(): void {
    if (!this.orderName.trim()) return;
    this.loading = true;
    this.result = null;
    this.error = '';

    this.inventoryOrderService.processOrder(this.orderName).subscribe({
      next: (res) => {
        this.result = res;
        this.loading = false;
      },
      error: (err) => {
        this.error = err.error?.error ?? 'שגיאה בעיבוד ההזמנה';
        this.loading = false;
      }
    });
  }
}
