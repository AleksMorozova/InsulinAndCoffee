import { Component } from '@angular/core';

@Component({
  selector: 'app-disclaimer',
  standalone: true,
  template: `
    <aside class="notice">
      <strong>Informational only.</strong>
      This app is not a medical device. Suggested insulin calculations must be reviewed and confirmed by the user.
    </aside>
  `
})
export class DisclaimerComponent {}
