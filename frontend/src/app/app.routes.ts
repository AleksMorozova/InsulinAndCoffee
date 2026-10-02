import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { CalculatorComponent } from './pages/calculator/calculator.component';
import { HistoryComponent } from './pages/history/history.component';
import { MealDetailsComponent } from './pages/meal-details/meal-details.component';
import { FoodsComponent } from './pages/foods/foods.component';
import { DeliveryMealsComponent } from './pages/delivery-meals/delivery-meals.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { SuppliesComponent } from './pages/supplies/supplies.component';
import { AccessDeniedPageComponent } from './pages/error-pages/access-denied-page.component';
import { GenericErrorPageComponent } from './pages/error-pages/generic-error-page.component';
import { NotFoundPageComponent } from './pages/error-pages/not-found-page.component';
import { LoginComponent } from './pages/auth/login.component';
import { RegisterComponent } from './pages/auth/register.component';
import { authGuard } from './core/auth.guard';
import { guestGuard } from './core/guest.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent, title: 'Sign in', canActivate: [guestGuard] },
  { path: 'register', component: RegisterComponent, title: 'Create account', canActivate: [guestGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'dashboard', component: DashboardComponent, title: 'Dashboard', canActivate: [authGuard] },
  { path: 'calculator', component: CalculatorComponent, title: 'Current Meal', canActivate: [authGuard] },
  { path: 'history', component: HistoryComponent, title: 'Meal History', canActivate: [authGuard] },
  { path: 'meals/:id', component: MealDetailsComponent, title: 'Meal Details', canActivate: [authGuard] },
  { path: 'delivery-meals', component: DeliveryMealsComponent, title: 'Ask Past Me', canActivate: [authGuard] },
  { path: 'foods', component: FoodsComponent, title: 'Food Library', canActivate: [authGuard] },
  { path: 'supplies', component: SuppliesComponent, title: 'Supplies', canActivate: [authGuard] },
  { path: 'settings', component: SettingsComponent, title: 'Settings', canActivate: [authGuard] },
  { path: 'not-found', component: NotFoundPageComponent, title: 'Page not found' },
  { path: 'access-denied', component: AccessDeniedPageComponent, title: 'Access denied' },
  { path: 'error', component: GenericErrorPageComponent, title: 'Something went wrong' },
  { path: '**', component: NotFoundPageComponent, title: 'Page not found' }
];
