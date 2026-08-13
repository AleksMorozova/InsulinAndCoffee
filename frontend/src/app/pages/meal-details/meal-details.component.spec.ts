import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, NEVER, Observable, of, throwError } from 'rxjs';
import { ApiError } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { MealDetail } from '../../core/models';
import { MealDetailsComponent } from './meal-details.component';

class ActivatedRouteStub {
  private readonly paramsSubject = new BehaviorSubject<ParamMap>(convertToParamMap({ id: 'meal-1' }));
  readonly paramMap = this.paramsSubject.asObservable();

  setMealId(id: string): void {
    this.paramsSubject.next(convertToParamMap({ id }));
  }
}

describe('MealDetailsComponent', () => {
  let fixture: ComponentFixture<MealDetailsComponent>;
  let api: jasmine.SpyObj<ApiService>;
  let route: ActivatedRouteStub;
  let router: Router;

  beforeEach(async () => {
    route = new ActivatedRouteStub();
    api = jasmine.createSpyObj<ApiService>('ApiService', [
      'getMeal',
      'getFoods',
      'confirmMealBolus',
      'addMealItems',
      'updateMealItem',
      'removeMealItem'
    ]);
    api.getFoods.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [MealDetailsComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: api },
        { provide: ActivatedRoute, useValue: route }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
  });

  it('displays a loading state while the meal request is pending', () => {
    api.getMeal.and.returnValue(NEVER);
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading meal details...');
    expect(api.getMeal).toHaveBeenCalledTimes(1);
  });

  it('displays meal details after a successful response', () => {
    api.getMeal.and.returnValue(of(createMeal()));
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Breakfast Details');
    expect(fixture.nativeElement.textContent).toContain('Sushi Rice');
    expect(api.getMeal).toHaveBeenCalledTimes(1);
  });

  it('displays contextual not-found UI for a missing meal without retry or generic error copy', () => {
    const navigateSpy = spyOn(router, 'navigate');
    api.getMeal.and.returnValue(throwError(() => apiError(404, 'Resource not found', "Meal with id 'meal-1' was not found.")));
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('We couldn’t find this meal');
    expect(text).toContain('It may have been deleted, or the link you followed is no longer available.');
    expect(text).toContain('Back to history');
    expect(text).toContain('Create a meal');
    expect(text).not.toContain('Something went wrong');
    expect(text).not.toContain('Try again');
    expect(navigateSpy).not.toHaveBeenCalledWith(['/error'], jasmine.anything());
    expect(api.getMeal).toHaveBeenCalledTimes(1);
  });

  it('uses the existing history and calculator routes for missing-meal actions', () => {
    api.getMeal.and.returnValue(throwError(() => apiError(404)));
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a')).map((link) => (link as HTMLAnchorElement).getAttribute('href'));
    expect(links).toContain('/history');
    expect(links).toContain('/calculator');
  });

  it('displays a recoverable error for server failures and retries exactly once when clicked', () => {
    api.getMeal.and.returnValues(
      throwError(() => apiError(500, 'Server error', 'Something went wrong. Please try again.')),
      of(createMeal())
    );
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load this meal');
    expect(fixture.nativeElement.textContent).toContain('Try again');
    expect(api.getMeal).toHaveBeenCalledTimes(1);

    fixture.nativeElement.querySelector('app-page-error button').click();
    fixture.detectChanges();

    expect(api.getMeal).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Breakfast Details');
  });

  it('displays a recoverable error for network failures', () => {
    api.getMeal.and.returnValue(throwError(() => apiError(0, 'Connection problem', 'Unable to connect to the server.')));
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load this meal');
    expect(fixture.nativeElement.textContent).toContain('Back to history');
  });

  it('performs one new request when the route id changes', () => {
    api.getMeal.and.returnValue(of(createMeal()));
    fixture = TestBed.createComponent(MealDetailsComponent);
    fixture.detectChanges();

    route.setMealId('meal-2');
    fixture.detectChanges();

    expect(api.getMeal).toHaveBeenCalledTimes(2);
  });
});

function apiError(status: number, title = 'Request failed', message = 'Request failed.'): ApiError {
  return {
    status,
    title,
    message,
    isApiError: true
  };
}

function createMeal(): MealDetail {
  return {
    id: 'meal-1',
    mealType: 'Breakfast',
    mealTime: '2026-06-22T10:32:06Z',
    preMealGlucose: 6.5,
    totalCarbs: 84,
    suggestedBolus: 8.4,
    confirmedBolus: null,
    notes: 'Morning sushi',
    foodNames: ['Sushi Rice'],
    createdAt: '2026-06-22T10:32:06Z',
    items: [
      {
        id: 'item-1',
        foodItemId: 'food-1',
        foodNameSnapshot: 'Sushi Rice',
        quantity: 300,
        measurementType: 'Grams',
        weightGrams: 300,
        carbsPer100gSnapshot: 28,
        carbsPerUnitSnapshot: null,
        calculatedCarbs: 84
      }
    ]
  };
}
