import { CalculatedMealItem, CarbProvenance, FoodMeasurementType, MealType } from './models';

export interface AssistantFoodSelection { foodId: string; quantity?: number; unit?: FoodMeasurementType; kind?: 'Food' | 'DeliveryMealItem' | 'DeliveryMeal'; }
export interface AssistantResolution { unresolvedId: string; action: 'search' | 'select' | 'manual' | 'remove' | 'update'; sourceId?: string; sourceKind?: 'Food' | 'DeliveryMealItem' | 'DeliveryMeal'; quantity?: number; unit?: FoodMeasurementType; carbs?: number; searchName?: string; }
export interface AssistantDraftEdit { itemIndex: number; action: 'set' | 'remove' | 'combine' | 'keepSeparate'; quantity?: number; draftItemId?: string; }
export interface AssistantUnresolvedItem { id: string; operationId?: string | null; requestedItemId?: string | null; originalText: string; query: { name: string; quantity: number | null; unit: FoodMeasurementType | null; unitInferred?: boolean; };
  reason: 'NoMatches' | 'Ambiguous' | 'QuantityRequired' | 'UnitMismatch' | 'MissingCarbs' | 'OrderUnavailable'; detail: string;
  candidates: { id: string; kind: 'Food' | 'DeliveryMealItem' | 'DeliveryMeal'; name: string; score: number; context?: string | null; }[]; }
export interface AssistantOrderItem extends AssistantSavedFood { orderId: string; placeName: string; orderName: string; quantity: number; effectiveCarbs: number; }
export interface AssistantSavedFood { id: string; name: string; unit: FoodMeasurementType; carbsPer100g: number | null; carbsPerUnit: number | null; provenance?: CarbProvenance | null; }

export interface MealAssistantResponse {
  operationSummary?: string | null;
  resultStatus?: 'Completed' | 'PartiallyCompleted' | 'NeedsClarification' | 'NoMatchesFound' | 'Failed' | null;
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
  duplicateGroups?: { draftItemIds: string[]; canCombine: boolean }[] | null;
  separateItemIds?: string[] | null;
  editFeedback?: string | null;
  sources?: { recordId: string; kind: string; name: string; unitInferred?: boolean; }[] | null;
  unresolved?: AssistantUnresolvedItem[] | null;
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
