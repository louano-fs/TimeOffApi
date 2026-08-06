import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { TeamApiService } from './team-api.service';

describe('TeamApiService', () => {
  let service: TeamApiService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TeamApiService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('gets assigned employees', () => {
    service.getMembers().subscribe();

    const request = httpTesting.expectOne('/api/team');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('gets a page of an assigned employee time logs', () => {
    service.getTimeLogs(42, 2, 10).subscribe();

    const request = httpTesting.expectOne(
      (candidate) => candidate.url === '/api/team/42/time-logs',
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0 });
  });

  it('gets a manager team report with explicit scope options', () => {
    service
      .getReport({ startDate: '2026-08-03', endDate: '2026-08-06' }, true)
      .subscribe();

    const request = httpTesting.expectOne((candidate) => candidate.url === '/api/team/report');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('startDate')).toBe('2026-08-03');
    expect(request.request.params.get('endDate')).toBe('2026-08-06');
    expect(request.request.params.get('includeInactive')).toBe('true');
    request.flush({});
  });

  it('downloads a team workbook using the server filename', () => {
    let fileName = '';
    service
      .downloadExport({ startDate: '2026-08-03', endDate: '2026-08-06' })
      .subscribe((download) => (fileName = download.fileName));

    const request = httpTesting.expectOne(
      (candidate) => candidate.url === '/api/team/time-logs/export',
    );
    expect(request.request.responseType).toBe('blob');
    expect(request.request.params.get('includeInactive')).toBe('false');
    expect(request.request.params.get('format')).toBe('xlsx');
    request.flush(new Blob(['xlsx']), {
      headers: new HttpHeaders({
        'Content-Disposition': "attachment; filename*=UTF-8''team-time-logs-2026-08-03.xlsx",
      }),
    });

    expect(fileName).toBe('team-time-logs-2026-08-03.xlsx');
  });
});
