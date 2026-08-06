import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { PagedResponse, WorkSessionResponse } from '../../shared/models/time-log.model';
import { TeamMember } from '../../shared/models/team.model';
import { TeamTimeReport, TimeReportRange } from '../../shared/models/time-report.model';
import { DownloadResponse, mapDownloadResponse } from './download-response';

@Injectable({
  providedIn: 'root',
})
export class TeamApiService {
  private readonly http = inject(HttpClient);

  getMembers(): Observable<readonly TeamMember[]> {
    return this.http.get<readonly TeamMember[]>('/api/team');
  }

  getTimeLogs(
    userId: number,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResponse<WorkSessionResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);

    return this.http.get<PagedResponse<WorkSessionResponse>>(`/api/team/${userId}/time-logs`, {
      params,
    });
  }

  getReport(
    range?: TimeReportRange,
    includeInactive = false,
  ): Observable<TeamTimeReport> {
    return this.http.get<TeamTimeReport>('/api/team/report', {
      params: this.reportParams(range, includeInactive),
    });
  }

  downloadExport(
    range: TimeReportRange,
    includeInactive = false,
  ): Observable<DownloadResponse> {
    const params = this.reportParams(range, includeInactive).set('format', 'xlsx');
    return this.http
      .get('/api/team/time-logs/export', {
        params,
        observe: 'response',
        responseType: 'blob',
      })
      .pipe(map((response) => mapDownloadResponse(response, 'team-time-logs.xlsx')));
  }

  private reportParams(range?: TimeReportRange, includeInactive = false): HttpParams {
    let params = new HttpParams().set('includeInactive', includeInactive);
    if (range) {
      params = params.set('startDate', range.startDate).set('endDate', range.endDate);
    }
    return params;
  }
}
