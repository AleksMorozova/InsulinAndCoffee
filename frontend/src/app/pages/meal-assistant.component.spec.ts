import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError, TimeoutError } from 'rxjs';
import { ApiService } from '../core/api.service';
import { MealAssistantResponse, AssistantUnresolvedItem } from '../core/meal-assistant.models';
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
    api.getMealAssistantDraftState.and.returnValue(throwError(() => new Error('No refresh response configured')));
    api.getMealAssistantConversations.and.returnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [MealAssistantComponent],
      providers: [provideRouter([]), { provide: ApiService, useValue: api }]
    }).compileComponents();
    fixture = TestBed.createComponent(MealAssistantComponent);
  });

  const california = { foodItemId: '00000000-0000-0000-0000-000000000000', foodName: 'Калифорния', quantity: 1, measurementType: 'Portion' as const,
    weightGrams: null, carbsPer100g: null, carbsPerUnit: 60, calculatedCarbs: 60, carbOverride: 60, effectiveCarbs: 60, sourceDeliveryMealItemId: 'california' };
  const potato: AssistantUnresolvedItem = { id: 'potato', originalText: '120 жареной картошки', query: { name: 'жареной картошки', quantity: 120, unit: 'Grams', unitInferred: true },
    reason: 'NoMatches', detail: 'Не найден сохранённый продукт: жареная картошка.', candidates: [] };
  const partial: MealAssistantResponse = { conversationId: 'conversation', message: 'Приём пищи собран частично.', meals: [], draftVersion: 1, resultStatus: 'PartiallyCompleted',
    draft: { items: [california], issues: [potato.detail], totalCarbs: 60, unresolved: [potato] } };

  it('keeps reply and privacy text above AA contrast on cream at phone and desktop widths', async () => {
    fixture.componentInstance.turns = [{ question: 'Saved meals?', answer: response }];
    fixture.detectChanges(); await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    await inResponsiveFrame(host, viewport => {
      const cream = host.querySelector('.chat');
      if (!cream) throw new Error('Missing chat');
      const luminance = (color: string) => {
        const rgb = color.match(/[\d.]+/g)?.slice(0, 3).map(Number);
        if (!rgb || rgb.length !== 3) throw new Error('Invalid computed color');
        const linear = rgb.map(value => value / 255 <= .04045 ? value / 255 / 12.92 : ((value / 255 + .055) / 1.055) ** 2.4);
        return linear[0] * .2126 + linear[1] * .7152 + linear[2] * .0722;
      };
      const background = luminance(viewport.getComputedStyle(cream).backgroundColor);
      for (const selector of ['.reply-text p', '.privacy', '.privacy p']) {
        const text = host.querySelector(selector);
        if (!text) throw new Error('Missing contrast target');
        const foreground = luminance(viewport.getComputedStyle(text).color);
        expect((Math.max(background, foreground) + .05) / (Math.min(background, foreground) + .05)).toBeGreaterThanOrEqual(4.5);
      }
    }, [[375, 812], [1440, 900]]);
  });

  it('shows known carbs and the original unresolved quantity separately with contextual controls', async () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation'; component.turns = [{ question: 'хочу съесть калифорнию и 120 жареной картошки', answer: partial }]; component.mealSummaryOpen = true;
    fixture.detectChanges(); await fixture.whenStable();
    const panel: HTMLElement = fixture.nativeElement.querySelector('.live-meal');
    expect(panel.querySelectorAll('.food-line').length).toBe(1);
    expect(panel.querySelector('.unresolved-item')?.textContent).toContain('120 жареной картошки');
    expect(panel.querySelector('.unresolved-item')?.textContent).toContain('автоматически');
    expect(panel.querySelector<HTMLInputElement>('.clarification-fields input[type=number]')?.value).toBe('120');
    expect(panel.querySelector('.partial-cta')).not.toBeNull();
    expect(component.responseText(component.turns[0], 0)).toContain('60');
    expect(component.responseText(component.turns[0], 0)).not.toContain('Не удалось');
  });

  it('keeps a failed manual clarification envelope, retries it safely and transfers both items to Calculator', async () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation'; component.draftVersion = 1;
    component.turns = [{ question: 'хочу съесть калифорнию и 120 жареной картошки', answer: partial }];
    component.missingCarbs[potato.id] = 25;
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new TimeoutError()));
    component.resolveMissing(potato, 'manual');
    const envelope = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(envelope[6]).toEqual(jasmine.objectContaining({ unresolvedId: 'potato', action: 'manual', quantity: 120, unit: 'Grams', carbs: 25 }));
    expect(component.activeDraft?.totalCarbs).toBe(60);
    const manual = { ...california, foodName: 'жареная картошка', quantity: 120, measurementType: 'Grams' as const, carbsPer100g: 20.83, carbsPerUnit: null,
      weightGrams: 120, carbOverride: 25, calculatedCarbs: 25, effectiveCarbs: 25, sourceDeliveryMealItemId: undefined };
    const complete: MealAssistantResponse = { ...partial, draftVersion: 2, resultStatus: 'Completed', draft: { items: [california, manual], issues: [], unresolved: [], totalCarbs: 85 } };
    api.sendMealAssistantMessage.and.returnValue(of(complete)); component.retryFailed();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(envelope);
    expect(component.activeDraft?.items.length).toBe(2); expect(component.activeDraft?.totalCarbs).toBe(85);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    api.getMealAssistantCalculatorDraft.and.returnValue(of([california, manual]));
    const draft = component.activeDraft; if (!draft) throw new Error('Expected complete meal');
    await component.useDraft(draft);
    expect(navigate).toHaveBeenCalledWith(['/calculator'], jasmine.objectContaining({ state: jasmine.objectContaining({ assistantMealItems: [
      jasmine.objectContaining({ sourceDeliveryMealItemId: 'california', carbOverride: 60 }),
      jasmine.objectContaining({ quantity: 120, measurementType: 'Grams', carbOverride: 25 })] }) }));
  });

  it('targets one unresolved item when choosing a saved source or entering zero carbs', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation'; component.turns = [{ question: 'добавь картошку', answer: partial }];
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Offline')));
    component.resolveMissing(potato, 'select', { id: 'saved-potato', kind: 'Food', name: 'Жареная картошка', score: 4 });
    expect(api.sendMealAssistantMessage.calls.mostRecent().args[6]).toEqual(jasmine.objectContaining({ unresolvedId: 'potato', sourceId: 'saved-potato', sourceKind: 'Food' }));
    component.missingCarbs[potato.id] = 0; component.resolveMissing(potato, 'manual');
    expect(api.sendMealAssistantMessage.calls.mostRecent().args[6]?.carbs).toBe(0);
  });

  it('fits clarification fields within desktop, tablet and phone Current Meal panels', async () => {
    fixture.componentInstance.turns = [{ question: 'хочу съесть калифорнию и 120 жареной картошки', answer: partial }];
    fixture.componentInstance.mealSummaryOpen = true; fixture.detectChanges(); await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement; host.style.height = 'calc(100dvh - 24px)';
    await inResponsiveFrame(host, viewport => {
      const panel = host.querySelector<HTMLDetailsElement>('.live-meal');
      if (panel) panel.open = true;
      const missing: HTMLElement | null = host.querySelector('.unresolved-item');
      const composer: HTMLElement | null = host.querySelector('.composer');
      if (!missing || !composer) throw new Error('Expected clarification controls and composer');
      expect(missing.scrollWidth).withContext('Clarification width at ' + viewport.innerWidth).toBeLessThanOrEqual(missing.clientWidth + 1);
      expect(host.scrollWidth).toBeLessThanOrEqual(host.clientWidth + 1);
      expect(composer.getBoundingClientRect().bottom).toBeLessThanOrEqual(viewport.innerHeight);
    });
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

  it('removes the exact entry ID with row status and replaces state from the response and reload', () => {
    const component = fixture.componentInstance;
    const first = { ...california, draftItemId: 'entry-a', sourceItemId: 'source-a' };
    const second = { ...california, draftItemId: 'entry-b', sourceItemId: 'source-b' };
    const initial = { ...response, draftVersion: 1, meals: [], draft: { items: [first, second], issues: [], totalCarbs: 120 } };
    component.conversationId = 'conversation'; component.draftVersion = 1;
    component.turns = [{ question: 'Добавь Калифорнию', answer: initial }]; component.mealSummaryOpen = true;
    const pending = new Subject<MealAssistantResponse>(); api.sendMealAssistantMessage.and.returnValue(pending);
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelectorAll<HTMLButtonElement>('.meal-item-actions button:last-child')[1].click(); fixture.detectChanges();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args[5]).toEqual({ itemIndex: 1, draftItemId: 'entry-b', action: 'remove' });
    expect(host.querySelectorAll('.food-line').length).toBe(2);
    expect(host.querySelectorAll('.food-line')[1].getAttribute('aria-busy')).toBe('true');
    expect(host.querySelectorAll('.food-line')[0].getAttribute('aria-busy')).toBe('false');
    expect(host.querySelector('.food-line [role=status]')?.textContent).toContain('Сохранение');
    const updated = { ...initial, draftVersion: 2, draft: { ...initial.draft, items: [first], totalCarbs: 60 } };
    pending.next(updated); pending.complete(); fixture.detectChanges();
    expect(component.activeDraft?.items.map(item => item.draftItemId)).toEqual(['entry-a']);
    expect(host.querySelectorAll('.food-line').length).toBe(1);
    expect(component.activeDraft?.totalCarbs).toBe(60);
    api.getMealAssistantConversation.and.returnValue(of([{ question: 'Remove', answer: updated }]));
    component.openChat('conversation'); fixture.detectChanges();
    expect(component.activeDraft?.items.map(item => item.draftItemId)).toEqual(['entry-a']);
    expect(api.sendMealAssistantMessage).toHaveBeenCalledTimes(1);
  });

  it('retains a failed removal and retries the exact ID and operation without optimistic deletion', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation'; component.draftVersion = 1;
    component.turns = [{ question: 'Add California', answer: { ...response, meals: [], draft: { items: [{ ...california, draftItemId: 'entry' }], issues: [], totalCarbs: 60 } } }];
    api.sendMealAssistantMessage.and.returnValue(throwError(() => new Error('Network disconnected')));
    component.editCurrentItem(0, 'remove');
    const envelope = api.sendMealAssistantMessage.calls.mostRecent().args;
    expect(component.activeDraft?.items.length).toBe(1); expect(component.error).toBeTruthy();
    component.retryFailed();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args).toEqual(envelope);
    expect(component.activeDraft?.items[0].draftItemId).toBe('entry');
  });

  it('shows the backend compound-operation summary without reinterpreting it or appending items', () => {
    const answer: MealAssistantResponse = { ...response, operationSummary: 'Убрала жареную картошку. Калифорнию оставила одну порцию. Всего: 78 г углеводов.',
      draft: { items: [california], issues: [], totalCarbs: 78 } };
    expect(fixture.componentInstance.responseText({ question: 'убери картошку и калифорнию оставь одну порцию', answer }, 0)).toBe(answer.operationSummary ?? '');
  });

  it('automatically loads authoritative state after a version conflict without retrying the mutation', () => {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation'; component.draftVersion = 1; component.message = 'Remove California';
    component.turns = [{ question: 'Add California', answer: { ...response, draft: { items: [california], issues: [], totalCarbs: 60 } } }];
    api.getMealAssistantDraftState.and.returnValue(of({ draftVersion: 2, draft: { items: [], issues: [], totalCarbs: 0 } }));
    api.sendMealAssistantMessage.and.returnValue(throwError(() => ({ status: 409, code: 'draft_version_conflict', message: 'Draft changed', isApiError: true })));
    component.send();
    expect(api.getMealAssistantDraftState).toHaveBeenCalledOnceWith('conversation');
    expect(component.activeDraft?.items.length).toBe(0); expect(component.draftVersion).toBe(2);
    expect(component.message).toBe('Remove California'); expect(component.error).toContain('Latest meal loaded');
    expect(api.sendMealAssistantMessage).toHaveBeenCalledTimes(1); expect(component.draftConflict).toBeFalse();
  });

  it('never lets a late older response replace a newer meal hydrated from history', () => {
    const component = fixture.componentInstance;
    const latest = { items: [{ ...california, draftItemId: 'latest-entry' }], issues: [], totalCarbs: 60 };
    component.conversationId = 'conversation'; component.draftVersion = 3;
    component.turns = [{ question: 'Latest meal', answer: { ...response, draft: latest, draftVersion: 3 } }];
    api.sendMealAssistantMessage.and.returnValue(of({ ...response, draft: { items: [], issues: [], totalCarbs: 0 }, draftVersion: 2 }));
    component.message = 'Edit meal'; component.send();
    expect(component.activeDraft).toEqual(latest); expect(component.draftVersion).toBe(3);
    expect(component.draftConflict).toBeTrue();
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
      transcript.scrollTop = transcript.scrollHeight;
      expect(summary.getBoundingClientRect().bottom).toBeLessThanOrEqual(composer.getBoundingClientRect().top);
      expect(summary.querySelector('input')).toBeNull();
      expect(summary.querySelector('.meal-food > small')?.textContent).toContain('1 serving');
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

  it('refreshes a stale Calculator export and waits for explicit review and retry', async () => {
    const component = fixture.componentInstance;
    const original = { items: [california], issues: [], totalCarbs: 60 };
    const updatedItem = { ...california, quantity: 2, calculatedCarbs: 120, carbOverride: 117, effectiveCarbs: 117 };
    const updated = { items: [updatedItem], issues: [], totalCarbs: 117 };
    component.conversationId = 'conversation'; component.draftVersion = 3;
    component.turns = [{ question: 'California', answer: { ...response, draft: original, draftVersion: 3 } }];
    const refresh = new Subject<{ draftVersion: number; draft: typeof updated }>();
    api.getMealAssistantDraftState.and.returnValue(refresh);
    api.getMealAssistantCalculatorDraft.and.returnValue(throwError(() => ({ status: 409, code: 'draft_version_conflict', message: 'Changed', isApiError: true })));
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    await component.useDraft(original);
    expect(api.getMealAssistantCalculatorDraft).toHaveBeenCalledOnceWith('conversation', 3);
    expect(api.getMealAssistantDraftState).toHaveBeenCalledOnceWith('conversation');
    expect(component.draftConflict).toBeTrue(); expect(navigate).not.toHaveBeenCalled();
    await component.useDraft(original); expect(api.getMealAssistantCalculatorDraft).toHaveBeenCalledTimes(1);
    refresh.next({ draftVersion: 4, draft: updated }); refresh.complete();
    expect(component.activeDraft).toBe(updated); expect(component.draftVersion).toBe(4);
    expect(component.handoffError).toContain('Review'); expect(navigate).not.toHaveBeenCalled();
    api.getMealAssistantCalculatorDraft.and.returnValue(of([updatedItem]));
    await component.useDraft(original); expect(navigate).not.toHaveBeenCalled();
    await component.useDraft(updated);
    expect(api.getMealAssistantCalculatorDraft.calls.mostRecent().args).toEqual(['conversation', 4]);
    expect(navigate).toHaveBeenCalledWith(['/calculator'], jasmine.objectContaining({ state: jasmine.objectContaining({ assistantMealItems: [
      jasmine.objectContaining({ quantity: 2, carbOverride: 117, sourceDeliveryMealItemId: 'california' })] }) }));
  });

  it('keeps Calculator export blocked when refreshing a stale meal fails', async () => {
    const component = fixture.componentInstance;
    const draft = { items: [california], issues: [], totalCarbs: 60 };
    component.conversationId = 'conversation'; component.draftVersion = 1;
    component.turns = [{ question: 'California', answer: { ...response, draft } }];
    api.getMealAssistantCalculatorDraft.and.returnValue(throwError(() => ({ status: 409, isApiError: true, message: 'Changed' })));
    api.getMealAssistantDraftState.and.returnValue(throwError(() => new Error('Offline')));
    await component.useDraft(draft); await component.useDraft(draft);
    expect(component.draftConflict).toBeTrue();
    expect(api.getMealAssistantCalculatorDraft).toHaveBeenCalledTimes(1);
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
    expect(api.getMealAssistantCalculatorDraft).toHaveBeenCalledOnceWith('conversation', 0);
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
    expect(transcript?.querySelectorAll('.live-meal .food-line').length).toBe(1);
    expect(page.querySelector('.transcript')?.nextElementSibling?.tagName).toBe('FORM');
  });

  function showCurrentMeal() {
    const component = fixture.componentInstance;
    component.conversationId = 'conversation'; component.draftVersion = 1; component.mealSummaryOpen = true;
    component.turns = [{ question: 'Добавь шаурму и гречку', answer: { ...response, meals: [], draft: {
      items: [{ ...california, foodName: 'Huli Huli · Шаурма', effectiveCarbs: 90, calculatedCarbs: 90, carbOverride: null,
        provenance: { referenceSource: 'Manual', transformation: 'HistoricalReuse' } },
        { ...california, foodName: 'Гречка', quantity: 120, measurementType: 'Grams', effectiveCarbs: 24, calculatedCarbs: 24,
          carbOverride: null, provenance: { referenceSource: 'Manual', transformation: 'Calculated' } }], issues: [], totalCarbs: 114
    } } }];
    fixture.detectChanges();
    return component;
  }

  function showDuplicateMeal(canCombine = true) {
    const component = showCurrentMeal();
    const first = { ...california, draftItemId: 'entry-a', sourceItemId: 'saved-source' };
    const second = { ...first, draftItemId: 'entry-b' };
    component.turns[0].answer.draft = { items: [first, second], issues: [], totalCarbs: 120,
      duplicateGroups: [{ draftItemIds: ['entry-a', 'entry-b'], canCombine }] };
    fixture.detectChanges(); return component;
  }

  it('keeps potential duplicates visible and offers optional combine without exposing technical IDs', () => {
    showDuplicateMeal(); const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelectorAll('.food-line').length).toBe(2);
    const controls = host.querySelector('.live-meal .source-choices');
    expect(controls?.textContent).toContain('Объединить количество'); expect(controls?.textContent).toContain('Оставить отдельно');
    expect(host.textContent).not.toContain('entry-a'); expect(host.textContent).not.toContain('saved-source');
    expect(api.sendMealAssistantMessage).not.toHaveBeenCalled();
  });

  it('does not offer Combine when carb values or units are incompatible', () => {
    showDuplicateMeal(false);
    const controls: HTMLElement | null = fixture.nativeElement.querySelector('.live-meal .source-choices');
    expect(controls?.querySelectorAll('button').length).toBe(1);
    expect(controls?.textContent).not.toContain('Объединить количество');
    expect(api.sendMealAssistantMessage).not.toHaveBeenCalled();
  });

  it('combines only through a versioned entry-ID operation and renders the authoritative result', () => {
    const component = showDuplicateMeal();
    api.sendMealAssistantMessage.and.returnValue(of({ ...response, draftVersion: 2, draft: {
      items: [{ ...california, draftItemId: 'entry-a', quantity: 2, calculatedCarbs: 120, carbOverride: 120, effectiveCarbs: 120 }], issues: [], totalCarbs: 120, duplicateGroups: []
    } }));
    component.duplicateAction(['entry-a', 'entry-b'], 'combine'); fixture.detectChanges();
    expect(api.sendMealAssistantMessage.calls.mostRecent().args[5]).toEqual({ itemIndex: 0, draftItemId: 'entry-a', action: 'combine' });
    expect(fixture.nativeElement.querySelectorAll('.food-line').length).toBe(1); expect(component.activeDraft?.totalCarbs).toBe(120);
  });

  it('keeps separate items and their saved values after acknowledging the source group and reloading', () => {
    const component = showDuplicateMeal(); const before = component.activeDraft;
    if (!before) throw new Error('Expected current meal');
    const kept = { ...before, duplicateGroups: [], separateItemIds: ['entry-a', 'entry-b'] };
    const answer = { ...response, draft: kept, draftVersion: 2 };
    api.sendMealAssistantMessage.and.returnValue(of(answer)); component.duplicateAction(['entry-a', 'entry-b'], 'keepSeparate');
    api.getMealAssistantConversation.and.returnValue(of([{ question: 'Оставить отдельно', answer }])); component.openChat('conversation'); fixture.detectChanges();
    expect(component.activeDraft?.items).toEqual(before.items); expect(fixture.nativeElement.querySelectorAll('.food-line').length).toBe(2);
    expect(fixture.nativeElement.querySelector('.live-meal .source-choices')).toBeNull();
  });

  it('preserves two independently requested pending Sushi entries with distinguishable accessible labels', () => {
    const component = showCurrentMeal();
    component.turns[0].answer.draft = { items: [], issues: ['Specify Sushi', 'Specify Sushi'], totalCarbs: 0,
      unresolved: [{ ...potato, id: 'request-1', requestedItemId: 'request-1', operationId: 'operation-1', originalText: 'Суши' },
        { ...potato, id: 'request-2', requestedItemId: 'request-2', operationId: 'operation-2', originalText: 'Суши' }] };
    fixture.detectChanges();
    const pending: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.unresolved-item');
    expect(pending.length).toBe(2); expect(pending[0].getAttribute('aria-label')).toContain('Позиция 1');
    expect(pending[1].getAttribute('aria-label')).toContain('Позиция 2');
    expect(fixture.nativeElement.textContent).not.toContain('operation-1');
  });

  it('sends one operation for an in-flight double-click Add item, with no optimistic appends', () => {
    const pending = new Subject<MealAssistantResponse>(); api.sendMealAssistantMessage.and.returnValue(pending);
    const food = { id: 'california', name: 'Калифорния', unit: 'Portion' as const, carbsPer100g: null, carbsPerUnit: 60 };
    const component = fixture.componentInstance;
    component.selectFood(food); component.selectFood(food);
    expect(api.sendMealAssistantMessage).toHaveBeenCalledTimes(1); expect(component.turns.length).toBe(0);
    pending.next({ ...response, draftVersion: 1, draft: { items: [california], issues: [], totalCarbs: 60 } }); pending.complete();
    expect(component.activeDraft?.items.length).toBe(1); expect(component.turns.length).toBe(1);
  });

  it('ignores the second click of a double-click even after a fast response, while preserving deliberate new clicks', () => {
    const component = fixture.componentInstance;
    const food = { id: 'california', name: 'Калифорния', unit: 'Portion' as const, carbsPer100g: null, carbsPerUnit: 60 };
    api.sendMealAssistantMessage.and.returnValue(of({ ...response, draftVersion: 1, draft: { items: [california], issues: [], totalCarbs: 60 } }));
    component.selectFood(food, new MouseEvent('click', { detail: 1 }));
    const first = api.sendMealAssistantMessage.calls.mostRecent().args[2];
    expect(component.loading).toBeFalse();
    component.selectFood(food, new MouseEvent('click', { detail: 2 }));
    expect(api.sendMealAssistantMessage).toHaveBeenCalledTimes(1);
    component.selectFood(food, new MouseEvent('click', { detail: 1 }));
    expect(api.sendMealAssistantMessage).toHaveBeenCalledTimes(2);
    expect(api.sendMealAssistantMessage.calls.mostRecent().args[2]).not.toBe(first);
  });

  it('separates quantity from explicitly labelled carbs and shows a compact complete summary', () => {
    showCurrentMeal();
    const page: HTMLElement = fixture.nativeElement;
    const panel = page.querySelector('.live-meal');
    expect(panel?.querySelector('summary')?.textContent).toContain('2 позиции');
    expect(panel?.querySelector('.meal-total')?.textContent).toContain('Всего: 114 г углеводов');
    const rows = page.querySelectorAll('.food-line');
    expect(rows[0].querySelector('.meal-food > small')?.textContent).toContain('1 порция');
    expect(rows[0].querySelector('.item-carbs')?.textContent).toContain('90 г углеводов');
    expect(rows[1].querySelector('.meal-food > small')?.textContent).toContain('120 г');
    expect(rows[1].querySelector('.item-carbs')?.textContent).toContain('24 г углеводов');
    expect(panel?.querySelectorAll('input').length).toBe(0);
    expect(rows[0].querySelector('.meal-source summary')?.textContent).toBe('Из истории');
    expect(panel?.querySelectorAll('button').length).toBe(5); // two icon actions per item + Calculator
  });

  it('uses a known-carb subtotal and clarification count for an incomplete meal', () => {
    showCurrentMeal();
    fixture.componentInstance.turns[0].answer.draft = { ...partial.draft, items: [california], issues: [potato.detail], unresolved: [potato], totalCarbs: 60 };
    fixture.detectChanges();
    const summary: HTMLElement | null = fixture.nativeElement.querySelector('.meal-total');
    expect(summary?.textContent).toContain('Известные углеводы: 60 г');
    expect(summary?.textContent).toContain('1 позиция');
    expect(summary?.textContent).not.toContain('Всего:');
  });

  it('opens a local quantity editor, cancels with Escape and saves with Enter through one deterministic edit', async () => {
    const component = showCurrentMeal();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.meal-icon')?.click(); fixture.detectChanges(); await fixture.whenStable();
    let input = host.querySelector<HTMLInputElement>('.quantity-editor input');
    if (!input) throw new Error('Expected quantity editor');
    input.value = '2'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); fixture.detectChanges();
    expect(host.querySelector('.quantity-editor')).toBeNull();
    expect(api.sendMealAssistantMessage).not.toHaveBeenCalled();
    component.startQuantityEdit(0); component.draftQuantities[0] = 2;
    fixture.detectChanges(); await fixture.whenStable();
    const cancel = host.querySelector<HTMLButtonElement>('.quantity-editor button:last-child');
    if (!cancel) throw new Error('Expected Cancel action');
    cancel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(api.sendMealAssistantMessage).not.toHaveBeenCalled();
    cancel.click(); fixture.detectChanges();
    expect(component.editingItem).toBeNull();
    expect(component.activeDraft?.items[0].quantity).toBe(1);
    component.startQuantityEdit(0); fixture.detectChanges(); await fixture.whenStable();
    input = host.querySelector<HTMLInputElement>('.quantity-editor input');
    if (!input) throw new Error('Expected quantity editor');
    input.value = '2'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    const updated = { ...response, draftVersion: 2, draft: { items: [{ ...california, quantity: 2 }], issues: [], totalCarbs: 120 } };
    api.sendMealAssistantMessage.and.returnValue(of(updated));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); fixture.detectChanges();
    expect(api.sendMealAssistantMessage).toHaveBeenCalledTimes(1);
    expect(api.sendMealAssistantMessage.calls.mostRecent().args[5]).toEqual({ itemIndex: 0, action: 'set', quantity: 2 });
    expect(component.editingItem).toBeNull();
    expect(host.querySelector('.quantity-editor')).toBeNull();
  });

  it('does not send unchanged, empty, zero, negative or fractional-piece quantity edits', () => {
    const component = showCurrentMeal();
    component.startQuantityEdit(0); component.saveQuantity(0);
    expect(component.editingItem).toBeNull();
    for (const quantity of [undefined, 0, -1, NaN, Infinity]) {
      component.startQuantityEdit(0); component.draftQuantities[0] = quantity; component.saveQuantity(0);
    }
    const draft = component.activeDraft;
    if (!draft) throw new Error('Expected meal draft');
    draft.items[0].measurementType = 'Piece';
    component.draftQuantities[0] = 1.5; component.saveQuantity(0);
    expect(api.sendMealAssistantMessage).not.toHaveBeenCalled();
    component.newChat(); expect(component.editingItem).toBeNull();
  });

  it('retains visible AI and unknown provenance without labelling it user-confirmed', () => {
    const component = showCurrentMeal();
    const item = component.activeDraft?.items[0];
    if (!item) throw new Error('Expected meal item');
    expect(component.mealSource({ ...item, provenance: { referenceSource: 'AiEstimate', transformation: 'HistoricalReuse' } })).toBe('Оценка AI');
    expect(component.mealSource({ ...item, provenance: null })).toBe('Источник неизвестен');
    expect(component.mealSource({ ...item, carbOverride: 0 })).toBe('Введено вручную');
  });

  it('keeps rows, editor and composer usable at 320, 390, 768, 1024, 1440 and 1920px without nested meal scrolling', async () => {
    const component = showCurrentMeal();
    component.startQuantityEdit(1); fixture.detectChanges(); await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement; host.style.height = 'calc(100dvh - 24px)';
    await inResponsiveFrame(host, viewport => {
      const panel = host.querySelector<HTMLDetailsElement>('.live-meal');
      if (!panel) throw new Error('Expected meal panel');
      panel.open = true;
      for (const element of Array.from(host.querySelectorAll<HTMLElement>('.food-line,.quantity-editor,.live-meal-items'))) {
        expect(element.scrollWidth).withContext('Overflow at ' + viewport.innerWidth).toBeLessThanOrEqual(element.clientWidth + 1);
      }
      const items = host.querySelector<HTMLElement>('.live-meal-items');
      if (!items) throw new Error('Expected meal content');
      expect(viewport.getComputedStyle(items).overflowY).toBe('visible');
      const composer = host.querySelector<HTMLElement>('.composer');
      expect(composer?.getBoundingClientRect().bottom).toBeLessThanOrEqual(viewport.innerHeight);
      expect(host.scrollWidth).toBeLessThanOrEqual(host.clientWidth + 1);
      expect(panel.getBoundingClientRect().width).toBeLessThanOrEqual(834);
    }, [[320, 740], [390, 844], [768, 900], [1024, 900], [1440, 1000], [1920, 1080]]);
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
