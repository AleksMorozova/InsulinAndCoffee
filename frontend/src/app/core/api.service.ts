import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Dashboard, DiabetesSettings, FoodItem, DeliveryMeal, DeliveryMealSections, MealCalculation, MealDetail, MealItemInput, MealSummary, MealType, ResultRating, UseDeliveryMeal } from './models';

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
  confirmedBolus: number;
  notes?: string;
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

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  getDashboard() {
    return this.http.get<Dashboard>(`${this.apiUrl}/dashboard`);
  }

  getFoods(search = '') {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<FoodItem[]>(`${this.apiUrl}/foods`, { params });
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

  getMeals(search = '', mealType = '') {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (mealType) params = params.set('mealType', mealType);
    return this.http.get<MealSummary[]>(`${this.apiUrl}/meals`, { params });
  }

  getMeal(id: string) {
    return this.http.get<MealDetail>(`${this.apiUrl}/meals/${id}`);
  }

  getSettings() {
    return this.http.get<DiabetesSettings>(`${this.apiUrl}/settings`);
  }

  updateSettings(request: Omit<DiabetesSettings, 'id' | 'updatedAt'>) {
    return this.http.put<DiabetesSettings>(`${this.apiUrl}/settings`, request);
  }

  getDeliveryMeals(search = '') {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<DeliveryMealSections>(`${this.apiUrl}/delivery-meals`, { params });
  }

  getDeliveryMeal(id: string) {
    return this.http.get<DeliveryMeal>(`${this.apiUrl}/delivery-meals/${id}`);
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

}
