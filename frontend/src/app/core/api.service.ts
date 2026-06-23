import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Dashboard, DiabetesSettings, FoodItem, KnownMeal, KnownMealSections, MealCalculation, MealDetail, MealItemInput, MealSummary, MealType, ResultRating, UseKnownMeal } from './models';

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

export interface UpsertKnownMealRequest {
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

export interface CreateKnownMealFromMealRequest {
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

  getKnownMeals(search = '') {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<KnownMealSections>(`${this.apiUrl}/known-meals`, { params });
  }

  getKnownMeal(id: string) {
    return this.http.get<KnownMeal>(`${this.apiUrl}/known-meals/${id}`);
  }

  createKnownMeal(request: UpsertKnownMealRequest) {
    return this.http.post<KnownMeal>(`${this.apiUrl}/known-meals`, request);
  }

  createKnownMealFromMeal(mealId: string, request: CreateKnownMealFromMealRequest) {
    return this.http.post<KnownMeal>(`${this.apiUrl}/meals/${mealId}/save-to-known-meals`, request);
  }

  updateKnownMeal(id: string, request: UpsertKnownMealRequest) {
    return this.http.put<KnownMeal>(`${this.apiUrl}/known-meals/${id}`, request);
  }

  toggleKnownMealFavorite(id: string) {
    return this.http.post<KnownMeal>(`${this.apiUrl}/known-meals/${id}/favorite`, {});
  }

  useKnownMealAgain(id: string) {
    return this.http.post<UseKnownMeal>(`${this.apiUrl}/known-meals/${id}/use-again`, {});
  }

  deleteKnownMeal(id: string) {
    return this.http.delete<void>(`${this.apiUrl}/known-meals/${id}`);
  }

}
