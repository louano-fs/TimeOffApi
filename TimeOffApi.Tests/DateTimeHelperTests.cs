using FluentAssertions;
using TimeOffApi.Infrastructure;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class DateTimeHelperTests
{
    [Fact]
    public void UtcDayBounds_advances_an_invalid_midnight_to_the_first_valid_instant()
    {
        var (start, _) = DateTimeHelper.UtcDayBounds(
            new DateTime(2026, 3, 8, 16, 0, 0, DateTimeKind.Utc),
            "America/Havana");
        var (_, previousEnd) = DateTimeHelper.UtcDayBounds(
            new DateTime(2026, 3, 7, 16, 0, 0, DateTimeKind.Utc),
            "America/Havana");

        start.Should().Be(new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc));
        previousEnd.Should().Be(start);
    }

    [Fact]
    public void UtcDayBounds_chooses_the_earliest_utc_instant_for_an_ambiguous_midnight()
    {
        var (start, _) = DateTimeHelper.UtcDayBounds(
            new DateTime(2026, 11, 1, 16, 0, 0, DateTimeKind.Utc),
            "America/Havana");

        start.Should().Be(new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc));
    }
}
