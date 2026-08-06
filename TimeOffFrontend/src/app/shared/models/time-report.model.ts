export interface TimeReportRange {
  startDate: string;
  endDate: string;
}

export interface DailyTimeReport {
  date: string;
  workedSeconds: number;
  breakSeconds: number;
}

export interface PersonalTimeReport {
  startDate: string;
  endDate: string;
  reportingTimezone: string;
  asOf: string;
  workedSeconds: number;
  breakSeconds: number;
  workSessionCount: number;
  daily: readonly DailyTimeReport[];
}

export interface TeamMemberTimeReport {
  userId: number;
  employeeId: number;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  workedSeconds: number;
  breakSeconds: number;
  workSessionCount: number;
}

export interface TeamTimeReport {
  startDate: string;
  endDate: string;
  reportingTimezone: string;
  asOf: string;
  includedMemberCount: number;
  excludedInactiveCount: number;
  totalWorkedSeconds: number;
  totalBreakSeconds: number;
  averageWorkedSeconds: number | null;
  members: readonly TeamMemberTimeReport[];
}
