import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AiMonitoringOverview } from '../../core/ai-monitoring.models';
import { AiMonitoringComponent } from './ai-monitoring.component';

describe('AI Monitoring', () => {
  let fixture: ComponentFixture<AiMonitoringComponent>;
  let api: jasmine.SpyObj<ApiService>;
  const overview: AiMonitoringOverview = { totals: { events: 2, providerRequests: 2, successfulRequests: 1, failedRequests: 1, cancelledRequests: 0,
    inputTokens: 1000, outputTokens: 100, missingUsageRequests: 1, estimatedCostUsd: .0025, unpricedRequests: 1, averageCostUsd: .0025,
    averageLatencyMs: 200, errorRate: .5, cacheHits: 0, cacheMisses: 0, cacheBypasses: 2, cacheHitRate: null, avoidedProviderRequests: 0 },
    daily: [{ key: '2026-11-22', requests: 2, inputTokens: 1000, outputTokens: 100, estimatedCostUsd: .0025, unpricedRequests: 1 }],
    models: [], features: [] };
  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['getAiOverview', 'getAiRequests', 'getAiPricing', 'getAiRequest', 'createAiPricing']);
    api.getAiOverview.and.returnValue(of(overview));
    api.getAiRequests.and.returnValue(of({ items: [], page: 1, pageSize: 25, totalCount: 26, totalPages: 2 }));
    api.getAiPricing.and.returnValue(of([]));
    await TestBed.configureTestingModule({ imports: [AiMonitoringComponent], providers: [{ provide: ApiService, useValue: api }] }).compileComponents();
    fixture = TestBed.createComponent(AiMonitoringComponent); fixture.detectChanges();
  });
  it('renders estimates, unknowns and cache bypasses without inventing spend or savings', () => {
    const page: HTMLElement = fixture.nativeElement;
    expect(page.textContent).toContain('Total estimated spend');
    expect(page.textContent).toContain('1 requests without cost');
    expect(page.textContent).toContain('Not enabled');
    expect(page.textContent).toContain('2 bypasses');
    expect(page.textContent).toContain('Savings unavailable');
    expect(fixture.componentInstance.usd(null)).toBe('Unavailable');
    expect(fixture.componentInstance.usd(0)).toBe('0.00');
    expect(page.querySelectorAll('.bar').length).toBe(3);
  });
  it('passes local calendar boundaries as UTC, filters and pagination to the existing API service', () => {
    const component = fixture.componentInstance;
    component.period = 'custom'; component.from = '2026-11-01'; component.to = '2026-11-07';
    component.userId = 'owner'; component.model = 'model'; component.status = 'Failure'; component.sort = 'cost';
    component.load(2);
    expect(api.getAiRequests).toHaveBeenCalledWith(jasmine.objectContaining({ from: new Date('2026-11-01T00:00:00').toISOString(),
      to: new Date('2026-11-08T00:00:00').toISOString(), userId: 'owner', model: 'model', status: 'Failure' }), 2, 'cost', true);
  });
  it('appends a pricing version and leaves historical analytics unchanged', () => {
    const component = fixture.componentInstance;
    component.priceModel = 'test-model'; component.inputPrice = 2; component.cachedPrice = .5; component.outputPrice = 8;
    component.effectiveFrom = '2027-01-01T00:00';
    api.createAiPricing.and.returnValue(of({ id: 'price-version', provider: 'OpenAI', model: 'test-model', inputPricePerMillionTokens: 2,
      cachedInputPricePerMillionTokens: .5, outputPricePerMillionTokens: 8, effectiveFromUtc: new Date(component.effectiveFrom).toISOString(), effectiveToUtc: null }));
    component.savePrice();
    expect(component.prices[0].id).toBe('price-version'); expect(component.overview?.totals.estimatedCostUsd).toBe(.0025);
    expect(component.pricingMessage).toContain('Historical costs are unchanged');
  });
  it('reports real API errors and rejects invalid custom dates before requesting data', () => {
    const component = fixture.componentInstance;
    component.period = 'custom'; component.load(1); expect(component.error).toContain('valid start and end');
    api.getAiOverview.and.returnValue(throwError(() => new Error('database failure')));
    component.period = 'today'; component.load(1); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('Could not load AI monitoring');
  });
});
