import { Routes } from '@angular/router';
import { InventoryComponent } from './components/inventory/inventory.component';
import { RecommendationsComponent } from './components/recommendations/recommendations.component';
import { ChatComponent } from './components/chat/chat.component';
import { OrderComponent } from './components/order/order.component';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'chat', component: ChatComponent, canActivate: [authGuard] },
  { path: 'inventory', component: InventoryComponent, canActivate: [authGuard] },
  { path: 'recommendations', component: RecommendationsComponent, canActivate: [authGuard] },
  { path: 'order', component: OrderComponent, canActivate: [authGuard] }
];
