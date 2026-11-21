import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ApiService } from '../core/api.service';
import { MealSummary } from '../core/models';
import { HistoryComponent } from './history/history.component';
import { FoodsComponent } from './foods/foods.component';
import { DeliveryMealsComponent } from './delivery-meals/delivery-meals.component';
import { DashboardComponent } from './dashboard/dashboard.component';

describe('Empty account pages', () => {
  it('History shows an empty state and keeps filters usable', fakeAsync(() => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getMeals']);
    api.getMeals.and.returnValue(of([]));
    TestBed.configureTestingModule({ imports: [HistoryComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(HistoryComponent);
    fixture.detectChanges(); tick(201); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No meals yet');
    expect(fixture.nativeElement.querySelector('a[href="/calculator"]')).not.toBeNull();
    fixture.componentInstance.filters.patchValue({ search: 'soup' });
    tick(201); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No meals match these filters');
    expect(api.getMeals).toHaveBeenCalledWith('soup', '');
  }));

  it('History keeps a failed request visible and retries without a global error redirect', fakeAsync(() => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getMeals']);
    api.getMeals.and.returnValues(throwError(() => new Error('offline')), of([]));
    TestBed.configureTestingModule({ imports: [HistoryComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] });
    const navigate = spyOn(TestBed.inject(Router), 'navigate');
    const fixture = TestBed.createComponent(HistoryComponent);
    fixture.detectChanges(); tick(201); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Could not load meal history');
    expect(fixture.nativeElement.textContent).not.toContain('No meals yet');
    fixture.componentInstance.retry(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No meals yet');
    expect(api.getMeals).toHaveBeenCalledTimes(2);
    expect(navigate).not.toHaveBeenCalled();
  }));

  it('History shows loading without claiming the account has no meals', fakeAsync(() => {
    const pending = new Subject<MealSummary[]>();
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getMeals']);
    api.getMeals.and.returnValue(pending);
    TestBed.configureTestingModule({ imports: [HistoryComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(HistoryComponent);
    fixture.detectChanges(); tick(201); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading meal history');
    expect(fixture.nativeElement.textContent).not.toContain('No meals yet');
    pending.next([]); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No meals yet');
  }));

  it('Foods supports an empty library and creating the first food', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getFoods']);
    api.getFoods.and.returnValue(of([]));
    TestBed.configureTestingModule({ imports: [FoodsComponent], providers: [{ provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(FoodsComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Your food library is empty');
    fixture.componentInstance.openCreate(); fixture.detectChanges();
    expect(fixture.componentInstance.isEditorOpen).toBeTrue();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('Foods does not misrepresent an API failure as an empty library', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getFoods']);
    api.getFoods.and.returnValues(throwError(() => new Error('failed')), of([]));
    TestBed.configureTestingModule({ imports: [FoodsComponent], providers: [{ provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(FoodsComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Could not load foods');
    expect(fixture.nativeElement.textContent).not.toContain('Your food library is empty');
    fixture.componentInstance.loadFoods(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Your food library is empty');
  });

  it('Ask Past Me supports no orders and a first-order editor', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getDeliveryMeals']);
    api.getDeliveryMeals.and.returnValue(of({ favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [] }));
    TestBed.configureTestingModule({ imports: [DeliveryMealsComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(DeliveryMealsComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No counted meals yet');
    fixture.componentInstance.openCreate(); fixture.detectChanges();
    expect(fixture.componentInstance.isEditorOpen).toBeTrue();
  });

  it('Ask Past Me keeps genuine load errors separate from empty orders', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getDeliveryMeals']);
    api.getDeliveryMeals.and.returnValue(throwError(() => new Error('failed')));
    TestBed.configureTestingModule({ imports: [DeliveryMealsComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(DeliveryMealsComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Could not load saved orders');
    expect(fixture.nativeElement.textContent).not.toContain('No counted meals yet');
  });

  it('Dashboard renders zero totals and the first-meal action', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getDashboard']);
    api.getDashboard.and.returnValue(of({ date: '2026-11-21', totalCarbs: 0, confirmedInsulin: 0, mealCount: 0, meals: [] }));
    TestBed.configureTestingModule({ imports: [DashboardComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No meals yet today');
    expect(fixture.nativeElement.textContent).toContain('No pending actions');
  });
});
