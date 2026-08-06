import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResponse, WorkSessionResponse } from '../../shared/models/time-log.model';
import { TeamMember } from '../../shared/models/team.model';

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
}
