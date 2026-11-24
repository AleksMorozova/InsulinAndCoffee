import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, timeout } from 'rxjs';
import { environment } from '../../environments/environment';
import { SKIP_GLOBAL_ERROR_NOTIFICATION } from './http-error-context';
import { CalculatedMealItem, Dashboard, DiabetesSettings, FoodItem, DeliveryMeal, DeliveryMealSections, FoodMeasurementType, LoginAttempt, LoginAttemptStats, MealCalculation, MealCarbPreview, MealDetail, MealItemInput, MealSummary, MealType, PaginatedResult, ResultRating, SupplyCheckResult, SupplyItem, UseDeliveryMeal } from './models';

import { AssistantConversation, AssistantDraftState, AssistantFoodSelection, AssistantTurn, MealAssistantResponse, AssistantResolution, AssistantDraftEdit } from './meal-assistant.models';
import { DeliveryMealItemInput, DeliveryMealPreview } from './models';
import { AiUsageRecord, AiMonitoringOverview, AiUsageFilters, AiModelPricing, CreateAiPricing } from './ai-monitoring.models';

export interface UpsertFoodRequest {
  name: string;
  measurementType: FoodMeasurementType;
  carbsPer100g?: number | null;
  carbsPerUnit?: number | null;
  proteinPer100g: number;
  fatPer100g: number;
  caloriesPer100g: number;
  isFavorite: boolean;
}

export interface CalculateMealRequest {
  sourceDeliveryMealId?: string;
  mealType: MealType;
  preMealGlucose: number;
  items: MealItemInput[];
  directCarbs?: number;
  directFoodName?: string;
  carbAdjustment?: number;
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
  quantity: number;
  carbOverride?: number | null;
}

export interface UpsertDeliveryMealRequest {
  items?: DeliveryMealItemInput[];
  expectedRevision?: number;
  acceptCalculatedTotal?: boolean;
  placeName: string;
  dishName: string;
  portionDescription?: string | null;
  carbs: number;
  usualInsulinUnits: number | null;
  lastPreMealGlucose?: number;
  resultRating: ResultRating;
  tags: string;
  notes?: string;
  isFavorite: boolean;
}

export interface CreateDeliveryMealFromMealRequest {
  placeName: string;
  dishName: string;
  portionDescription?: string | null;
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
  previewDeliveryMealItems(items: DeliveryMealItemInput[], savedTotal?: number) {
    return this.http.post<DeliveryMealPreview>(`${this.apiUrl}/delivery-meals/preview-items`, { items, savedTotal }, { context: this.localErrorContext });
  }

  reuseDeliveryMealItems(id: string, itemIds: string[], expectedRevision: number) {
    return this.http.post<CalculatedMealItem[]>(`${this.apiUrl}/delivery-meals/${id}/items-draft`, { itemIds, expectedRevision }, { context: this.localErrorContext });
  }
  private readonly apiUrl = environment.apiUrl;
  private readonly localErrorContext = new HttpContext().set(SKIP_GLOBAL_ERROR_NOTIFICATION, true);

  constructor(private readonly http: HttpClient) {}

  sendMealAssistantMessage(conversationId: string | null, message: string, requestId: string, selection?: AssistantFoodSelection, expectedDraftVersion?: number, edit?: AssistantDraftEdit, resolution?: AssistantResolution) {
    return this.http.post<MealAssistantResponse>(`${this.apiUrl}/meal-assistant/messages`, { conversationId, message, requestId,
      ...(selection ? { selection } : {}), ...(expectedDraftVersion !== undefined ? { expectedDraftVersion } : {}), ...(edit ? { edit } : {}), ...(resolution ? { resolution } : {})
    }, { context: this.localErrorContext }).pipe(timeout(90000));
  }

  getMealAssistantDraftState(id: string) {
    return this.http.get<AssistantDraftState>(`${this.apiUrl}/meal-assistant/conversations/${id}/draft-state`, { context: this.localErrorContext });
  }

