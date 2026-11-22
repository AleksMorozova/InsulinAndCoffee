import { APP_BASE_HREF, BrowserPlatformLocation, PlatformLocation } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { apiErrorInterceptor } from './core/api-error.interceptor';
import { AuthService } from './core/auth.service';
import { LoggingService } from './core/logging.service';
import { CalculatedMealItem, DeliveryMeal, FoodItem, MealCalculation, MealDetail } from './core/models';
import { MealAssistantResponse } from './core/meal-assistant.models';
import { LoginComponent } from './pages/auth/login.component';
import { CalculatorComponent } from './pages/calculator/calculator.component';
import { DeliveryMealsComponent } from './pages/delivery-meals/delivery-meals.component';
import { HistoryComponent } from './pages/history/history.component';
import { MealDetailsComponent } from './pages/meal-details/meal-details.component';
import { MealAssistantComponent } from './pages/meal-assistant.component';

// Real routed browser components/services and interceptors; mocked HTTP, not live full-stack E2E.
describe('Personal Food Memory routed browser acceptance', () => {
  let http: HttpTestingController;
  let previousState: unknown;
  let previousUrl: string;
  const keys = ['access-token', 'user', 'expires-at'].map(key => 'insulin-and-coffee.' + key);
  const glucoseKey = 'insulin-and-coffee.calculator-glucose.accept-user';
  const food: FoodItem = { id: 'rice', name: 'Rice', measurementType: 'Grams', carbsPer100g: 20, carbsPerUnit: null,
    proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false, createdAt: '', provenance: { referenceSource: 'Manual', transformation: 'None' } };
  const row: CalculatedMealItem = { foodItemId: 'rice', foodName: 'Rice', quantity: 150, measurementType: 'Grams', weightGrams: 150,
    carbsPer100g: 20, carbsPerUnit: null, calculatedCarbs: 30, carbOverride: null, effectiveCarbs: 30, provenance: { referenceSource: 'Manual', transformation: 'Calculated' } };
  const meal: MealDetail = { id: 'saved-meal', mealType: 'Lunch', mealTime: '2026-11-21T12:00:00Z', createdAt: '2026-11-21T12:00:00Z',
    preMealGlucose: 6.8, totalCarbs: 30, carbAdjustment: 0, suggestedBolus: 3, confirmedBolus: null, foodNames: ['Rice'], carbRevision: 0,
    items: [{ id: 'saved-item', foodItemId: 'rice', foodNameSnapshot: 'Rice', quantity: 150, measurementType: 'Grams', weightGrams: 150,
      carbsPer100gSnapshot: 20, carbsPerUnitSnapshot: null, calculatedCarbs: 30, carbOverride: null, effectiveCarbs: 30 }] };

  beforeEach(() => {
    previousState = history.state; previousUrl = location.href; history.replaceState({}, '');
    keys.forEach(key => localStorage.removeItem(key)); sessionStorage.removeItem(glucoseKey);
    TestBed.configureTestingModule({ providers: [provideRouter(routes),
      { provide: PlatformLocation, useClass: BrowserPlatformLocation }, { provide: APP_BASE_HREF, useValue: '/' },
      provideHttpClient(withInterceptors([authInterceptor, apiErrorInterceptor])), provideHttpClientTesting(),
      { provide: LoggingService, useValue: jasmine.createSpyObj<LoggingService>('LoggingService', ['logHttpError', 'logRuntimeError']) }
    ] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    http.verify(); keys.forEach(key => localStorage.removeItem(key)); sessionStorage.removeItem(glucoseKey);
    history.replaceState(previousState, '', previousUrl);
  });
  async function login() {
    const harness = await RouterTestingHarness.create();
    const loginPage = await harness.navigateByUrl('/meal-assistant', LoginComponent);
    loginPage.form.setValue({ username: 'accept-user', password: 'acceptance-only' }); loginPage.submit();
    http.expectOne(request => request.url.endsWith('/auth/login')).flush({ accessToken: 'acceptance-token', expiresAt: new Date(Date.now() + 300000).toISOString(),
      user: { id: 'accept-user', username: 'accept-user', role: 'User', createdAt: '' } });
    await harness.fixture.whenStable();
    const assistant = await harness.navigateByUrl('/meal-assistant', MealAssistantComponent); chats(); harness.detectChanges();
    expect(TestBed.inject(AuthService).isAuthenticated()).toBeTrue(); return { harness, assistant };
  }
  function chats() { http.match(request => request.method === 'GET' && request.url.endsWith('/meal-assistant/conversations')).forEach(request => request.flush([])); }
  function foods() { http.match(request => request.method === 'GET' && request.url.endsWith('/foods')).forEach(request => request.flush({ items: [food], totalCount: 1, page: 1, pageSize: 100, totalPages: 1 })); }
  function calculation(items: CalculatedMealItem[], total: number, adjustment = 0): MealCalculation {
    return { items, foodCarbs: total - adjustment, carbAdjustment: adjustment, totalCarbs: total, mealBolus: 3, correctionBolus: 0, suggestedBolus: 3 };
  }
  function answer(items: CalculatedMealItem[], total: number, version = 1): MealAssistantResponse {
    return { conversationId: 'conversation', message: 'Saved-food draft', meals: [], draftVersion: version, draft: { items, totalCarbs: total, issues: [] } };
  }

  it('logs in, searches/selects, edits, opens Calculator, saves, explicitly verifies and retrieves History', async () => {
    const { harness, assistant } = await login();
    assistant.message = 'Rice'; assistant.send();
    const search = http.expectOne(request => request.url.endsWith('/meal-assistant/messages'));
    expect(search.request.headers.get('Authorization')).toBe('Bearer acceptance-token');
    search.flush({ conversationId: 'conversation', message: 'Saved references', meals: [], draftVersion: 0,
      knowledge: { foods: [{ id: 'rice', name: 'Rice', unit: 'Grams', carbsPer100g: 20, carbsPerUnit: null }], orders: [] } }); chats();
    assistant.candidateQuantities['rice'] = 150;
    assistant.selectFood({ id: 'rice', name: 'Rice', unit: 'Grams', carbsPer100g: 20, carbsPerUnit: null });
    const selected = http.expectOne(request => request.url.endsWith('/meal-assistant/messages'));
    expect(selected.request.body.selection).toEqual({ foodId: 'rice', quantity: 150, unit: 'Grams' }); selected.flush(answer([row], 30)); chats();
    assistant.message = 'Use 200g rice'; assistant.send();
    const edit = http.expectOne(request => request.url.endsWith('/meal-assistant/messages'));
    expect(edit.request.body.expectedDraftVersion).toBe(1);
    const larger = { ...row, quantity: 200, weightGrams: 200, calculatedCarbs: 40, effectiveCarbs: 40 };
    edit.flush(answer([larger], 40, 2)); chats();
    const draft = assistant.activeDraft; if (!draft) throw new Error('Expected active draft');
    const transfer = assistant.useDraft(draft);
    http.expectOne(request => request.url.endsWith('/calculator-draft')).flush([larger]);
    await transfer; await harness.fixture.whenStable();
    const calculator = await harness.navigateByUrl('/calculator', CalculatorComponent); foods();
    const initial = http.expectOne(request => request.url.endsWith('/meals/calculate'));
    expect(initial.request.body.preMealGlucose).toBe(5.5); expect(initial.request.body.items[0].quantity).toBe(200);
    initial.flush(calculation([larger], 40)); harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Your value');
    expect(harness.routeNativeElement?.querySelector('.compact-source')?.getAttribute('title')).toContain('Calculated from saved food');
    calculator.save(); http.expectNone(request => request.method === 'POST' && request.url.endsWith('/meals'));
    calculator.form.controls.preMealGlucose.setValue(6.8, { emitEvent: false }); calculator.recordEnteredGlucose();
    http.expectOne(request => request.url.endsWith('/meals/calculate')).flush(calculation([larger], 40)); calculator.save();
    const save = http.expectOne(request => request.method === 'POST' && request.url.endsWith('/meals'));
    expect(save.request.body.confirmedBolus).toBeNull(); expect(save.request.body.preMealGlucose).toBe(6.8);
    const saved: MealDetail = { ...meal, totalCarbs: 40, items: [{ ...meal.items[0], quantity: 200, weightGrams: 200, calculatedCarbs: 40, effectiveCarbs: 40 }] };
    save.flush(saved); await harness.fixture.whenStable();
    const details = await harness.navigateByUrl('/meals/saved-meal', MealDetailsComponent);
    http.expectOne(request => request.url.endsWith('/meals/saved-meal')).flush(saved); foods(); harness.detectChanges();
    details.verifyCarbs(saved);
    const verify = http.expectOne(request => request.url.endsWith('/carb-verification'));
    expect(verify.request.body).toEqual({ expectedCarbRevision: 0 });
    const verified = { ...saved, carbRevision: 1, carbsVerifiedAt: '2026-11-21T12:30:00Z' };
    verify.flush(verified); http.expectOne(request => request.url.endsWith('/meals/saved-meal')).flush(verified); harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Verified by you');
    await harness.navigateByUrl('/history', HistoryComponent); await new Promise(resolve => setTimeout(resolve, 220));
    http.expectOne(request => request.method === 'GET' && request.url.endsWith('/meals')).flush([saved]); harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Rice'); expect(harness.routeNativeElement?.querySelector('a[href="/meals/saved-meal"]')).not.toBeNull();
  });

  it('repeats full historical snapshots and preserves overrides/adjustment without historical glucose or insulin', async () => {
    const { harness, assistant } = await login();
    const original: MealDetail = { ...meal, id: 'original', preMealGlucose: 11, confirmedBolus: 4, totalCarbs: 52, carbAdjustment: -3,
      items: [{ ...meal.items[0], carbOverride: 25, effectiveCarbs: 25 }, { ...meal.items[0], id: 'bread-item', foodItemId: 'bread', foodNameSnapshot: 'Bread', measurementType: 'Piece',
        quantity: 2, weightGrams: null, carbsPer100gSnapshot: null, carbsPerUnitSnapshot: 15 }] };
    assistant.useMeal('original'); http.expectOne(request => request.url.endsWith('/meals/original')).flush(original); await harness.fixture.whenStable();
    const calculator = await harness.navigateByUrl('/calculator', CalculatorComponent); foods();
    const initial = http.expectOne(request => request.url.endsWith('/meals/calculate'));
    expect(initial.request.body.carbAdjustment).toBe(-3); expect(initial.request.body.preMealGlucose).toBe(5.5);
    expect(initial.request.body.items[0]).toEqual(jasmine.objectContaining({ sourceMealItemId: 'saved-item', carbOverride: 25 })); initial.flush(calculation([row], 52, -3));
    expect(calculator.form.controls.confirmedBolus.value).toBeNull(); calculator.items.at(0).patchValue({ quantity: 200 });
    const changed = http.expectOne(request => request.url.endsWith('/meals/calculate'));
    expect(changed.request.body.items[0]).toEqual(jasmine.objectContaining({ sourceMealItemId: 'saved-item', quantity: 200, carbOverride: 25, clearCarbOverride: false }));
    expect(changed.request.body.carbAdjustment).toBe(-3); changed.flush(calculation([{ ...row, quantity: 200, calculatedCarbs: 40, carbOverride: 25, effectiveCarbs: 25 }], 52, -3));
    calculator.items.at(0).patchValue({ carbOverride: null });
    const cleared = http.expectOne(request => request.url.endsWith('/meals/calculate'));
    expect(cleared.request.body.items[0].clearCarbOverride).toBeTrue(); cleared.flush(calculation([{ ...row, quantity: 200, calculatedCarbs: 40, effectiveCarbs: 40 }], 67, -3));
    expect(original.items[0].quantity).toBe(150); expect(original.items[0].carbOverride).toBe(25); expect(original.totalCarbs).toBe(52);
  });

  it('retries the same failed envelope and requires refresh/new version after conflict', async () => {
    const { harness, assistant } = await login();
    assistant.candidateQuantities['rice'] = 150; assistant.selectFood({ id: 'rice', name: 'Rice', unit: 'Grams', carbsPer100g: 20, carbsPerUnit: null });
    const failed = http.expectOne(request => request.url.endsWith('/meal-assistant/messages'));
    const envelope: unknown = failed.request.body;
    failed.flush({ status: 503, title: 'Unavailable', detail: 'Temporary failure' }, { status: 503, statusText: 'Unavailable' });
    expect(assistant.message).toContain('Rice'); expect(TestBed.inject(Router).url).toBe('/meal-assistant'); assistant.send();
    const retry = http.expectOne(request => request.url.endsWith('/meal-assistant/messages'));
    expect(retry.request.body).toEqual(envelope); retry.flush(answer([row], 30)); chats();
    assistant.message = 'Use 200g rice'; assistant.send();
    const conflict = http.expectOne(request => request.url.endsWith('/meal-assistant/messages')); const oldId: unknown = conflict.request.body.requestId;
    conflict.flush({ status: 409, title: 'Draft changed', code: 'draft_version_conflict' }, { status: 409, statusText: 'Conflict' });
    expect(assistant.draftConflict).toBeTrue(); expect(assistant.message).toBe('Use 200g rice'); assistant.send(); http.expectNone(request => request.url.endsWith('/meal-assistant/messages'));
    assistant.refreshDraft();
    http.expectOne(request => request.url.endsWith('/meal-assistant/conversations/conversation')).flush([{ question: 'Rice', answer: answer([row], 30, 2) }]);
    http.expectOne(request => request.url.endsWith('/draft-state')).flush({ draftVersion: 2, draft: { items: [row], issues: [], totalCarbs: 30 } }); assistant.send();
    const updated = http.expectOne(request => request.url.endsWith('/meal-assistant/messages'));
    expect(updated.request.body.expectedDraftVersion).toBe(2); expect(updated.request.body.requestId).not.toEqual(oldId); updated.flush(answer([row], 30, 3)); chats(); harness.detectChanges();
    expect(assistant.turns.length).toBe(2); expect(TestBed.inject(Router).url).toBe('/meal-assistant');
  });

  it('edits structured Ask Past Me, accepts a changed total, and transfers only the selected item', async () => {
    const { harness } = await login();
    const page = await harness.navigateByUrl('/delivery-meals', DeliveryMealsComponent);
    const order: DeliveryMeal = { id: 'order', placeName: 'Le Grill', dishName: 'Sushi set', portionDescription: '', carbs: 75, usualInsulinUnits: 4,
      lastPreMealGlucose: 11, resultRating: 'Unknown', tags: '', isFavorite: false, usageCount: 0, createdAt: '', revision: 0,
      items: [{ id: 'sushi-item', name: 'Sushi', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15, calculatedCarbs: 75, carbOverride: null, effectiveCarbs: 75,
        displayOrder: 0, provenance: { referenceSource: 'Manual', transformation: 'Calculated' } }] };
    http.expectOne(request => request.method === 'GET' && request.url.endsWith('/delivery-meals')).flush({ favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [order] }); harness.detectChanges();
    page.openEdit(order);
    const sushi: CalculatedMealItem = { ...row, foodItemId: '00000000-0000-0000-0000-000000000000', foodName: 'Sushi', quantity: 5,
      measurementType: 'Piece', weightGrams: null, carbsPer100g: null, carbsPerUnit: 15, calculatedCarbs: 75, effectiveCarbs: 75, sourceDeliveryMealItemId: 'sushi-item' };
    http.expectOne(request => request.url.endsWith('/preview-items')).flush({ items: [sushi], totalCarbs: 75, difference: 0 });
    page.editorItems.at(0).patchValue({ quantity: 3 }); await new Promise(resolve => setTimeout(resolve, 170));
    const smaller = { ...sushi, quantity: 3, calculatedCarbs: 45, effectiveCarbs: 45 };
    http.expectOne(request => request.url.endsWith('/preview-items')).flush({ items: [smaller], totalCarbs: 45, difference: -30 });
    expect(page.form.controls.carbs.value).toBe(75); expect(page.itemsReady).toBeFalse(); page.acceptTotal(); page.save();
    const save = http.expectOne(request => request.method === 'PUT' && request.url.endsWith('/delivery-meals/order'));
    expect(save.request.body.expectedRevision).toBe(0); expect(save.request.body.acceptCalculatedTotal).toBeTrue(); expect(save.request.body.portionDescription).toBeNull();
    expect(save.request.body.items[0]).toEqual(jasmine.objectContaining({ id: 'sushi-item', quantity: 3 }));
    const updated: DeliveryMeal = { ...order, revision: 1, carbs: 45, items: order.items?.map(item => ({ ...item, quantity: 3, calculatedCarbs: 45, effectiveCarbs: 45 })) };
    save.flush(updated); http.expectOne(request => request.method === 'GET' && request.url.endsWith('/delivery-meals')).flush({ favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [updated] });
    page.reuseItems(updated, ['sushi-item']);
    const reuse = http.expectOne(request => request.url.endsWith('/items-draft'));
    expect(reuse.request.body).toEqual({ itemIds: ['sushi-item'], expectedRevision: 1 }); reuse.flush([smaller]); await harness.fixture.whenStable();
    const calculator = await harness.navigateByUrl('/calculator', CalculatorComponent); foods();
    const calculated = http.expectOne(request => request.url.endsWith('/meals/calculate'));
    expect(calculated.request.body.items).toEqual([jasmine.objectContaining({ sourceDeliveryMealItemId: 'sushi-item', quantity: 3 })]);
    expect(calculated.request.body.preMealGlucose).toBe(5.5); calculated.flush(calculation([smaller], 45)); expect(calculator.form.controls.confirmedBolus.value).toBeNull();
  });

  it('shows precision validation without silently rounding the field or leaving Calculator', async () => {
    const { harness, assistant } = await login(); assistant.useMeal('saved-meal');
    http.expectOne(request => request.url.endsWith('/meals/saved-meal')).flush(meal); await harness.fixture.whenStable();
    const calculator = await harness.navigateByUrl('/calculator', CalculatorComponent); foods();
    http.expectOne(request => request.url.endsWith('/meals/calculate')).flush(calculation([row], 30)); calculator.items.at(0).patchValue({ quantity: 0.125 });
    http.expectOne(request => request.url.endsWith('/meals/calculate')).flush({ status: 400, title: 'Validation failed', detail: 'Quantity supports at most two decimal places so saved meal snapshots remain exact.' }, { status: 400, statusText: 'Bad Request' });
    expect(calculator.items.at(0).value.quantity).toBe(0.125); expect(calculator.error).toContain('two decimal places'); expect(calculator.calculation).toBeUndefined(); expect(TestBed.inject(Router).url).toBe('/calculator');
    calculator.items.at(0).patchValue({ quantity: 0.13 }); http.expectOne(request => request.url.endsWith('/meals/calculate')).flush(calculation([{ ...row, quantity: 0.13, calculatedCarbs: 0.03, effectiveCarbs: 0.03 }], 0.03));
    expect(calculator.error).toBe('');
  });
});
