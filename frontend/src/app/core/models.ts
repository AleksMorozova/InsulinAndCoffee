export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';
export type ResultRating = 'Perfect' | 'Good' | 'HighGlucose' | 'LowGlucose' | 'Unknown';
export type FoodMeasurementType = 'Grams' | 'Portion' | 'Piece';
export type CarbReferenceSource = 'Unknown' | 'Manual' | 'AiEstimate' | 'Mixed';
export type CarbTransformation = 'None' | 'Calculated' | 'HistoricalReuse' | 'Calculated, HistoricalReuse';
export interface CarbProvenance {
  referenceSource: CarbReferenceSource;
  transformation: CarbTransformation;
  sourceMealVerifiedAt?: string | null;
}

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
  provenance?: CarbProvenance | null;
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

export interface LoginAttempt {
  id: string;
  username: string;
  userId: string | null;
  success: boolean;
  attemptedAtUtc: string;
  failureReason: string | null;
  ipAddress: string | null;
  userAgent: string | null;
}

export interface LoginAttemptStats {
  totalAttempts: number;
  successfulAttempts: number;
  failedAttempts: number;
  uniqueUsernames: number;
}

export interface MealItemInput {
  sourceDeliveryMealItemId?: string | null;
  sourceMealItemId?: string | null;
  sourceDeliveryMealId?: string | null;
  clearCarbOverride?: boolean;
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
  draftItemId?: string | null;
  sourceItemId?: string | null;
  sourceDeliveryMealItemId?: string | null;
  provenance?: CarbProvenance | null;
  sourceMealItemId?: string | null;
  sourceDeliveryMealId?: string | null;
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
  provenance?: CarbProvenance | null;
  carbsVerifiedAt?: string | null;
  carbRevision?: number;
  createdAt: string;
  items: {
    provenance?: CarbProvenance | null;
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
  items?: DeliveryMealItem[];
  revision?: number;
  provenance?: CarbProvenance | null;
  id: string;
  placeName: string;
  dishName: string;
  portionDescription: string;
  carbs: number;
  usualInsulinUnits: number | null;
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
  items?: CalculatedMealItem[] | null;
  provenance?: CarbProvenance | null;
  id: string;
  carbs: number;
  usualInsulinUnits: number | null;
  notes: string;
}

export interface DeliveryMealItemInput {
  id?: string | null;
  foodItemId?: string | null;
  name: string;
  quantity: number;
  measurementType: FoodMeasurementType;
  carbsPer100g?: number | null;
  carbsPerUnit?: number | null;
  carbOverride?: number | null;
}
export interface DeliveryMealItem extends DeliveryMealItemInput {
  id: string;
  calculatedCarbs: number;
  effectiveCarbs: number;
  displayOrder: number;
  provenance: CarbProvenance;
}
export interface DeliveryMealPreview { items: CalculatedMealItem[]; totalCarbs: number; difference?: number | null; }

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


export interface MealCarbPreview {
  foodCarbs: number;
  totalCarbs: number;
  items: CalculatedMealItem[];
}