  getMealAssistantConversations() {
    return this.http.get<AssistantConversation[]>(`${this.apiUrl}/meal-assistant/conversations`, { context: this.localErrorContext });
  }

  getMealAssistantConversation(id: string) {
    return this.http.get<AssistantTurn[]>(`${this.apiUrl}/meal-assistant/conversations/${id}`, { context: this.localErrorContext });
  }

  getMealAssistantCalculatorDraft(id: string) {
    return this.http.get<CalculatedMealItem[]>(`${this.apiUrl}/meal-assistant/conversations/${id}/calculator-draft`, { context: this.localErrorContext });
  }

  deleteMealAssistantConversation(id: string) {
    return this.http.delete<void>(`${this.apiUrl}/meal-assistant/conversations/${id}`, { context: this.localErrorContext });
  }

  renameMealAssistantConversation(id: string, title: string) {
    return this.http.patch<void>(`${this.apiUrl}/meal-assistant/conversations/${id}`, { title }, { context: this.localErrorContext });
  }

  private aiParams(filters: AiUsageFilters) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) if (value) params = params.set(key, value);
    return params;
  }
  getAiOverview(filters: AiUsageFilters) {
    return this.http.get<AiMonitoringOverview>(`${this.apiUrl}/admin/ai-monitoring/overview`, { params: this.aiParams(filters), context: this.localErrorContext });
  }
  getAiRequests(filters: AiUsageFilters, page = 1, sort = 'time', descending = true) {
    return this.http.get<PaginatedResult<AiUsageRecord>>(`${this.apiUrl}/admin/ai-monitoring/requests`, {
      params: this.aiParams(filters).set('page', page).set('pageSize', 25).set('sort', sort).set('descending', descending), context: this.localErrorContext });
  }
  getAiRequest(id: string) { return this.http.get<AiUsageRecord>(`${this.apiUrl}/admin/ai-monitoring/requests/${id}`, { context: this.localErrorContext }); }
  getAiPricing() { return this.http.get<AiModelPricing[]>(`${this.apiUrl}/admin/ai-monitoring/pricing`, { context: this.localErrorContext }); }
  createAiPricing(request: CreateAiPricing) { return this.http.post<AiModelPricing>(`${this.apiUrl}/admin/ai-monitoring/pricing`, request, { context: this.localErrorContext }); }

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

  previewMealCarbs(items: MealItemInput[], carbAdjustment: number) {
    return this.http.post<MealCarbPreview>(`${this.apiUrl}/meals/preview-carbs`, { items, carbAdjustment }, { context: this.localErrorContext });
  }

  calculateMeal(request: CalculateMealRequest) {
    return this.http.post<MealCalculation>(`${this.apiUrl}/meals/calculate`, request);
  }

  createMeal(request: CreateMealRequest) {
    return this.http.post<MealDetail>(`${this.apiUrl}/meals`, request);
  }

  verifyMealCarbs(id: string, expectedCarbRevision: number) {
    return this.http.post<MealDetail>(`${this.apiUrl}/meals/${id}/carb-verification`, { expectedCarbRevision });
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

  getLoginAttempts(page: number, pageSize: number, username: string, success: string, from: string, to: string) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (username) params = params.set('username', username);
    if (success) params = params.set('success', success);
    if (from) params = params.set('from', new Date(from).toISOString());
    if (to) params = params.set('to', new Date(to).toISOString());
    return this.http.get<PaginatedResult<LoginAttempt>>(`${this.apiUrl}/admin/login-attempts`, { params });
  }

  getLoginAttemptStats(username: string, success: string, from: string, to: string) {
    let params = new HttpParams();
    if (username) params = params.set('username', username);
    if (success) params = params.set('success', success);
    if (from) params = params.set('from', new Date(from).toISOString());
    if (to) params = params.set('to', new Date(to).toISOString());
    return this.http.get<LoginAttemptStats>(`${this.apiUrl}/admin/login-attempts/stats`, { params });
  }

}

