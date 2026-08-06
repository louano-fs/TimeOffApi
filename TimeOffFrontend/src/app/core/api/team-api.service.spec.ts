import { provideHttpClient } from '@angular/common/http';
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
});
