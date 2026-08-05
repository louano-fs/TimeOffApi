import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
    ClockActionRequest,
    ClockStatusResponse,
    TimeLogResponse,
} from '../../shared/models/clock.model';

@Injectable({
    providedIn: 'root',
})
export class TimeClockApiService {
    private readonly http = inject(HttpClient);

    getStatus(): Observable<ClockStatusResponse> {
        return this.http.get<ClockStatusResponse>(
            '/api/time-clock/status'
        );
    }

    clockIn(dateTime: string): Observable<TimeLogResponse> {
        return this.performAction(
            '/api/time-clock/clock-in',
            dateTime
        );
    }

    startBreak(dateTime: string): Observable<TimeLogResponse> {
        return this.performAction(
            '/api/time-clock/break/start',
            dateTime,
        );
    }

    endBreak(dateTime: string): Observable<TimeLogResponse> {
        return this.performAction(
            '/api/time-clock/break/end',
            dateTime,
        );
    }

    clockOut(dateTime: string): Observable<TimeLogResponse> {
        return this.performAction(
            '/api/time-clock/clock-out',
            dateTime,
        );
    }

    private performAction(
        url: string,
        dateTime: string,
    ): Observable<TimeLogResponse> {
        const request: ClockActionRequest = { dateTime };
        return this.http.post<TimeLogResponse>(url, request);
    }
}