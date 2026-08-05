import {
  HttpClient,
  HttpParams,
} from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  PagedResponse,
  WorkSessionResponse,
} from '../../shared/models/time-log.model';

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
}