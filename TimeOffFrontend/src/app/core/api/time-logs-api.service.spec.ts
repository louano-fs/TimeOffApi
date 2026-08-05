import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import {
  PagedResponse,
  WorkSessionResponse,
} from '../../shared/models/time-log.model';
import { TimeLogsApiService } from './time-logs-api.service';

describe('TimeLogsApiService', () => {
  let service: TimeLogsApiService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(TimeLogsApiService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('gets a page of the current employee time logs', () => {
    const expected: PagedResponse<WorkSessionResponse> = {
      items: [
        {
          id: 101,
          userId: 1,
          employeeId: 1001,
          shiftDate: '2026-08-05',
          start: '2026-08-05T00:00:00Z',
          end: '2026-08-05T09:00:00Z',
          status: 'Completed',
          timezone: 'Asia/Manila',
          totalElapsedMinutes: 540,
          totalBreakMinutes: 60,
          totalWorkedMinutes: 480,
          breaks: [
            {
              id: 102,
              start: '2026-08-05T04:00:00Z',
              end: '2026-08-05T05:00:00Z',
              durationMinutes: 60,
            },
          ],
        },
      ],
      page: 2,
      pageSize: 20,
      totalCount: 25,
      totalPages: 2,
    };

    let result:
      | PagedResponse<WorkSessionResponse>
      | undefined;

    service
      .getMyTimeLogs(2, 20)
      .subscribe((response) => {
        result = response;
      });

    const request = httpTesting.expectOne((candidate) => {
      return candidate.url === '/api/time-logs';
    });

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');

    request.flush(expected);

    expect(result).toEqual(expected);
  });
});