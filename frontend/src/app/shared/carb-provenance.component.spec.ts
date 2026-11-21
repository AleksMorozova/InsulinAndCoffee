import { TestBed } from '@angular/core/testing';
import { CarbProvenanceComponent } from './carb-provenance.component';

describe('CarbProvenanceComponent', () => {
  it('keeps history provenance short and moves full details into an accessible tooltip', () => {
    const fixture = TestBed.createComponent(CarbProvenanceComponent);
    fixture.componentInstance.compact = true;
    fixture.componentInstance.overridden = true;
    fixture.componentInstance.provenance = { referenceSource: 'Manual', transformation: 'Calculated, HistoricalReuse', sourceMealVerifiedAt: '2026-11-21T10:00:00Z' };
    fixture.detectChanges();
    const indicator: HTMLElement | null = fixture.nativeElement.querySelector('.compact-source');
    expect(indicator?.textContent?.trim()).toBe('From history');
    expect(indicator?.title).toContain('Override entered by you');
    expect(indicator?.getAttribute('aria-label')).toContain('does not verify the current meal');
    if (indicator) expect(getComputedStyle(indicator).whiteSpace).toBe('nowrap');
  });
  it('keeps reference source, transformation, override and source verification distinct', () => {
    const fixture = TestBed.createComponent(CarbProvenanceComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Source unknown');
    fixture.componentInstance.provenance = { referenceSource: 'Manual', transformation: 'Calculated' };
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Calculated from saved food · entered by you');
    fixture.componentInstance.provenance = { referenceSource: 'AiEstimate', transformation: 'Calculated, HistoricalReuse', sourceMealVerifiedAt: '2026-11-21T10:00:00Z' };
    fixture.componentInstance.overridden = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('From previous record · ai estimate');
    expect(fixture.nativeElement.textContent).toContain('Override entered by you');
    expect(fixture.nativeElement.textContent).toContain('source meal verified');
    expect(fixture.nativeElement.textContent).not.toContain('Verified by you');
    expect(fixture.nativeElement.querySelector('span').title).toContain('does not verify the current meal');
  });
});
