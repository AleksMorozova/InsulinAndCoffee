export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';
export type ResultRating = 'Perfect' | 'Good' | 'HighGlucose' | 'LowGlucose' | 'Unknown';

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
  carbsPer100g: number;
  proteinPer100g: number;
  fatPer100g: number;
  caloriesPer100g: number;
  isFavorite: boolean;
  createdAt: string;
}

export interface MealItemInput {
  foodItemId: string;
  weightGrams: number;
}

export interface CalculatedMealItem {
  foodItemId: string;
  foodName: string;
  weightGrams: number;
  carbsPer100g: number;
  calculatedCarbs: number;
}

export interface MealCalculation {
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
  suggestedBolus: number;
  confirmedBolus: number;
  notes?: string;
  foodNames: string[];
}

export interface MealDetail extends MealSummary {
  createdAt: string;
  items: {
    id: string;
    foodItemId: string;
    foodNameSnapshot: string;
    weightGrams: number;
    carbsPer100gSnapshot: number;
    calculatedCarbs: number;
  }[];
}

export interface Dashboard {
  todaysTotalCarbs: number;
  todaysConfirmedInsulinUnits: number;
  lastMeal?: MealSummary;
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
