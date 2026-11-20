import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError, TimeoutError } from 'rxjs';
import { ApiService } from '../core/api.service';
import { MealAssistantResponse } from '../core/meal-assistant.models';
import { MealAssistantComponent } from './meal-assistant.component';

describe('MealAssistantComponent', () => {
  let fixture: ComponentFixture<MealAssistantComponent>;
  let api: jasmine.SpyObj<ApiService>;

  const response: MealAssistantResponse = {
    conversationId: 'conversation',
    message: 'Here are saved meals.',
    meals: [{
      id: 'saved-meal', mealTime: '2026-11-01T12:00:00Z', mealType: 'Lunch', totalCarbs: 20,
      suggestedBolus: 2, confirmedBolus: null, preMealGlucose: 6, glucoseReadings: [], canUseInCalculator: true, match: 'Exact composition',
      items: [{ name: 'Porridge', quantity: 100, unit: 'Grams', carbs: 20, requestedQuantity: 150, requestedUnit: 'Grams', estimatedCarbs: 30 }]
    }]
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['sendMealAssistantMessage', 'getMeal', 'getMealAssistantConversations', 'getMealAssistantConversation', 'getMealAssistantCalculatorDraft', 'getMealAssistantDraftState']);
    api.getMealAssistantConversations.and.returnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [MealAssistantComponent],
      providers: [provideRouter([]), { provide: ApiService, useValue: api }]
    }).compileComponents();
    fixture = TestBed.createComponent(MealAssistantComponent);
  });

  it('selects a saved candidate by id and retries only that selection with the same request id', () => {
    const food = { id: 'chosen-food', name: 'Salad', unit: 'Portion' as const, carbsPer100g: null, carbsPerUnit: 20 };
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Offline')));
    const component = fixture.componentInstance;
    component.candidateQuantities[food.id] = 2;
    component.selectFood(food);
    const first = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(first[3]).toEqual({ foodId: food.id, quantity: 2, unit: 'Portion' });
    expect(component.message).toBe('Use Salad');

    api.sendMealAssistantMessage.and.returnValue(of(response));
    component.send();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(first);
    expect(component.turns.length).toBe(1);
  });

  it('preserves conflicting input and requires refresh plus explicit resubmission with a new version and request id', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation';
    component.draftVersion = 1;
    component.message = 'Add bread';
    api.sendMealAssistantMessage.and.returnValue(throwError(() => ({
      status: 409, code: 'draft_version_conflict', message: 'Draft changed', isApiError: true
    })));
    component.send();
    const first = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(component.message).toBe('Add bread');
    expect(component.draftConflict).toBeTrue();
    component.send();
    expect(api.sendMealAssistantMessage.calls.count()).toBe(1);

    const latest = { items: [], issues: [], totalCarbs: 0 };
    api.getMealAssistantConversation.and.returnValue(of([]));
    api.getMealAssistantDraftState.and.returnValue(of({ draftVersion: 2, draft: latest }));
    component.refreshDraft();
    expect(component.message).toBe('Add bread');
    expect(component.draftVersion).toBe(2);
    expect(component.activeDraft).toEqual(latest);
    expect(component.draftConflict).toBeFalse();
    expect(api.sendMealAssistantMessage.calls.count()).toBe(1);

    api.sendMealAssistantMessage.and.returnValue(of({ ...response, draft: latest, draftVersion: 3 }));
    component.send();
    const next = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(next[2]).not.toBe(first[2]);
    expect(next[4]).toBe(2);
  });

  it('keeps the pending input and conflict state if refreshing fails', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation';
    component.message = 'Keep this edit';
    component.draftConflict = true;
    api.getMealAssistantConversation.and.returnValue(of([]));
    api.getMealAssistantDraftState.and.returnValue(throwError(() => new Error('Offline')));
    component.refreshDraft();
    expect(component.message).toBe('Keep this edit');
    expect(component.draftConflict).toBeTrue();
  });

  it('retries a timeout with the original envelope and recognizes a previously completed response', () => {
    const component = fixture.componentInstance;
    component.message = 'Add bread';
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new TimeoutError()));
    component.send();
    const first = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(component.error).toContain('timed out');
    api.sendMealAssistantMessage.and.returnValue(of({ ...response, requestId: first[2], replayed: true, draftVersion: 1 }));
    component.send();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(first);
    expect(component.turns.length).toBe(1);
    expect(component.draftConflict).toBeTrue();
  });

  it('does not append a second visible copy of an already displayed request result', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation';
    component.turns = [{ question: 'Add bread', answer: { ...response, requestId: 'completed-request', draftVersion: 1 } }];
    component.message = 'Add bread';
    api.sendMealAssistantMessage.and.returnValue(of({ ...response, requestId: 'completed-request', draftVersion: 1, replayed: true }));
    component.send();
    expect(component.turns.length).toBe(1);
  });

  it('prevents duplicate submits and preserves text typed while an earlier request is pending', () => {
    const pending = new Subject<MealAssistantResponse>();
    api.sendMealAssistantMessage.and.returnValue(pending);
    const component = fixture.componentInstance;
    component.message = 'First message';
    component.send();
    component.send();
    expect(api.sendMealAssistantMessage.calls.count()).toBe(1);
    component.message = 'Unsent follow-up';
    pending.next(response);
    pending.complete();
    expect(component.message).toBe('Unsent follow-up');
    expect(component.turns.length).toBe(1);
  });

  it('keeps the original failed envelope when reopening the same conversation', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation';
    component.draftVersion = 1;
    component.message = 'Add bread';
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new TimeoutError()));
    component.send();
    const first = api.sendMealAssistantMessage.calls.mostRecent().args;
    api.getMealAssistantConversation.and.returnValue(of([{ question: 'Other edit', answer: { ...response, draftVersion: 2 } }]));
    component.openChat('conversation');
    expect(component.draftVersion).toBe(2);
    expect(component.message).toBe('Add bread');
    api.sendMealAssistantMessage.and.returnValue(of({ ...response, draftVersion: 2, requestId: first[2], replayed: true }));
    component.send();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(first);
  });

  it('creates a new request when the user changes a failed candidate quantity', () => {
    const food = { id: 'chosen-food', name: 'Salad', unit: 'Portion' as const, carbsPer100g: null, carbsPerUnit: 20 };
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Offline')));
    const component = fixture.componentInstance;
    component.candidateQuantities[food.id] = 2;
    component.selectFood(food);
    const first = api.sendMealAssistantMessage.calls.mostRecent().args;
    component.candidateQuantities[food.id] = 3;
    component.selectFood(food);
    const second = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(second[2]).not.toBe(first[2]);
    expect(second[3]?.quantity).toBe(3);
  });

  it('shows progress, renders saved facts and submits follow-ups with the same conversation', () => {
    const pending = new Subject<MealAssistantResponse>();
    api.sendMealAssistantMessage.and.returnValue(pending);
    const component = fixture.componentInstance;
    component.message = 'Find porridge';
    component.send();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role=status]').textContent).toContain('Searching');
    expect(api.sendMealAssistantMessage).toHaveBeenCalledWith(null, 'Find porridge', jasmine.any(String), undefined, 0);
    pending.next(response);
    pending.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Actual dose: Not confirmed');
    expect(fixture.nativeElement.textContent).toContain('Estimated 30 g carbs');
    expect(fixture.nativeElement.querySelector('a').getAttribute('href')).toBe('/meals/saved-meal');
    api.sendMealAssistantMessage.and.returnValue(of(response));
    component.message = 'What about 180g?';
    component.send();
    expect(api.sendMealAssistantMessage).toHaveBeenCalledWith('conversation', 'What about 180g?', jasmine.any(String), undefined, 0);
  });

  it('restores a draft, requires acknowledgment, and sends every matched item to the calculator', async () => {
    const items = ['Borscht', 'Pizza'].map((foodName, index) => ({
      foodItemId: foodName, foodName, quantity: index + 1, measurementType: 'Portion' as const,
      weightGrams: null, carbsPer100g: null, carbsPerUnit: 20, calculatedCarbs: 20,
      carbOverride: index === 0 ? 10 : null, effectiveCarbs: 20
    }));
    const draft = { items, issues: ['Missing salad'], totalCarbs: 40 };
    api.getMealAssistantConversation.and.returnValue(of([{ question: 'Borscht and pizza', answer: { ...response, draft } }]));
    api.getMealAssistantCalculatorDraft.and.returnValue(of(items));
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const component = fixture.componentInstance;
    component.openChat('conversation');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Matched carbs: 40 g');
    await component.useDraft(draft);
    expect(navigate).not.toHaveBeenCalled();
    await component.useDraft(draft, true);
    expect(navigate).toHaveBeenCalledWith(['/calculator'], { state: { assistantLocale: 'en', assistantMealItems: [
      { foodItemId: 'Borscht', quantity: 1, measurementType: 'Portion', foodNameSnapshot: 'Borscht', carbsPer100gSnapshot: null, carbsPerUnitSnapshot: 20, carbOverride: 10 },
      { foodItemId: 'Pizza', quantity: 2, measurementType: 'Portion', foodNameSnapshot: 'Pizza', carbsPer100gSnapshot: null, carbsPerUnitSnapshot: 20, carbOverride: null }
    ] } });
  });

  it('renders five historical totals and one live summary across a lookup and clarification', () => {
    const component = fixture.componentInstance;
    const item = {
      foodItemId: 'pizza', foodName: 'Pizza', quantity: 1, measurementType: 'Piece' as const,
      weightGrams: null, carbsPer100g: null, carbsPerUnit: 30, calculatedCarbs: 30,
      carbOverride: null, effectiveCarbs: 30
    };
    const question = 'Change my meal';
    const edits = [50, 80, 60, 123, 130].map(totalCarbs => ({
      question, answer: { ...response, meals: [], draft: { items: [item], issues: [], totalCarbs } }
    }));
    const latest = edits[4].answer.draft;
    api.getMealAssistantConversation.and.returnValue(of([
      ...edits,
      { question, answer: { ...response, meals: [], draft: { ...latest, editFeedback: 'Which item?' }, message: 'Which item?' } },
      { question: 'Find history', answer: response }
    ]));
    component.openChat('conversation');
    fixture.detectChanges();

    const page: HTMLElement = fixture.nativeElement;
    expect(page.querySelectorAll('.live-meal').length).toBe(1);
    expect(page.querySelector('.live-meal summary')?.textContent).toContain('130 g');
    const transcript = page.querySelector('.transcript');
    for (const total of [50, 80, 60, 123, 130]) {
      expect(transcript?.textContent).toContain('Total carbohydrates: ' + total + ' g');
    }
    expect(transcript?.textContent).toContain('Which item?');
    expect(transcript?.querySelector('.food-line')).toBeNull();
    expect(page.querySelector('.live-meal')?.nextElementSibling?.tagName).toBe('FORM');
  });

  it('keeps failed questions available for retry', () => {
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Offline')));
    fixture.componentInstance.message = 'Find porridge';
    fixture.componentInstance.send();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('Try again');
    expect(fixture.componentInstance.message).toBe('Find porridge');
    expect(fixture.componentInstance.loading).toBeFalse();
  });

  it('loads the original and transfers snapshots and adjustment without historical medical values', () => {
    const items = [{
      id: 'historical-item', foodItemId: 'deleted-food', foodNameSnapshot: 'Original soup',
      quantity: 150, measurementType: 'Grams' as const, weightGrams: 150,
      carbsPer100gSnapshot: 10, carbsPerUnitSnapshot: null, calculatedCarbs: 15,
      carbOverride: 18, effectiveCarbs: 18
    }];
    api.getMeal.and.returnValue(of({
      id: 'saved-meal', mealType: 'Lunch', mealTime: '', createdAt: '', foodNames: [],
      preMealGlucose: 10, totalCarbs: 20, carbAdjustment: 1, suggestedBolus: 3, confirmedBolus: 2,
      items
    }));
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    fixture.componentInstance.useMeal('saved-meal');

    expect(api.getMeal).toHaveBeenCalledWith('saved-meal');
    expect(navigate).toHaveBeenCalledWith(['/calculator'], { state: {
      assistantMealItems: items, assistantCarbAdjustment: 1, assistantOriginalMealId: 'saved-meal'
    } });
  });
});
