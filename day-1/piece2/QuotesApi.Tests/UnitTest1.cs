using QuotesApi.Services;

namespace QuotesApi.Tests;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}

public class ClockTests
{
    [Fact]
    public void FakeClock_Returns_Controlled_Time()
    {
        var expected = new DateTimeOffset(
            2026, 8, 11, 10, 30, 0, TimeSpan.Zero);

        var clock = new FakeClock
        {
            UtcNow = expected
        };

        Assert.Equal(expected, clock.UtcNow);
    }
}
