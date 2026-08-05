export type ClockStatus =
  | 'ClockedOut'
  | 'Working'
  | 'OnBreak';

export type ClockAction =
  | 'clockIn'
  | 'startBreak'
  | 'endBreak'
  | 'clockOut';

export type TimeLogType = 'Work' | 'Break';

export type TimeLogStatus =
  | 'Working'
  | 'OnBreak'
  | 'Completed';

export interface ClockActionRequest {
  dateTime: string;
}

export interface TimeLogResponse {
  id: number;
  type: TimeLogType;
  status: TimeLogStatus;
  shiftDate: string;
  start: string;
  end?: string;
  timezone: string;
  workedMinutes: number;
}

export interface ClockStatusResponse {
  status: ClockStatus;
  activeWorkLogId?: number;
  activeBreakLogId?: number;
  clockedInAt?: string;
  breakStartedAt?: string;
  workedMinutesToday: number;
  breakMinutesToday: number;
}