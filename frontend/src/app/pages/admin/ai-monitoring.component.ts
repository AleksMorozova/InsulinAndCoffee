import { Component, DestroyRef, inject } from '@angular/core';
import { DatePipe, DecimalPipe, JsonPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { getApiErrorMessage } from '../../core/api-error';
import { AiModelPricing, AiMonitoringOverview, AiUsageBucket, AiUsageFilters, AiUsageRecord } from '../../core/ai-monitoring.models';

@Component({
  standalone: true,
  imports: [FormsModule, DatePipe, DecimalPipe, PercentPipe, JsonPipe],
  template: `
    <header><p>Administration</p><h1>AI Monitoring</h1><p>Estimated costs from provider-reported usage, not provider billing. No conversation content is stored here.</p></header>
    <form class="filters" (ngSubmit)="load(1)">
      <label>Period<select name="period" [(ngModel)]="period"><option value="today">Today</option><option value="7">Last 7 days</option><option value="30">Last 30 days</option><option value="month">This month</option><option value="custom">Custom range</option></select></label>
      @if (period === 'custom') { <label>From<input name="from" type="date" [(ngModel)]="from"></label><label>Through<input name="to" type="date" [(ngModel)]="to"></label> }
      <label>User ID<input name="user" [(ngModel)]="userId" placeholder="All users"></label>
      <label>Model<input name="model" [(ngModel)]="model" placeholder="All models"></label>
      <label>Feature<select name="feature" [(ngModel)]="feature"><option value="">All</option><option>MealAssistant</option></select></label>
      <label>Status<select name="status" [(ngModel)]="status"><option value="">All</option><option>Success</option><option>Failure</option><option>Cancelled</option></select></label>
      <label>Sort<select name="sort" [(ngModel)]="sort"><option value="time">Time</option><option value="cost">Estimated cost</option><option value="duration">Latency</option></select></label>
      <label>Direction<select name="direction" [(ngModel)]="descending"><option [ngValue]="true">Descending</option><option [ngValue]="false">Ascending</option></select></label>
      <button [disabled]="loading">Apply</button>
    </form>
    @if (error) { <p role="alert">{{ error }}</p> }
    @if (loading) { <p role="status">Loading AI usage…</p> }
    @if (overview; as summary) {
      <section class="kpis" aria-label="AI usage overview">
        <article><span>Total estimated spend (USD)</span><strong>{{ usd(summary.totals.estimatedCostUsd) }}</strong><small>{{ summary.totals.unpricedRequests }} requests without cost</small></article>
        <article><span>Provider requests</span><strong>{{ summary.totals.providerRequests }}</strong><small>{{ summary.totals.successfulRequests }} success · {{ summary.totals.failedRequests }} failure · {{ summary.totals.cancelledRequests }} cancelled</small></article>
        <article><span>Input / output tokens</span><strong>{{ summary.totals.inputTokens ?? 'Unknown' }} / {{ summary.totals.outputTokens ?? 'Unknown' }}</strong><small>{{ summary.totals.missingUsageRequests }} requests with incomplete usage</small></article>
        <article><span>Average estimated cost (priced requests)</span><strong>{{ usd(summary.totals.averageCostUsd) }}</strong></article>
        <article><span>Average latency</span><strong>{{ summary.totals.averageLatencyMs === null ? 'Unavailable' : (summary.totals.averageLatencyMs | number:'1.0-0') + ' ms' }}</strong></article>
        <article><span>Error rate</span><strong>{{ summary.totals.errorRate === null ? 'Unavailable' : (summary.totals.errorRate | percent:'1.0-1') }}</strong></article>
        <article><span>Application cache hit rate</span><strong>{{ summary.totals.cacheHitRate === null ? 'Not enabled' : (summary.totals.cacheHitRate | percent:'1.0-1') }}</strong><small>{{ summary.totals.cacheHits }} hits · {{ summary.totals.cacheMisses }} misses · {{ summary.totals.cacheBypasses }} bypasses</small></article>
        <article><span>Avoided provider requests</span><strong>{{ summary.totals.avoidedProviderRequests }}</strong><small>Savings unavailable without a baseline. Provider cached input is accounted for separately.</small></article>
      </section>
      <section class="analytics"><article><h2>Daily estimated cost · UTC days</h2>
        @for (day of summary.daily; track day.key) { <div class="bar-row"><span>{{ day.key }}</span><div class="bar-track"><span class="bar" [style.width.%]="width(day.estimatedCostUsd, costMaximum())"></span></div><strong>{{ usd(day.estimatedCostUsd) }}</strong></div> } @empty { <p>No usage in this period.</p> }
      </article><article><h2>Token usage · UTC days</h2><p>Input / output · reported tokens only</p>
        @for (day of summary.daily; track day.key) { <div class="bar-row"><span>{{ day.key }}</span><div class="bar-track"><span class="bar" [style.width.%]="width(day.inputTokens, tokenMaximum())"></span><span class="bar output" [style.width.%]="width(day.outputTokens, tokenMaximum())"></span></div><span>{{ day.inputTokens ?? '?' }} / {{ day.outputTokens ?? '?' }}</span></div> }
      </article></section>
      <section class="analytics">@for (group of [{ title: 'Requests by feature', rows: summary.features }, { title: 'Requests by model', rows: summary.models }]; track group.title) {
        <article><h2>{{ group.title }}</h2>@for (bucket of group.rows; track bucket.key) { <div class="bar-row"><span>{{ bucket.key }}</span><div class="bar-track"><span class="bar" [style.width.%]="width(bucket.requests, requestMaximum(group.rows))"></span></div><strong>{{ bucket.requests }} calls</strong></div><p>{{ bucket.inputTokens ?? '?' }} input / {{ bucket.outputTokens ?? '?' }} output · {{ usd(bucket.estimatedCostUsd) }} USD · {{ bucket.unpricedRequests }} unpriced</p> }</article>
      }</section>
    }
    <h2>Request history</h2><p>Request times use your browser timezone. Null usage and costs mean unavailable, not zero.</p>
    <div class="table-scroll"><table><thead><tr><th>Time</th><th>User</th><th>Feature / model</th><th>Status</th><th>Input / output / total</th><th>Estimated USD</th><th>Duration</th><th>Cache</th><th>Correlation ID</th><th>Details</th></tr></thead>
      <tbody>@for (request of requests; track request.id) { <tr><td>{{ request.startedAtUtc | date:'medium' }}</td><td>{{ request.userId ?? 'Unavailable' }}</td><td>{{ request.feature }} / {{ request.model }}</td><td>{{ request.status }}</td><td>{{ request.inputTokens ?? '?' }} / {{ request.outputTokens ?? '?' }} / {{ request.totalTokens ?? '?' }}</td><td>{{ usd(request.estimatedCostUsd) }}</td><td>{{ request.durationMs }} ms</td><td>{{ request.cacheStatus }}</td><td>{{ request.correlationId ?? '—' }}</td><td><button type="button" class="subtle" (click)="details(request.id)">Details</button></td></tr> } @empty { <tr><td colspan="10">No requests match these filters.</td></tr> }</tbody></table></div>
    <nav aria-label="AI request pages"><button type="button" [disabled]="loading || page <= 1" (click)="load(page - 1)">Previous</button><span>Page {{ page }} of {{ totalPages || 1 }} · {{ totalCount }} records</span><button type="button" [disabled]="loading || page >= totalPages" (click)="load(page + 1)">Next</button></nav>
    @if (selected; as request) { <section class="request-details" aria-label="AI request details"><h2>Request details</h2><button type="button" class="subtle" (click)="selected = null">Close details</button><p>Operational metadata only. Token categories and the applied pricing snapshot are retained below.</p><pre>{{ request | json }}</pre></section> }
    <details class="pricing"><summary>Model pricing · immutable versions</summary>
      <p>New versions apply from their effective date. The most recent effective version wins. Existing recorded costs never change. Enter verified rates in USD per million tokens.</p>
      <form class="filters" #priceForm="ngForm" (ngSubmit)="savePrice()">
        <label>Provider<input name="provider" [(ngModel)]="priceProvider" required maxlength="100"></label><label>Exact model<input name="priceModel" [(ngModel)]="priceModel" required maxlength="100"></label>
        <label>Input USD / million<input name="inputPrice" type="number" min="0" max="1000000" step="any" [(ngModel)]="inputPrice" required></label>
        <label>Cached input (optional)<input name="cachedPrice" type="number" min="0" max="1000000" step="any" [(ngModel)]="cachedPrice"></label>
        <label>Output USD / million<input name="outputPrice" type="number" min="0" max="1000000" step="any" [(ngModel)]="outputPrice" required></label>
        <label>Effective from (local time)<input name="effective" type="datetime-local" [(ngModel)]="effectiveFrom" required></label>
        <button [disabled]="priceForm.invalid || savingPrice">Add pricing version</button>
      </form>
      @if (pricingMessage) { <p role="status">{{ pricingMessage }}</p> }
      @for (price of prices; track price.id) { <p><strong>{{ price.provider }} / {{ price.model }}</strong> · {{ price.id }} · {{ priceState(price) }} · from {{ price.effectiveFromUtc | date:'medium' }} @if (price.effectiveToUtc) { until {{ price.effectiveToUtc | date:'medium' }} } · input {{ price.inputPricePerMillionTokens }} / cached {{ price.cachedInputPricePerMillionTokens ?? 'Unavailable' }} / output {{ price.outputPricePerMillionTokens }}</p> }
    </details>
  `,
  styles: [`:host { display:block; } header h1 { margin:.2rem 0; } header p { margin:.3rem 0; } h2 { font-size:1rem; }
    .filters { display:flex; flex-wrap:wrap; gap:.65rem; align-items:end; margin:1rem 0; } label { min-width:130px; flex:1; font-size:.82rem; }
    .kpis { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:.65rem; } article,.request-details,.pricing { border:1px solid var(--line); border-radius:8px; padding:.75rem; background:var(--cream); }
    .kpis article { display:grid; align-content:start; gap:.3rem; } .kpis strong { font-size:1.2rem; } small { color:var(--muted); }
    .analytics { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:.65rem; margin-top:.65rem; } .analytics article { min-width:0; max-height:320px; overflow:auto; }
    .bar-row { display:grid; grid-template-columns:auto minmax(40px,1fr) auto; gap:.5rem; font-size:.75rem; margin:.4rem 0; align-items:center; } .bar-track { display:grid; gap:2px; } .bar { display:block; height:7px; background:var(--coffee-dark); } .output { background:#b48461; }
    .table-scroll { overflow:auto; } table { width:100%; white-space:nowrap; border-collapse:collapse; font-size:.8rem; } th,td { padding:.55rem; border-bottom:1px solid var(--line); text-align:left; }
    nav { display:flex; gap:.65rem; justify-content:space-between; align-items:center; margin:1rem 0; } pre { white-space:pre-wrap; overflow-wrap:anywhere; } .pricing p { overflow-wrap:anywhere; font-size:.85rem; }
    @media(max-width:760px) { .analytics { grid-template-columns:1fr; } nav { flex-wrap:wrap; } }`]
})
export class AiMonitoringComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  overview: AiMonitoringOverview | null = null; requests: AiUsageRecord[] = []; prices: AiModelPricing[] = []; selected: AiUsageRecord | null = null;
  page = 1; totalPages = 0; totalCount = 0; loading = false; error = ''; period = '30'; from = ''; to = ''; userId = ''; model = ''; feature = ''; status = ''; sort = 'time'; descending = true;
  priceProvider = 'OpenAI'; priceModel = ''; inputPrice: number | null = null; cachedPrice: number | null = null; outputPrice: number | null = null; effectiveFrom = ''; savingPrice = false; pricingMessage = '';
  ngOnInit() { this.load(1); }
  filters(): AiUsageFilters {
    const end = new Date(); end.setHours(24, 0, 0, 0);
    const start = new Date(end); start.setDate(start.getDate() - (this.period === '7' ? 7 : this.period === '30' ? 30 : 1));
    if (this.period === 'month') start.setDate(1);
    if (this.period === 'custom') {
      const customStart = new Date(this.from + 'T00:00:00'); const customEnd = new Date(this.to + 'T00:00:00'); customEnd.setDate(customEnd.getDate() + 1);
      return { from: customStart.toISOString(), to: customEnd.toISOString(), userId: this.userId, model: this.model, feature: this.feature, status: this.status };
    }
    return { from: start.toISOString(), to: end.toISOString(), userId: this.userId, model: this.model, feature: this.feature, status: this.status };
  }
  load(page: number) {
    if (this.loading) return;
    let filters: AiUsageFilters;
    try { filters = this.filters(); } catch { this.error = 'Enter valid start and end dates.'; return; }
    this.loading = true; this.error = '';
    forkJoin({ overview: this.api.getAiOverview(filters), history: this.api.getAiRequests(filters, page, this.sort, this.descending), prices: this.api.getAiPricing() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)).subscribe({
        next: result => { this.overview = result.overview; this.requests = result.history.items; this.page = result.history.page; this.totalPages = result.history.totalPages; this.totalCount = result.history.totalCount; this.prices = result.prices; },
        error: error => this.error = getApiErrorMessage(error, 'Could not load AI monitoring. Try again.') });
  }
  details(id: string) { this.api.getAiRequest(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => this.selected = value, error: error => this.error = getApiErrorMessage(error, 'Could not load request details.') }); }
  usd(value: number | null) { return value === null ? 'Unavailable' : value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 10 }); }
  costMaximum() { return Math.max(...(this.overview?.daily.map(day => day.estimatedCostUsd ?? 0) ?? []), 0); }
  tokenMaximum() { return Math.max(...(this.overview?.daily.flatMap(day => [day.inputTokens ?? 0, day.outputTokens ?? 0]) ?? []), 0); }
  requestMaximum(rows: AiUsageBucket[]) { return Math.max(...rows.map(row => row.requests), 0); }
  width(value: number | null, maximum: number) { return value !== null && maximum > 0 ? value / maximum * 100 : 0; }
  priceState(price: AiModelPricing) {
    const now = Date.now();
    if (Date.parse(price.effectiveFromUtc) > now) return 'Scheduled';
    if (price.effectiveToUtc && Date.parse(price.effectiveToUtc) <= now) return 'Expired';
    return this.prices.some(other => other.provider === price.provider && other.model === price.model && Date.parse(other.effectiveFromUtc) > Date.parse(price.effectiveFromUtc)
      && Date.parse(other.effectiveFromUtc) <= now && (!other.effectiveToUtc || Date.parse(other.effectiveToUtc) > now)) ? 'Historical' : 'Active';
  }
  savePrice() {
    if (this.savingPrice || this.inputPrice === null || this.outputPrice === null || !this.effectiveFrom) return;
    this.savingPrice = true; this.pricingMessage = '';
    this.api.createAiPricing({ provider: this.priceProvider, model: this.priceModel, inputPricePerMillionTokens: this.inputPrice,
      cachedInputPricePerMillionTokens: this.cachedPrice, outputPricePerMillionTokens: this.outputPrice,
      effectiveFromUtc: new Date(this.effectiveFrom).toISOString(), effectiveToUtc: null })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.savingPrice = false)).subscribe({
        next: price => { this.prices = [price, ...this.prices]; this.pricingMessage = 'Pricing version added. Historical costs are unchanged.'; },
        error: error => this.pricingMessage = getApiErrorMessage(error, 'Could not save pricing version.') });
  }
}
