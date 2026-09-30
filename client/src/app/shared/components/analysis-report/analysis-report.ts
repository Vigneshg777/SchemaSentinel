import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AnalysisResult, RiskFinding } from '../../../core/models/analysis.models';
import { SeverityBadge } from '../severity-badge/severity-badge';

@Component({
  selector: 'app-analysis-report',
  templateUrl: './analysis-report.html',
  styleUrl: './analysis-report.scss',
  imports: [SeverityBadge, DatePipe],
})
export class AnalysisReport {
  readonly result = input.required<AnalysisResult>();

  sourceLabel(finding: RiskFinding): string {
    return finding.source === 'DatabaseAnalysis'
      ? 'Static + Database Analysis'
      : 'Static Analysis';
  }
}
