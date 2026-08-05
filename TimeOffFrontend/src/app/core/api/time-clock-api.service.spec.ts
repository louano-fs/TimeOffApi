import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';

import {
  ClockStatusResponse,
  TimeLogResponse,
} from '../../shared/models/clock.model';
import { TimeClockApiService } from './time-clock-api.service';

describe('TimeClockApiService', () => {
  let service: TimeClockApiService;
  let httpTesting: HttpTestingController;

  const timestamp = '2026-08-05T01:00:00.000Z';

  const actionResponse: TimeLogResponse = {
    id: 101,
    type: 'Work',
    status: 'Working',
    shiftDate: '2026-08-05',
    start: timestamp,
    timezone: 'Asia/Manila',
    workedMinutes: 0,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(TimeClockApiService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('gets the current clock status', () => {
    const expected: ClockStatusResponse = {
      status: 'ClockedOut',
      workedMinutesToday: 480,
      breakMinutesToday: 60,
    };

    let result: ClockStatusResponse | undefined;

    service.getStatus().subscribe((response) => {
      result = response;
    });

    const request = httpTesting.expectOne(
      '/api/time-clock/status',
    );

    expect(request.request.method).toBe('GET');

    request.flush(expected);

    expect(result).toEqual(expected);
  });

  it('posts a clock-in action', () => {
    expectActionRequest(
      service.clockIn(timestamp),
      '/api/time-clock/clock-in',
    );
  });

  it('posts a start-break action', () => {
    expectActionRequest(
      service.startBreak(timestamp),
      '/api/time-clock/break/start',
    );
  });

  it('posts an end-break action', () => {
    expectActionRequest(
      service.endBreak(timestamp),
      '/api/time-clock/break/end',
    );
  });

  it('posts a clock-out action', () => {
    expectActionRequest(
      service.clockOut(timestamp),
      '/api/time-clock/clock-out',
    );
  });

  function expectActionRequest(
    action: Observable<TimeLogResponse>,
    expectedUrl: string,
  ): void {
    let result: TimeLogResponse | undefined;

    action.subscribe((response) => {
      result = response;
    });

    const request = httpTesting.expectOne(expectedUrl);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      dateTime: timestamp,
    });

    request.flush(actionResponse);

    expect(result).toEqual(actionResponse);
  }
});