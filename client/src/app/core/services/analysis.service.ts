import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AnalysisHistoryItem,
  AnalysisResult,
  AnalyzeRequest,
} from '../models/analysis.models';

/** Single point of HTTP communication with the SchemaSentinel API. */
@Injectable({ providedIn: 'root' })
export class AnalysisService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/analysis`;

  analyze(request: AnalyzeRequest): Observable<AnalysisResult> {
    return this.http.post<AnalysisResult>(this.baseUrl, request);
  }

  getRecent(take = 25): Observable<AnalysisHistoryItem[]> {
    return this.http.get<AnalysisHistoryItem[]>(`${this.baseUrl}?take=${take}`);
  }

  getById(id: string): Observable<AnalysisResult> {
    return this.http.get<AnalysisResult>(`${this.baseUrl}/${id}`);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
