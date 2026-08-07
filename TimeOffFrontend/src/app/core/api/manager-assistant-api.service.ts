import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import {
  ManagerAssistantCapabilities,
  ManagerAssistantMessageRequest,
  ManagerAssistantMessageResponse,
} from '../../shared/models/manager-assistant.model';
import { DownloadResponse, mapDownloadResponse } from './download-response';

@Injectable({ providedIn: 'root' })
export class ManagerAssistantApiService {
  private readonly http = inject(HttpClient);

  getCapabilities(): Observable<ManagerAssistantCapabilities> {
    return this.http.get<ManagerAssistantCapabilities>('/api/manager-assistant/capabilities');
  }

  sendMessage(request: ManagerAssistantMessageRequest): Observable<ManagerAssistantMessageResponse> {
    return this.http.post<ManagerAssistantMessageResponse>(
      '/api/manager-assistant/messages',
      request,
    );
  }

  downloadExport(downloadUrl: string, fileName: string): Observable<DownloadResponse> {
    return this.http
      .get(downloadUrl, { observe: 'response', responseType: 'blob' })
      .pipe(map((response) => mapDownloadResponse(response, fileName)));
  }
}
