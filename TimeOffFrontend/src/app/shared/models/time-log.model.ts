export type WorkSessionStatus =
  | 'Active'
  | 'Completed';

export interface BreakResponse {
  id: number;
  start: string;
  end?: string;
  durationMinutes: number;
}

export interface WorkSessionResponse {
  id: number;
  userId: number;
  employeeId: number;
  shiftDate: string;
  start: string;
  end?: string;
  status: WorkSessionStatus;
  timezone: string;
  totalElapsedMinutes: number;
  totalBreakMinutes: number;
  totalWorkedMinutes: number;
  breaks: BreakResponse[];
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}