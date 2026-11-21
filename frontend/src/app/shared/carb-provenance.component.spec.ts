import { TestBed } from '@angular/core/testing';
import { CarbProvenanceComponent } from './carb-provenance.component';

describe('CarbProvenanceComponent', () => {
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
