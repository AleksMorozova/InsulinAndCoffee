import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';
import { SKIP_GLOBAL_ERROR_NOTIFICATION } from './http-error-context';
import { Dashboard, DiabetesSettings, FoodItem, DeliveryMeal, DeliveryMealSections, MealCalculation, MealDetail, MealItemInput, MealSummary, MealType, PaginatedResult, ResultRating, SupplyCheckResult, SupplyItem, UseDeliveryMeal } from './models';

export interface UpsertFoodRequest {
  name: string;
  carbsPer100g: number;
  proteinPer100g: number;
  fatPer100g: number;
  caloriesPer100g: number;
  isFavorite: boolean;
}

export interface CalculateMealRequest {
  mealType: MealType;
  preMealGlucose: number;
  items: MealItemInput[];
  directCarbs?: number;
  directFoodName?: string;
}

export interface CreateMealRequest extends CalculateMealRequest {
  mealTime?: string;
  confirmedBolus: number | null;
  notes?: string;
}

export interface ConfirmMealBolusRequest {
  confirmedBolus: number;
}

export interface AddMealItemsRequest {
  items: MealItemInput[];
}

export interface UpdateMealItemRequest {
  weightGrams: number;
}

export interface UpsertDeliveryMealRequest {
  placeName: string;
  dishName: string;
  portionDescription: string;
  carbs: number;
  usualInsulinUnits: number;
  lastPreMealGlucose?: number;
  resultRating: ResultRating;
  tags: string;
  notes?: string;
  isFavorite: boolean;
}

export interface CreateDeliveryMealFromMealRequest {
  placeName: string;
  dishName: string;
  portionDescription: string;
  resultRating: ResultRating;
  tags: string;
  isFavorite: boolean;
}

export interface UpsertSupplyRequest {
  name: string;
  currentQuantity: number;
  unit: string;
  dailyUsage: number;
  lowStockThresholdDays: number;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly apiUrl = environment.apiUrl;
  private readonly localErrorContext = new HttpContext().set(SKIP_GLOBAL_ERROR_NOTIFICATION, true);

  constructor(private readonly http: HttpClient) {}

  getDashboard() {
    return this.http.get<Dashboard>(`${this.apiUrl}/dashboard/today`, { context: this.localErrorContext });
  }

  getFoods(search = '') {
    let params = new HttpParams().set('pageSize', 100);
    if (search) params = params.set('search', search);
    return this.http.get<PaginatedResult<FoodItem>>(`${this.apiUrl}/foods`, { params, context: this.localErrorContext }).pipe(
      map((result) => result.items)
    );
  }

  createFood(request: UpsertFoodRequest) {
    return this.http.post<FoodItem>(`${this.apiUrl}/foods`, request);
  }

  updateFood(id: string, request: UpsertFoodRequest) {
    return this.http.put<FoodItem>(`${this.apiUrl}/foods/${id}`, request);
  }

  deleteFood(id: string) {
    return this.http.delete<void>(`${this.apiUrl}/foods/${id}`);
  }

  calculateMeal(request: CalculateMealRequest) {
    return this.http.post<MealCalculation>(`${this.apiUrl}/meals/calculate`, request);
  }

  createMeal(request: CreateMealRequest) {
    return this.http.post<MealDetail>(`${this.apiUrl}/meals`, request);
  }

  confirmMealBolus(id: string, request: ConfirmMealBolusRequest) {
    return this.http.patch<MealDetail>(`${this.apiUrl}/meals/${id}/confirmed-bolus`, request);
  }

  addMealItems(id: string, request: AddMealItemsRequest) {
    return this.http.patch<MealDetail>(`${this.apiUrl}/meals/${id}/items`, request);
  }

  updateMealItem(mealId: string, itemId: string, request: UpdateMealItemRequest) {
    return this.http.put<MealDetail>(`${this.apiUrl}/meals/${mealId}/items/${itemId}`, request);
  }

  removeMealItem(mealId: string, itemId: string) {
    return this.http.delete<MealDetail>(`${this.apiUrl}/meals/${mealId}/items/${itemId}`);
  }

  getMeals(search = '', mealType = '') {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (mealType) params = params.set('mealType', mealType);
    return this.http.get<MealSummary[]>(`${this.apiUrl}/meals`, { params, context: this.localErrorContext });
  }

  getMeal(id: string) {
    return this.http.get<MealDetail>(`${this.apiUrl}/meals/${id}`, { context: this.localErrorContext });
  }

  getSettings() {
    return this.http.get<DiabetesSettings>(`${this.apiUrl}/settings`, { context: this.localErrorContext });
  }

  updateSettings(request: Omit<DiabetesSettings, 'id' | 'updatedAt'>) {
    return this.http.put<DiabetesSettings>(`${this.apiUrl}/settings`, request);
  }

  getDeliveryMeals(search = '') {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<DeliveryMealSections>(`${this.apiUrl}/delivery-meals`, { params, context: this.localErrorContext });
  }

  getDeliveryMeal(id: string) {
    return this.http.get<DeliveryMeal>(`${this.apiUrl}/delivery-meals/${id}`, { context: this.localErrorContext });
  }

  createDeliveryMeal(request: UpsertDeliveryMealRequest) {
    return this.http.post<DeliveryMeal>(`${this.apiUrl}/delivery-meals`, request);
  }

  createDeliveryMealFromMeal(mealId: string, request: CreateDeliveryMealFromMealRequest) {
    return this.http.post<DeliveryMeal>(`${this.apiUrl}/meals/${mealId}/save-as-delivery-meal`, request);
  }

  updateDeliveryMeal(id: string, request: UpsertDeliveryMealRequest) {
    return this.http.put<DeliveryMeal>(`${this.apiUrl}/delivery-meals/${id}`, request);
  }

  toggleDeliveryMealFavorite(id: string) {
    return this.http.post<DeliveryMeal>(`${this.apiUrl}/delivery-meals/${id}/favorite`, {});
  }

  createMealDraftFromDeliveryMeal(id: string) {
    return this.http.post<UseDeliveryMeal>(`${this.apiUrl}/delivery-meals/${id}/meal-draft`, {});
  }

  deleteDeliveryMeal(id: string) {
    return this.http.delete<void>(`${this.apiUrl}/delivery-meals/${id}`);
  }

  getSupplies() {
    return this.http.get<SupplyItem[]>(`${this.apiUrl}/supplies`, { context: this.localErrorContext });
  }

  getSupply(id: string) {
    return this.http.get<SupplyItem>(`${this.apiUrl}/supplies/${id}`, { context: this.localErrorContext });
  }

  createSupply(request: UpsertSupplyRequest) {
    return this.http.post<SupplyItem>(`${this.apiUrl}/supplies`, request);
  }

  updateSupply(id: string, request: UpsertSupplyRequest) {
    return this.http.put<SupplyItem>(`${this.apiUrl}/supplies/${id}`, request);
  }

  deleteSupply(id: string) {
    return this.http.delete<void>(`${this.apiUrl}/supplies/${id}`);
  }

  getSupplyCheck() {
    return this.http.get<SupplyCheckResult[]>(`${this.apiUrl}/supplies/check`, { context: this.localErrorContext });
  }

}
