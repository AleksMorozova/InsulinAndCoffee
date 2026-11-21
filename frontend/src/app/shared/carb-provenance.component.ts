import { Component, Input } from '@angular/core';
import { CarbProvenance } from '../core/models';

@Component({
  selector: 'app-carb-provenance',
  standalone: true,
  host: { '[class.compact]': 'compact' },
  template: `@if (compact) { <small class="compact-source" tabindex="0" [title]="description" [attr.aria-label]="description">{{ shortLabel }}</small> }
    @else { <small>{{ label }}@if (overridden) { · Override entered by you }@if (provenance?.sourceMealVerifiedAt) { <span title="The source meal was verified. This does not verify the current meal or changed portion."> · source meal verified</span> }</small> }`,
  styles: [':host { display:block; } small { color:var(--muted); font-size:.78rem; line-height:1.4; overflow-wrap:anywhere; } :host(.compact) { min-width:0; max-width:100%; } .compact-source { display:block; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; overflow-wrap:normal; font-size:.74rem; }']
})
export class CarbProvenanceComponent {
  @Input() provenance?: CarbProvenance | null;
  @Input() overridden = false;
  @Input() compact = false;
  get shortLabel() {
    if (this.provenance?.transformation.includes('HistoricalReuse')) return 'From history';
    return { Unknown: 'Source unknown', Manual: 'Your value', AiEstimate: 'AI estimate', Mixed: 'Mixed sources' }[this.provenance?.referenceSource ?? 'Unknown'];
  }
  get description() {
    return this.label + (this.overridden ? ' · Override entered by you' : '')
      + (this.provenance?.sourceMealVerifiedAt ? ' · Source meal verified; this does not verify the current meal or changed portion.' : '');
  }
  get label() {
    const source = this.provenance?.referenceSource ?? 'Unknown';
    const origin = { Unknown: 'Source unknown', Manual: 'Entered by you', AiEstimate: 'AI estimate', Mixed: 'Mixed sources' }[source];
    const operation = this.provenance?.transformation;
    return operation?.includes('HistoricalReuse') ? `From previous record · ${origin.toLowerCase()}`
      : operation?.includes('Calculated') ? `Calculated from saved food · ${origin.toLowerCase()}` : origin;
  }
}
