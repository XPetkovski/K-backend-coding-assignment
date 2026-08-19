namespace Claims.Infrastructure;

public class MongoOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "Claims";
}

public class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool UseTestContainers { get; set; }

    public bool ApplyMigrationsOnStartup { get; set; } = true;

    public MongoOptions Mongo { get; set; } = new();

    public string AuditDbConnectionString { get; set; } = string.Empty;

    public void Validate()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(Mongo.ConnectionString))
        {
            missing.Add($"{SectionName}:{nameof(Mongo)}:{nameof(Mongo.ConnectionString)}");
        }

        if (string.IsNullOrWhiteSpace(Mongo.DatabaseName))
        {
            missing.Add($"{SectionName}:{nameof(Mongo)}:{nameof(Mongo.DatabaseName)}");
        }

        if (string.IsNullOrWhiteSpace(AuditDbConnectionString))
        {
            missing.Add($"{SectionName}:{nameof(AuditDbConnectionString)}");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Persistence is not configured. Missing: {string.Join(", ", missing)}. " +
                $"Either supply these settings or set {SectionName}:{nameof(UseTestContainers)} to true for local development.");
        }
    }
}