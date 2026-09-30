import { Component, inject, input, signal, effect } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AnalysisService } from '../../core/services/analysis.service';
import { AnalysisResult } from '../../core/models/analysis.models';
import { AnalysisReport } from '../../shared/components/analysis-report/analysis-report';

@Component({
  selector: 'app-history-detail',
  templateUrl: './history-detail.html',
  styleUrl: './history-detail.scss',
  imports: [RouterLink, AnalysisReport],
})
export class HistoryDetail {
  private readonly analysisService = inject(AnalysisService);

  /** Bound from the route parameter via withComponentInputBinding. */
  readonly id = input.required<string>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly result = signal<AnalysisResult | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      this.loading.set(true);
      this.error.set(null);
      this.analysisService.getById(id).subscribe({
        next: (result) => {
          this.result.set(result);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('This analysis could not be found.');
          this.loading.set(false);
        },
      });
    });
  }
}
