using Linden.Api.Services;
using Xunit;

namespace Linden.Tests;

public class ScheduleRulesTests
{
    // A Tuesday at noon. Offsets below are counted from this day.
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0);

    [Theory]
    [InlineData(1, 10, 0, 50, true)]    // Wednesday 10:00
    [InlineData(1, 8, 0, 50, true)]     // starts at opening time
    [InlineData(1, 18, 10, 50, true)]   // ends exactly at closing time
    [InlineData(0, 9, 0, 50, false)]    // earlier today
    [InlineData(4, 10, 0, 50, false)]   // Saturday
    [InlineData(5, 10, 0, 50, false)]   // Sunday
    [InlineData(1, 7, 30, 50, false)]   // before opening
    [InlineData(1, 18, 30, 50, false)]  // ends after closing
    public void CheckSlot_follows_the_working_hours(int addDays, int hour, int minute, int length, bool allowed)
    {
        var start = Now.Date.AddDays(addDays).AddHours(hour).AddMinutes(minute);

        var error = ScheduleRules.CheckSlot(start, length, Now);

        Assert.Equal(allowed, error is null);
    }
}
