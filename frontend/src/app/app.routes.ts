import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { CalculatorComponent } from './pages/calculator/calculator.component';
import { HistoryComponent } from './pages/history/history.component';
import { MealDetailsComponent } from './pages/meal-details/meal-details.component';
import { FoodsComponent } from './pages/foods/foods.component';
import { DeliveryMealsComponent } from './pages/delivery-meals/delivery-meals.component';
import { SettingsComponent } from './pages/settings/settings.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent, title: 'Dashboard' },
  { path: 'calculator', component: CalculatorComponent, title: 'Current Meal' },
  { path: 'history', component: HistoryComponent, title: 'Meal History' },
  { path: 'meals/:id', component: MealDetailsComponent, title: 'Meal Details' },
  { path: 'delivery-meals', component: DeliveryMealsComponent, title: 'Ask Past Me' },
  { path: 'foods', component: FoodsComponent, title: 'Food Library' },
  { path: 'settings', component: SettingsComponent, title: 'Settings' },
  { path: '**', redirectTo: '' }
];
