import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError, TimeoutError } from 'rxjs';
import { ApiService } from '../core/api.service';
import { MealAssistantResponse } from '../core/meal-assistant.models';
import { MealAssistantComponent } from './meal-assistant.component';
import { inResponsiveFrame } from '../testing/responsive-frame';

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
    api = jasmine.createSpyObj<ApiService>('ApiService', ['sendMealAssistantMessage', 'getMeal', 'getMealAssistantConversations', 'getMealAssistantConversation', 'getMealAssistantCalculatorDraft', 'getMealAssistantDraftState', 'renameMealAssistantConversation']);
    api.getMealAssistantConversations.and.returnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [MealAssistantComponent],
      providers: [provideRouter([]), { provide: ApiService, useValue: api }]
    }).compileComponents();
    fixture = TestBed.createComponent(MealAssistantComponent);
  });

  it('supports a first conversation with no meals, saved foods or orders', () => {
    api.sendMealAssistantMessage.and.returnValue(of({ conversationId: 'first',
      message: "I couldn't find matching meals in your saved history.", meals: [] }));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No saved conversations yet');
    fixture.componentInstance.message = 'What did I eat last time?';
    fixture.componentInstance.send();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("couldn't find matching meals");
    expect(fixture.componentInstance.error).toBe('');
    expect(fixture.componentInstance.turns.length).toBe(1);
  });

  it('prefills a saved order item quantity and provides just one whole-order action', async () => {
    fixture.componentInstance.turns = [{ question: 'California', answer: { ...response, meals: [], knowledge: { foods: [],
      items: [{ id: 'california', orderId: 'order', placeName: 'Le Gril', orderName: 'Sushi', name: 'California', quantity: 1,
        unit: 'Portion', carbsPer100g: null, carbsPerUnit: 60, effectiveCarbs: 60 }],
      orders: [{ id: 'order', placeName: 'Le Gril', dishName: 'Sushi', portionDescription: '', carbs: 135, lastUsedAt: null }] } } }];
    fixture.detectChanges();
    await fixture.whenStable();
    const input: HTMLInputElement | null = fixture.nativeElement.querySelector('.result-actions input');
    expect(input?.value).toBe('1');
    const text: string = fixture.nativeElement.textContent;
    expect(text.match(/Add entire order/g)?.length).toBe(1); expect(text).not.toContain('Reuse whole order');
  });

  it('edits the persisted current meal by index and retries the exact edit safely', () => {
    const component = fixture.componentInstance;
    const item = { foodItemId: 'food', foodName: 'Soup', quantity: 1, measurementType: 'Portion' as const, carbsPer100g: null,
      carbsPerUnit: 20, weightGrams: null, calculatedCarbs: 20, carbOverride: null, effectiveCarbs: 20 };
    component.conversationId = 'conversation'; component.draftVersion = 1;
    component.turns = [{ question: 'Add Soup', answer: { ...response, draft: { items: [item], issues: [], totalCarbs: 20 } } }];
    component.draftQuantities[0] = 2;
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Offline')));
    component.editCurrentItem(0, 'set');
    const envelope = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(envelope[5]).toEqual({ itemIndex: 0, action: 'set', quantity: 2 }); expect(envelope[4]).toBe(1);
    component.retryFailed(); expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(envelope);
  });

  it('renames conversations through the existing API service', () => {
    spyOn(window, 'prompt').and.returnValue('  Lunch plans  ');
    api.renameMealAssistantConversation.and.returnValue(of(undefined));
    fixture.componentInstance.renameChat({ id: 'conversation', title: 'Old title', updatedAt: '' });
    expect(api.renameMealAssistantConversation).toHaveBeenCalledOnceWith('conversation', 'Lunch plans');
  });

  it('keeps Russian timeout errors in Russian and preserves the failed question', () => {
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new TimeoutError()));
    fixture.componentInstance.message = 'Сколько брала на калифорнию'; fixture.componentInstance.send();
    expect(fixture.componentInstance.error).toContain('не успел');
    expect(fixture.componentInstance.failedSend?.question).toBe('Сколько брала на калифорнию');
  });

  it('keeps Ukrainian lookup controls and timeout errors in Ukrainian', () => {
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new TimeoutError()));
    fixture.componentInstance.message = 'знайди гречку';
    expect(fixture.componentInstance.ui('quantity')).toBe('Кількість');
    fixture.componentInstance.send();
    expect(fixture.componentInstance.error).toContain('не встиг');
  });

  it('keeps the expanded current meal, scrolling transcript and composer usable at desktop, tablet and phone widths', async () => {
    const component = fixture.componentInstance;
    const item = { foodItemId: 'food', foodName: 'Long saved restaurant item name with a readable quantity', quantity: 1,
      measurementType: 'Portion' as const, carbsPer100g: null, carbsPerUnit: 20, weightGrams: null, calculatedCarbs: 20, carbOverride: null, effectiveCarbs: 20 };
    component.turns = Array.from({ length: 12 }, (_, index) => ({ question: 'Question ' + index, answer: { ...response, meals: [] } }));
    component.turns.push({ question: 'Add food', answer: { ...response, meals: [], draft: { items: [item], issues: ['Unresolved: another food'], totalCarbs: 20 } } });
    component.mealSummaryOpen = true;
    fixture.detectChanges(); await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.style.height = 'calc(100dvh - 24px)';
    await inResponsiveFrame(host, viewport => {
      const transcript: HTMLElement | null = host.querySelector('.transcript');
      const composer: HTMLElement | null = host.querySelector('.composer');
      const summary: HTMLElement | null = host.querySelector('.live-meal');
      if (!transcript || !composer || !summary) throw new Error('Expected persistent chat controls');
      expect(host.scrollWidth).toBeLessThanOrEqual(host.clientWidth + 1);
      expect(transcript.scrollHeight).toBeGreaterThan(transcript.clientHeight);
      expect(viewport.getComputedStyle(transcript).overflowY).toBe('auto');
      expect(composer.getBoundingClientRect().bottom).toBeLessThanOrEqual(viewport.innerHeight);
      expect(summary.getBoundingClientRect().bottom).toBeLessThanOrEqual(composer.getBoundingClientRect().top);
      expect(summary.querySelector('input')?.value).toBe('1');
    });
  });

  it('keeps a selected order item identity, unit and quantity on retry', () => {
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Offline')));
    const item = { id: 'order-item', orderId: 'order', placeName: 'Le Grill', orderName: 'Set', name: 'Sushi', quantity: 5,
      unit: 'Piece' as const, carbsPer100g: null, carbsPerUnit: 15, effectiveCarbs: 75 };
    fixture.componentInstance.candidateQuantities[item.id] = 3;
    fixture.componentInstance.selectOrderItem(item);
    const envelope = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(envelope[3]).toEqual({ foodId: item.id, quantity: 3, unit: 'Piece', kind: 'DeliveryMealItem' });
    api.sendMealAssistantMessage.and.returnValue(of(response));
    fixture.componentInstance.send();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(envelope);
    expect(fixture.componentInstance.turns.length).toBe(1);
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
      assistantMealItems: items.map(item => ({ ...item, sourceMealItemId: item.id })), assistantCarbAdjustment: 1, assistantOriginalMealId: 'saved-meal'
    } });
  });
});
