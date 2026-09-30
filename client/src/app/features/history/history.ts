import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AnalysisService } from '../../core/services/analysis.service';
import { AnalysisHistoryItem } from '../../core/models/analysis.models';
import { SeverityBadge } from '../../shared/components/severity-badge/severity-badge';

@Component({
  selector: 'app-history',
  templateUrl: './history.html',
  styleUrl: './history.scss',
  imports: [DatePipe, RouterLink, SeverityBadge],
})
export class History {
  private readonly analysisService = inject(AnalysisService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<AnalysisHistoryItem[]>([]);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.analysisService.getRecent(50).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load analysis history.');
        this.loading.set(false);
      },
    });
  }

  remove(event: Event, id: string): void {
    event.stopPropagation();
    event.preventDefault();
    this.analysisService.delete(id).subscribe({
      next: () => this.items.update((list) => list.filter((i) => i.id !== id)),
    });
  }
}
