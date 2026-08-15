export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';
export type ResultRating = 'Perfect' | 'Good' | 'HighGlucose' | 'LowGlucose' | 'Unknown';
export type FoodMeasurementType = 'Grams' | 'Portion' | 'Piece';

export const mealTypes: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
export const resultRatings: ResultRating[] = ['Perfect', 'Good', 'HighGlucose', 'LowGlucose', 'Unknown'];

export interface DiabetesSettings {
  id: string;
  targetGlucose: number;
  carbRatio: number;
  correctionFactor: number;
  insulinDurationHours: number;
  updatedAt: string;
}

export interface FoodItem {
  id: string;
  name: string;
  measurementType: FoodMeasurementType;
  carbsPer100g: number | null;
  carbsPerUnit: number | null;
  proteinPer100g: number;
  fatPer100g: number;
  caloriesPer100g: number;
  isFavorite: boolean;
  createdAt: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface MealItemInput {
  foodItemId: string;
  quantity: number;
  weightGrams?: number;
  measurementType?: FoodMeasurementType | null;
  foodNameSnapshot?: string | null;
  carbsPer100gSnapshot?: number | null;
  carbsPerUnitSnapshot?: number | null;
  carbOverride?: number | null;
}

export interface CalculatedMealItem {
  foodItemId: string;
  foodName: string;
  quantity: number;
  measurementType: FoodMeasurementType;
  weightGrams: number | null;
  carbsPer100g: number | null;
  carbsPerUnit: number | null;
  calculatedCarbs: number;
  carbOverride: number | null;
  effectiveCarbs: number;
}

export interface MealCalculation {
  foodCarbs: number;
  carbAdjustment: number;
  totalCarbs: number;
  mealBolus: number;
  correctionBolus: number;
  suggestedBolus: number;
  items: CalculatedMealItem[];
}

export interface MealSummary {
  id: string;
  mealType: MealType;
  mealTime: string;
  preMealGlucose: number;
  totalCarbs: number;
  carbAdjustment: number;
  suggestedBolus: number;
  confirmedBolus: number | null;
  notes?: string;
  foodNames: string[];
}

export interface MealDetail extends MealSummary {
  createdAt: string;
  items: {
    id: string;
    foodItemId: string;
    foodNameSnapshot: string;
    quantity: number;
    measurementType: FoodMeasurementType;
    weightGrams: number | null;
    carbsPer100gSnapshot: number | null;
    carbsPerUnitSnapshot: number | null;
    calculatedCarbs: number;
    carbOverride: number | null;
    effectiveCarbs: number;
  }[];
}

export interface DashboardMeal {
  id: string;
  mealType: MealType;
  mealTime: string;
  createdAt: string;
  totalCarbs: number;
  confirmedInsulin: number | null;
  requiresInsulinConfirmation: boolean;
}

export interface Dashboard {
  date: string;
  totalCarbs: number;
  confirmedInsulin: number;
  mealCount: number;
  meals: DashboardMeal[];
}

export interface DeliveryMeal {
  id: string;
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
  usageCount: number;
  lastUsedAt?: string;
  createdAt: string;
}

export interface DeliveryMealSections {
  favorites: DeliveryMeal[];
  mostUsed: DeliveryMeal[];
  recentlyUsed: DeliveryMeal[];
  searchResults: DeliveryMeal[];
}

export interface UseDeliveryMeal {
  id: string;
  carbs: number;
  usualInsulinUnits: number;
  notes: string;
}

export type SupplyStatus = 'Ok' | 'Low' | 'Critical' | 'Unknown';

export interface SupplyItem {
  id: string;
  name: string;
  currentQuantity: number;
  unit: string;
  dailyUsage: number;
  lowStockThresholdDays: number;
  lastUpdatedAt: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface SupplyCheckResult {
  id: string;
  name: string;
  currentQuantity: number;
  unit: string;
  dailyUsage: number;
  lowStockThresholdDays: number;
  daysLeft: number | null;
  estimatedRunOutDate: string | null;
  status: SupplyStatus;
}

