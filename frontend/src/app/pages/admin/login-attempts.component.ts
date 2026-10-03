import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { LoginAttempt, LoginAttemptStats } from '../../core/models';

@Component({
  selector: 'app-login-attempts',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <section class="page-shell admin-audit">
      <header class="page-heading">
        <div><p class="eyebrow">Administration</p><h1>Login history</h1><p>Review successful and failed authentication attempts.</p></div>
      </header>

      @if (stats(); as summary) {
        <div class="audit-stats" aria-label="Login attempt statistics">
          <article><strong>{{ summary.totalAttempts }}</strong><span>Total</span></article>
          <article><strong>{{ summary.successfulAttempts }}</strong><span>Successful</span></article>
          <article><strong>{{ summary.failedAttempts }}</strong><span>Failed</span></article>
          <article><strong>{{ summary.uniqueUsernames }}</strong><span>Usernames</span></article>
        </div>
      }

      <form class="audit-filters" (ngSubmit)="applyFilters()">
        <label>Username<input name="username" [(ngModel)]="username" placeholder="Search username" /></label>
        <label>Result<select name="success" [(ngModel)]="success"><option value="">All</option><option value="true">Successful</option><option value="false">Failed</option></select></label>
        <label>From<input name="from" type="datetime-local" [(ngModel)]="from" /></label>
        <label>To<input name="to" type="datetime-local" [(ngModel)]="to" /></label>
        <button type="submit">Apply filters</button>
      </form>

      @if (loading()) { <p class="state-card">Loading login history…</p> }
      @else if (error()) { <p class="state-card error">{{ error() }}</p> }
      @else if (!attempts().length) { <p class="state-card">No login attempts match these filters.</p> }
      @else {
        <div class="table-scroll"><table>
          <thead><tr><th>Time</th><th>Username</th><th>Result</th><th>Reason</th><th>IP address</th><th>User agent</th></tr></thead>
          <tbody>@for (attempt of attempts(); track attempt.id) {
            <tr><td>{{ attempt.attemptedAtUtc | date:'medium' }}</td><td>{{ attempt.username }}</td>
              <td><span class="status-badge" [class.success]="attempt.success">{{ attempt.success ? 'Success' : 'Failed' }}</span></td>
              <td>{{ attempt.failureReason || '—' }}</td><td>{{ attempt.ipAddress || '—' }}</td><td class="user-agent" [title]="attempt.userAgent || ''">{{ attempt.userAgent || '—' }}</td></tr>
          }</tbody>
        </table></div>
        <nav class="pagination" aria-label="Login history pages">
          <button type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">Previous</button>
          <span>Page {{ page() }} of {{ totalPages() || 1 }} · {{ totalCount() }} attempts</span>
          <button type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">Next</button>
        </nav>
      }
    </section>
  `,
  styles: [`
    .admin-audit { max-width: 1200px; margin: 0 auto; }
    .eyebrow { color: var(--accent, #26766c); font-weight: 700; text-transform: uppercase; letter-spacing: .08em; }
    .audit-stats { display:grid; grid-template-columns:repeat(4,1fr); gap:1rem; margin:1.5rem 0; }
    .audit-stats article,.audit-filters,.table-scroll,.state-card { background:#fffaf5; border:1px solid #ead9ca; border-radius:12px; }
    .audit-stats article { padding:1rem; display:flex; flex-direction:column; } .audit-stats strong { font-size:1.7rem; } .audit-stats span { color:#765f55; }
    .audit-filters { display:grid; grid-template-columns:2fr 1fr 1.5fr 1.5fr auto; gap:.75rem; align-items:end; padding:1rem; margin-bottom:1rem; }
    label { display:grid; gap:.35rem; font-weight:600; } input,select,button { min-height:42px; border-radius:8px; border:1px solid #d8c2b3; padding:.55rem .7rem; }
    button { cursor:pointer; font-weight:700; } button:disabled { cursor:not-allowed; opacity:.5; }
    .table-scroll { overflow-x:auto; } table { width:100%; border-collapse:collapse; } th,td { padding:.85rem; text-align:left; border-bottom:1px solid #ead9ca; white-space:nowrap; }
    .user-agent { max-width:260px; overflow:hidden; text-overflow:ellipsis; } .status-badge { padding:.25rem .55rem; border-radius:999px; background:#f8d7da; color:#842029; } .status-badge.success { background:#d1e7dd; color:#0f5132; }
    .pagination { display:flex; justify-content:space-between; align-items:center; margin-top:1rem; } .error { color:#842029; }
    @media (max-width: 850px) { .audit-stats { grid-template-columns:repeat(2,1fr); } .audit-filters { grid-template-columns:1fr 1fr; } }
  `]
})
export class LoginAttemptsComponent implements OnInit {
  readonly attempts = signal<LoginAttempt[]>([]);
  readonly stats = signal<LoginAttemptStats | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly page = signal(1);
  readonly totalPages = signal(0);
  readonly totalCount = signal(0);
  readonly pageSize = 25;
  username = '';
  success = '';
  from = '';
  to = '';

  constructor(private readonly api: ApiService) {}
  ngOnInit(): void { this.load(1); }
  applyFilters(): void { this.load(1); }
  load(page: number): void {
    this.loading.set(true); this.error.set('');
    this.api.getLoginAttempts(page, this.pageSize, this.username, this.success, this.from, this.to).subscribe({
      next: result => { this.attempts.set(result.items); this.page.set(result.page); this.totalPages.set(result.totalPages); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: () => { this.error.set('Could not load login history.'); this.loading.set(false); }
    });
    this.api.getLoginAttemptStats(this.username, this.success, this.from, this.to).subscribe({ next: value => this.stats.set(value) });
  }
}
