import { ShiftDatePipe } from './shift-date-pipe';

describe('ShiftDatePipe', () => {
  const pipe = new ShiftDatePipe();

  it('formats the day without shifting the calendar date', () => {
    expect(pipe.transform('2026-08-05', 'day')).toBe('Wednesday');
  });

  it('formats the local shift date without a timezone conversion', () => {
    expect(pipe.transform('2026-08-05', 'date')).toBe('Aug 5, 2026');
  });

  it('returns a placeholder for an invalid date', () => {
    expect(pipe.transform('2026-02-30', 'date')).toBe('N/A');
  });
});
