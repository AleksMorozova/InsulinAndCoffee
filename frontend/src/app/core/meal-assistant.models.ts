import { CalculatedMealItem, FoodMeasurementType, MealType } from './models';

export interface AssistantFoodSelection { foodId: string; quantity?: number; unit?: FoodMeasurementType; }
export interface AssistantSavedFood { id: string; name: string; unit: FoodMeasurementType; carbsPer100g: number | null; carbsPerUnit: number | null; }

export interface MealAssistantResponse {
  conversationId: string;
  message: string;
  draft?: AssistantDraft | null;
  knowledge?: {
    foods: AssistantSavedFood[];
    orders: AssistantOrder[];
  } | null;
  meals: {
    id: string;
    mealTime: string;
    mealType: MealType;
    totalCarbs: number;
    suggestedBolus: number;
    confirmedBolus: number | null;
    match: string;
    preMealGlucose: number;
    glucoseReadings: { value: number; readingTime: string; readingType: string }[];
    canUseInCalculator: boolean;
    items: {
      name: string;
      quantity: number;
      unit: FoodMeasurementType;
      carbs: number;
      requestedQuantity: number | null;
      requestedUnit: FoodMeasurementType | null;
      estimatedCarbs: number | null;
    }[];
  }[];
}

export interface AssistantDraft {
  editFeedback?: string | null;
  sources?: { recordId: string; kind: string; name: string }[] | null;
  items: CalculatedMealItem[];
  issues: string[];
  totalCarbs: number;
}

export interface AssistantConversation {
  id: string;
  title: string;
  updatedAt: string;
}

export interface AssistantTurn {
  question: string;
  answer: MealAssistantResponse;
}
export function assistantFoodName(name: string): string {
  return name.replace(/^\[Meal Assistant(?: local fixture v1)?\]\s*/i, '').trim() || 'Unresolved food';
}

export interface AssistantOrder {
  id: string;
  placeName: string;
  dishName: string;
  portionDescription: string;
  carbs: number;
  lastUsedAt: string | null;
}
