import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { PagedResponse, WorkSessionResponse } from '../../shared/models/time-log.model';
import { PersonalTimeReport, TimeReportRange } from '../../shared/models/time-report.model';
import { DownloadResponse, mapDownloadResponse } from './download-response';

@Injectable({
  providedIn: 'root',
})
export class TimeLogsApiService {
  private readonly http = inject(HttpClient);

  getMyTimeLogs(
    page = 1,
    pageSize = 20,
  ): Observable<PagedResponse<WorkSessionResponse>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedResponse<WorkSessionResponse>>(
      '/api/time-logs',
      { params },
    );
  }

  getReport(range?: TimeReportRange): Observable<PersonalTimeReport> {
    return this.http.get<PersonalTimeReport>('/api/time-logs/report', {
      params: this.rangeParams(range),
    });
  }

  downloadExport(range: TimeReportRange): Observable<DownloadResponse> {
    const params = this.rangeParams(range).set('format', 'xlsx');
    return this.http
      .get('/api/time-logs/export', {
        params,
        observe: 'response',
        responseType: 'blob',
      })
      .pipe(map((response) => mapDownloadResponse(response, 'my-time-logs.xlsx')));
  }

  private rangeParams(range?: TimeReportRange): HttpParams {
    if (!range) {
      return new HttpParams();
    }

    return new HttpParams().set('startDate', range.startDate).set('endDate', range.endDate);
  }
}
