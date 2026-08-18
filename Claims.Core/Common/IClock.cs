namespace Claims.Core.Common;

public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}