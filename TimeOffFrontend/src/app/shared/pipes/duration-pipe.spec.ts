import { DurationPipe } from './duration-pipe';

describe('DurationPipe', () => {
  const pipe = new DurationPipe();

  it('formats durations shorter than one hour', () => {
    expect(pipe.transform(45)).toBe('45m');
  });

  it('formats complete hours', () => {
    expect(pipe.transform(120)).toBe('2h');
  });

  it('formats hours and minutes', () => {
    expect(pipe.transform(630)).toBe('10h 30m');
  });

  it('does not display negative durations', () => {
    expect(pipe.transform(-30)).toBe('0m');
  });
});