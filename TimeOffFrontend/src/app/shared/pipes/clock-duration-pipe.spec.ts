import { ClockDurationPipe } from './clock-duration-pipe';

describe('ClockDurationPipe', () => {
  const pipe = new ClockDurationPipe();

  it('formats seconds as a padded clock duration', () => {
    expect(pipe.transform(3_723)).toBe('01:02:03');
  });

  it('does not wrap hours after a full day', () => {
    expect(pipe.transform(90_061)).toBe('25:01:01');
  });

  it('clamps missing and negative durations to zero', () => {
    expect(pipe.transform(undefined)).toBe('00:00:00');
    expect(pipe.transform(-1)).toBe('00:00:00');
  });
});
