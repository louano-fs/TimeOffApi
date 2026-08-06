import { SecondsDurationPipe } from './seconds-duration-pipe';

describe('SecondsDurationPipe', () => {
  const pipe = new SecondsDurationPipe();

  it('formats exact hour minute and second values', () => {
    expect(pipe.transform(29_125)).toBe('8h 5m 25s');
  });

  it('formats zero and clamps negative values', () => {
    expect(pipe.transform(0)).toBe('0s');
    expect(pipe.transform(-60)).toBe('0s');
  });
});
