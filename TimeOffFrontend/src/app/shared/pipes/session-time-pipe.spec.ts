import { SessionTimePipe } from './session-time-pipe';

describe('SessionTimePipe', () => {
  const pipe = new SessionTimePipe();

  it('formats UTC time in the session timezone', () => {
    const result = pipe.transform('2026-08-05T00:00:00Z', 'Asia/Manila');

    expect(result).toBe('8:00 AM');
  });

  it('returns a placeholder when time is missing', () => {
    expect(pipe.transform(undefined, 'Asia/Manila')).toBe('N/A');
  });

  it('returns a placeholder for an invalid timezone', () => {
    expect(pipe.transform('2026-08-05T00:00:00Z', 'Invalid/Timezone')).toBe('N/A');
  });
});
