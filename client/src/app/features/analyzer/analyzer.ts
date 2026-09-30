import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AnalysisService } from '../../core/services/analysis.service';
import { AnalysisResult } from '../../core/models/analysis.models';
import { AnalysisReport } from '../../shared/components/analysis-report/analysis-report';
import { SAMPLE_MIGRATIONS, SampleMigration } from '../../shared/data/sample-migrations';

@Component({
  selector: 'app-analyzer',
  templateUrl: './analyzer.html',
  styleUrl: './analyzer.scss',
  imports: [ReactiveFormsModule, AnalysisReport],
})
export class Analyzer {
  private readonly fb = inject(FormBuilder);
  private readonly analysisService = inject(AnalysisService);

  readonly samples = SAMPLE_MIGRATIONS;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<AnalysisResult | null>(null);

  readonly form = this.fb.nonNullable.group({
    scriptName: ['migration.sql'],
    script: ['', [Validators.required]],
  });

  loadSample(sample: SampleMigration): void {
    this.form.patchValue({ script: sample.script });
    this.result.set(null);
    this.error.set(null);
  }

  clear(): void {
    this.form.patchValue({ script: '' });
    this.result.set(null);
    this.error.set(null);
  }

  analyze(): void {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    const { script, scriptName } = this.form.getRawValue();
    this.analysisService.analyze({ script, scriptName }).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(this.toMessage(err));
        this.loading.set(false);
      },
    });
  }

  private toMessage(err: HttpErrorResponse): string {
    if (err.status === 0) {
      return 'Could not reach the SchemaSentinel API. Make sure the server is running.';
    }
    if (err.status === 400 && err.error?.detail) {
      return err.error.detail;
    }
    return 'The analysis failed. Please try again.';
  }
}
