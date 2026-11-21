import { Component, Input } from '@angular/core';
import { CarbProvenance } from '../core/models';

@Component({
  selector: 'app-carb-provenance',
  standalone: true,
  template: `<small>{{ label }}@if (overridden) { · Override entered by you }@if (provenance?.sourceMealVerifiedAt) { <span title="The source meal was verified. This does not verify the current meal or changed portion."> · source meal verified</span> }</small>`,
  styles: [':host { display:block; } small { color:var(--muted); font-size:.78rem; line-height:1.4; overflow-wrap:anywhere; }']
})
export class CarbProvenanceComponent {
  @Input() provenance?: CarbProvenance | null;
  @Input() overridden = false;
  get label() {
    const source = this.provenance?.referenceSource ?? 'Unknown';
    const origin = { Unknown: 'Source unknown', Manual: 'Entered by you', AiEstimate: 'AI estimate', Mixed: 'Mixed sources' }[source];
    const operation = this.provenance?.transformation;
    return operation?.includes('HistoricalReuse') ? `From previous record · ${origin.toLowerCase()}`
      : operation?.includes('Calculated') ? `Calculated from saved food · ${origin.toLowerCase()}` : origin;
  }
}
