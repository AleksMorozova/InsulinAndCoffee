import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { environment } from '../../environments/environment';

describe('ApiService assistant candidate selection', () => {
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
