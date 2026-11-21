import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { TimeoutError } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { environment } from '../../environments/environment';

describe('ApiService assistant candidate selection', () => {
  it('sends structured order identities and expected revision without asserted provenance', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ApiService).reuseDeliveryMealItems('order', ['item-1', 'item-2'], 4).subscribe();
    const request = http.expectOne(environment.apiUrl + '/delivery-meals/order/items-draft');
    expect(request.request.body).toEqual({ itemIds: ['item-1', 'item-2'], expectedRevision: 4 });
    request.flush([]); http.verify();
  });
  it('requests carb verification using only the expected server revision', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ApiService).verifyMealCarbs('meal-id', 3).subscribe();
    const request = http.expectOne(environment.apiUrl + '/meals/meal-id/carb-verification');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ expectedCarbRevision: 3 });
    request.flush({});
    http.verify();
  });
  it('sends the expected draft version as part of the retry envelope', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ApiService).sendMealAssistantMessage('conversation', 'Add bread', 'request', undefined, 4).subscribe();
    const request = http.expectOne(environment.apiUrl + '/meal-assistant/messages');
    expect(request.request.body.expectedDraftVersion).toBe(4);
    request.flush({ conversationId: 'conversation', message: 'Draft', meals: [], draftVersion: 5 });
    http.verify();
  });

  it('times out without automatically resubmitting a mutation', fakeAsync(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    let observed: unknown;
    TestBed.inject(ApiService).sendMealAssistantMessage('conversation', 'Add bread', 'request', undefined, 1)
      .subscribe({ error: error => observed = error });
    const request = http.expectOne(environment.apiUrl + '/meal-assistant/messages');
    tick(90001);
    expect(observed instanceof TimeoutError).toBeTrue();
    expect(request.cancelled).toBeTrue();
    http.expectNone(environment.apiUrl + '/meal-assistant/messages');
    http.verify();
  }));

  it('posts an identity and quantity without authoritative carbohydrate data', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ApiService);
    const http = TestBed.inject(HttpTestingController);
    service.sendMealAssistantMessage('conversation', 'Use salad', 'request', {
      foodId: 'selected-id', quantity: 2, unit: 'Portion'
    }).subscribe();
    const request = http.expectOne(environment.apiUrl + '/meal-assistant/messages');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ conversationId: 'conversation', message: 'Use salad', requestId: 'request',
      selection: { foodId: 'selected-id', quantity: 2, unit: 'Portion' } });
    request.flush({ conversationId: 'conversation', message: 'Draft', meals: [] });
    http.verify();
  });
});
