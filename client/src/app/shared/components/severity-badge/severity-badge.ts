import { Component, input } from '@angular/core';
import { UpperCasePipe } from '@angular/common';
import { RiskSeverity } from '../../../core/models/analysis.models';

@Component({
  selector: 'app-severity-badge',
  template: `
    <span class="badge" [class]="'sev-' + severity().toLowerCase()">
      <span class="dot"></span>{{ severity() | uppercase }}
    </span>
  `,
  styleUrl: './severity-badge.scss',
  imports: [UpperCasePipe],
})
export class SeverityBadge {
  readonly severity = input.required<RiskSeverity>();
}
