namespace CosmosDbMigration.Configuration
{
    public class CosmosEndpointSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class CosmosDbSettings
    {
        public CosmosEndpointSettings Source { get; set; } = new();
        public CosmosEndpointSettings Destination { get; set; } = new();
    }

    public class MigrationSettings
    {
        public int BatchSize { get; set; } = 100;
        public bool DryRun { get; set; } = false;
        public bool ShowDetailedErrors { get; set; } = true;
        public int MaxErrorsToDisplay { get; set; } = 10;
    }

    public class AppSettings
    {
        public CosmosDbSettings CosmosDb { get; set; } = new();
        public MigrationSettings MigrationSettings { get; set; } = new();
    }
}