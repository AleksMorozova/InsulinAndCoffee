import { CalculatedMealItem, CarbProvenance, FoodMeasurementType, MealType } from './models';

export interface AssistantFoodSelection { foodId: string; quantity?: number; unit?: FoodMeasurementType; kind?: 'Food' | 'DeliveryMealItem' | 'DeliveryMeal'; }
export interface AssistantOrderItem extends AssistantSavedFood { orderId: string; placeName: string; orderName: string; quantity: number; effectiveCarbs: number; }
export interface AssistantSavedFood { id: string; name: string; unit: FoodMeasurementType; carbsPer100g: number | null; carbsPerUnit: number | null; provenance?: CarbProvenance | null; }

export interface MealAssistantResponse {
  draftVersion?: number;
  requestId?: string | null;
  replayed?: boolean;
  conversationId: string;
  message: string;
  draft?: AssistantDraft | null;
  knowledge?: {
    foods: AssistantSavedFood[];
    orders: AssistantOrder[];
    items?: AssistantOrderItem[];
  } | null;
  meals: {
    carbsVerifiedAt?: string | null;
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
      provenance?: CarbProvenance | null;
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

export interface AssistantDraftState { draftVersion: number; draft: AssistantDraft | null; }

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
  provenance?: CarbProvenance | null;
  id: string;
  placeName: string;
  dishName: string;
  portionDescription: string;
  carbs: number;
  lastUsedAt: string | null;
}
