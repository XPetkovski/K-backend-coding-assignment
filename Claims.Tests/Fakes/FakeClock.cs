using Claims.Core.Common;

namespace Claims.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateOnly today)
    {
        UtcNow = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; }
}