namespace Claims.Infrastructure.Auditing;

public class AuditQueueOptions
{
    public const string SectionName = "Auditing";

    public int Capacity { get; set; } = 1_000;

    public int BatchSize { get; set; } = 100;

    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);
}