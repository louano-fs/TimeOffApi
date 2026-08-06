using System.Globalization;
using ClosedXML.Excel;
using TimeOffApi.Contracts;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

internal interface ITimeLogWorkbookWriter
{
    TimeLogExportFile Write(TimeLogExportData data);
}

internal sealed class TimeLogWorkbookWriter : ITimeLogWorkbookWriter
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public TimeLogExportFile Write(TimeLogExportData data)
    {
        using var workbook = new XLWorkbook();
        WriteSummary(workbook, data);
        if (data.ReportType == "Team")
            WriteEmployees(workbook, data);
        WriteWorkSessions(workbook, data);
        WriteBreaks(workbook, data);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var prefix = data.ReportType == "Team" ? "team-time-logs" : "my-time-logs";
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}-{data.StartDate:yyyy-MM-dd}-to-{data.EndDate:yyyy-MM-dd}.xlsx");
        return new(stream.ToArray(), ExcelContentType, fileName);
    }

    private static void WriteSummary(XLWorkbook workbook, TimeLogExportData data)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        WriteHeader(sheet, ["Field", "Value"]);

        var asOfLocal = DateTimeHelper.LocalDateTime(data.AsOf, data.ReportingTimezone);
        var values = new (string Label, object Value)[]
        {
            ("Report Type", data.ReportType),
            ("Prepared For", data.PreparedFor),
            ("Employee Number", data.PreparedForEmployeeNumber ?? string.Empty),
            ("Reporting Timezone", data.ReportingTimezone),
            ("Start Date", data.StartDate.ToDateTime(TimeOnly.MinValue)),
            ("End Date", data.EndDate.ToDateTime(TimeOnly.MinValue)),
            ("As Of", asOfLocal),
            ("Included Member Count", data.Members.Count),
            ("Excluded Inactive Count", data.ExcludedInactiveCount),
            ("Work Session Count", data.WorkSessions.Count),
            ("Break Count", data.Breaks.Count),
            ("Total Elapsed Seconds", data.WorkSessions.Sum(x => (long)x.ElapsedSeconds)),
            ("Total Break Seconds", data.WorkSessions.Sum(x => (long)x.BreakSeconds)),
            ("Total Worked Seconds", data.WorkSessions.Sum(x => (long)x.WorkedSeconds))
        };

        for (var index = 0; index < values.Length; index++)
        {
            var row = index + 2;
            SetLiteralText(sheet.Cell(row, 1), values[index].Label);
            SetCellValue(sheet.Cell(row, 2), values[index].Value);
        }

        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 36;
        sheet.Range(6, 2, 8, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
    }

    private static void WriteEmployees(XLWorkbook workbook, TimeLogExportData data)
    {
        var sheet = workbook.Worksheets.Add("Employees");
        WriteHeader(sheet,
        [
            "Employee ID",
            "Employee Number",
            "First Name",
            "Last Name",
            "Active",
            "Work Session Count",
            "Elapsed Seconds",
            "Break Seconds",
            "Worked Seconds"
        ]);

        var sessionsByUser = data.WorkSessions
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        for (var index = 0; index < data.Members.Count; index++)
        {
            var row = index + 2;
            var member = data.Members[index];
            var sessions = sessionsByUser.GetValueOrDefault(member.UserId) ?? [];
            sheet.Cell(row, 1).SetValue(member.EmployeeId);
            SetLiteralText(sheet.Cell(row, 2), member.EmployeeNumber);
            SetLiteralText(sheet.Cell(row, 3), member.FirstName);
            SetLiteralText(sheet.Cell(row, 4), member.LastName);
            sheet.Cell(row, 5).SetValue(member.IsActive);
            sheet.Cell(row, 6).SetValue(sessions.Length);
            sheet.Cell(row, 7).SetValue(sessions.Sum(x => (long)x.ElapsedSeconds));
            sheet.Cell(row, 8).SetValue(sessions.Sum(x => (long)x.BreakSeconds));
            sheet.Cell(row, 9).SetValue(sessions.Sum(x => (long)x.WorkedSeconds));
        }

        FinishTabularSheet(sheet, 9);
    }

    private static void WriteWorkSessions(XLWorkbook workbook, TimeLogExportData data)
    {
        var sheet = workbook.Worksheets.Add("Work Sessions");
        WriteHeader(sheet,
        [
            "Source Session ID",
            "Employee Number",
            "Employee Name",
            "Shift Date",
            "Start",
            "End",
            "Status",
            "Stored Timezone",
            "Elapsed Seconds",
            "Break Seconds",
            "Worked Seconds"
        ]);

        for (var index = 0; index < data.WorkSessions.Count; index++)
        {
            var row = index + 2;
            var session = data.WorkSessions[index];
            sheet.Cell(row, 1).SetValue(session.SourceSessionId);
            SetLiteralText(sheet.Cell(row, 2), session.EmployeeNumber);
            SetLiteralText(sheet.Cell(row, 3), session.EmployeeName);
            sheet.Cell(row, 4).SetValue(session.ShiftDate.ToDateTime(TimeOnly.MinValue));
            sheet.Cell(row, 5).SetValue(session.Start);
            sheet.Cell(row, 6).SetValue(session.End);
            SetLiteralText(sheet.Cell(row, 7), session.Status);
            SetLiteralText(sheet.Cell(row, 8), session.StoredTimezone);
            sheet.Cell(row, 9).SetValue(session.ElapsedSeconds);
            sheet.Cell(row, 10).SetValue(session.BreakSeconds);
            sheet.Cell(row, 11).SetValue(session.WorkedSeconds);
        }

        sheet.Columns(4, 6).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        FinishTabularSheet(sheet, 11);
    }

    private static void WriteBreaks(XLWorkbook workbook, TimeLogExportData data)
    {
        var sheet = workbook.Worksheets.Add("Breaks");
        WriteHeader(sheet,
        [
            "Source Break ID",
            "Source Session ID",
            "Employee Number",
            "Employee Name",
            "Start",
            "End",
            "Status",
            "Duration Seconds"
        ]);

        for (var index = 0; index < data.Breaks.Count; index++)
        {
            var row = index + 2;
            var breakLog = data.Breaks[index];
            sheet.Cell(row, 1).SetValue(breakLog.SourceBreakId);
            sheet.Cell(row, 2).SetValue(breakLog.SourceSessionId);
            SetLiteralText(sheet.Cell(row, 3), breakLog.EmployeeNumber);
            SetLiteralText(sheet.Cell(row, 4), breakLog.EmployeeName);
            sheet.Cell(row, 5).SetValue(breakLog.Start);
            sheet.Cell(row, 6).SetValue(breakLog.End);
            SetLiteralText(sheet.Cell(row, 7), breakLog.Status);
            sheet.Cell(row, 8).SetValue(breakLog.DurationSeconds);
        }

        sheet.Columns(5, 6).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        FinishTabularSheet(sheet, 8);
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
            SetLiteralText(sheet.Cell(1, index + 1), headers[index]);

        var header = sheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.DarkBlue;
    }

    private static void FinishTabularSheet(IXLWorksheet sheet, int columnCount)
    {
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, sheet.LastRowUsed()?.RowNumber() ?? 1), columnCount)
            .SetAutoFilter();
        sheet.Columns(1, columnCount).Width = 20;
    }

    private static void SetCellValue(IXLCell cell, object value)
    {
        switch (value)
        {
            case string text:
                SetLiteralText(cell, text);
                break;
            case DateTime dateTime:
                cell.SetValue(dateTime);
                break;
            case int intValue:
                cell.SetValue(intValue);
                break;
            case long longValue:
                cell.SetValue(longValue);
                break;
            default:
                throw new InvalidOperationException($"Unsupported workbook value type {value.GetType().Name}.");
        }
    }

    private static void SetLiteralText(IXLCell cell, string value)
    {
        cell.SetValue(value);
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            cell.Style.IncludeQuotePrefix = true;
    }
}
