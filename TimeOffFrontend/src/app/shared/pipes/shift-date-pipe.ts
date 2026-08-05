import { Pipe, PipeTransform } from '@angular/core';

export type ShiftDateFormat = 'day' | 'date';

@Pipe({
  name: 'shiftDate',
})
export class ShiftDatePipe implements PipeTransform {
  transform(value: string, format: ShiftDateFormat): string {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);

    if (!match) {
      return 'N/A';
    }

    const year = Number(match[1]);
    const month = Number(match[2]);
    const day = Number(match[3]);
    const date = new Date(Date.UTC(year, month - 1, day));

    if (
      date.getUTCFullYear() !== year ||
      date.getUTCMonth() !== month - 1 ||
      date.getUTCDate() !== day
    ) {
      return 'N/A';
    }

    const options: Intl.DateTimeFormatOptions =
      format === 'day'
        ? { weekday: 'long', timeZone: 'UTC' }
        : { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' };

    return new Intl.DateTimeFormat('en-US', options).format(date);
  }
}
