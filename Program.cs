using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using CosmosDbMigration.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CosmosDbMigration
{
    class Program
    {
        private static AppSettings? _settings;

        static async Task Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Cosmos DB Migration Tool - Hierarchical Keys");
            Console.WriteLine("==============================================\n");

            // Carica la configurazione
            try
            {
                _settings = LoadConfiguration();
                Console.WriteLine("✅ Configuration loaded successfully");
                Console.WriteLine($"   Source Database: {_settings.CosmosDb.Source.DatabaseName}");

                // Determina se usa lo stesso account o uno diverso
                var useSameAccount = string.IsNullOrWhiteSpace(_settings.CosmosDb.Destination.ConnectionString);
                var useSameDatabase = string.IsNullOrWhiteSpace(_settings.CosmosDb.Destination.DatabaseName);

                if (useSameAccount && useSameDatabase)
                {
                    Console.WriteLine($"   Destination: Same account and database");
                }
                else if (useSameAccount)
                {
                    Console.WriteLine($"   Destination Database: {_settings.CosmosDb.Destination.DatabaseName} (same account)");
                }
                else
                {
                    Console.WriteLine($"   Destination: Different account");
                    Console.WriteLine($"   Destination Database: {_settings.CosmosDb.Destination.DatabaseName}");
                }

                Console.WriteLine($"   Batch size: {_settings.MigrationSettings.BatchSize}");
                Console.WriteLine($"   Dry run: {(_settings.MigrationSettings.DryRun ? "YES" : "NO")}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load configuration: {ex.Message}");
                Console.WriteLine("\nMake sure appsettings.json exists and is properly configured.");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Determina le connessioni
                var sourceConnectionString = _settings.CosmosDb.Source.ConnectionString;
                var sourceDatabaseName = _settings.CosmosDb.Source.DatabaseName;

                var destinationConnectionString = string.IsNullOrWhiteSpace(_settings.CosmosDb.Destination.ConnectionString)
                    ? sourceConnectionString
                    : _settings.CosmosDb.Destination.ConnectionString;

                var destinationDatabaseName = string.IsNullOrWhiteSpace(_settings.CosmosDb.Destination.DatabaseName)
                    ? sourceDatabaseName
                    : _settings.CosmosDb.Destination.DatabaseName;

                var useSameAccount = sourceConnectionString == destinationConnectionString;
                var useSameDatabase = useSameAccount && sourceDatabaseName == destinationDatabaseName;

                // Crea i client Cosmos DB
                var clientOptions = new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    },
                    ConnectionMode = ConnectionMode.Direct,
                    MaxRetryAttemptsOnRateLimitedRequests = 9,
                    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
                };

                using var sourceClient = new CosmosClient(sourceConnectionString, clientOptions);
                var sourceDatabase = sourceClient.GetDatabase(sourceDatabaseName);

                // Usa lo stesso client se è lo stesso account, altrimenti creane uno nuovo
                CosmosClient destinationClient;
                Database destinationDatabase;

                if (useSameAccount)
                {
                    destinationClient = sourceClient;
                    destinationDatabase = useSameDatabase
                        ? sourceDatabase
                        : sourceClient.GetDatabase(destinationDatabaseName);
                }
                else
                {
                    destinationClient = new CosmosClient(destinationConnectionString, clientOptions);
                    destinationDatabase = destinationClient.GetDatabase(destinationDatabaseName);
                }

                // Lista i container disponibili nel SOURCE
                Console.WriteLine($"📂 SOURCE Database: {sourceDatabaseName}\n");
                Console.WriteLine("Analyzing source containers (this may take a moment)...\n");
                Console.WriteLine("Available source containers:");
                Console.WriteLine(new string('-', 100));
                Console.WriteLine($"{"#",-4} {"Container Name",-30} {"Documents",-15} {"Partition Key",-40}");
                Console.WriteLine(new string('-', 100));

                var sourceContainers = new List<(string Name, int DocumentCount)>();
                var sourceIterator = sourceDatabase.GetContainerQueryIterator<ContainerProperties>();
                int containerIndex = 1;

                while (sourceIterator.HasMoreResults)
                {
                    var response = await sourceIterator.ReadNextAsync();
                    foreach (var containerProps in response)
                    {
                        var container = sourceDatabase.GetContainer(containerProps.Id);
                        int documentCount = await GetDocumentCountAsync(container);

                        sourceContainers.Add((containerProps.Id, documentCount));

                        var partitionKeyPathsDisplay = string.Join(", ", containerProps.PartitionKeyPaths);
                        var keyType = containerProps.PartitionKeyPaths.Count > 1 ? " [Hierarchical]" : "";

                        Console.WriteLine($"{containerIndex,-4} {containerProps.Id,-30} {documentCount.ToString("N0"),-15} {partitionKeyPathsDisplay + keyType}");
                        containerIndex++;
                    }
                }

                Console.WriteLine(new string('-', 100));

                if (!sourceContainers.Any())
                {
                    Console.WriteLine("❌ No containers found in source database!");
                    return;
                }

                Console.WriteLine();

                // Lista i container disponibili nel DESTINATION (se è un database diverso)
                List<(string Name, int DocumentCount)> destinationContainers;

                if (!useSameDatabase)
                {
                    Console.WriteLine($"📂 DESTINATION Database: {destinationDatabaseName}\n");
                    Console.WriteLine("Analyzing destination containers...\n");
                    Console.WriteLine("Available destination containers:");
                    Console.WriteLine(new string('-', 100));
                    Console.WriteLine($"{"#",-4} {"Container Name",-30} {"Documents",-15} {"Partition Key",-40}");
                    Console.WriteLine(new string('-', 100));

                    destinationContainers = new List<(string Name, int DocumentCount)>();
                    var destIterator = destinationDatabase.GetContainerQueryIterator<ContainerProperties>();
                    containerIndex = 1;

                    while (destIterator.HasMoreResults)
                    {
                        var response = await destIterator.ReadNextAsync();
                        foreach (var containerProps in response)
                        {
                            var container = destinationDatabase.GetContainer(containerProps.Id);
                            int documentCount = await GetDocumentCountAsync(container);

                            destinationContainers.Add((containerProps.Id, documentCount));

                            var partitionKeyPathsDisplay = string.Join(", ", containerProps.PartitionKeyPaths);
                            var keyType = containerProps.PartitionKeyPaths.Count > 1 ? " [Hierarchical]" : "";

                            Console.WriteLine($"{containerIndex,-4} {containerProps.Id,-30} {documentCount.ToString("N0"),-15} {partitionKeyPathsDisplay + keyType}");
                            containerIndex++;
                        }
                    }

                    Console.WriteLine(new string('-', 100));
                    Console.WriteLine();
                }
                else
                {
                    destinationContainers = sourceContainers;
                }

                // Chiedi il container sorgente
                string sourceContainerName = GetContainerSelection(
                    "Enter SOURCE container name (or number): ",
                    sourceContainers.Select(c => c.Name).ToList()
                );

                // Chiedi il container destinazione
                string destinationContainerName = GetContainerSelection(
                    "Enter DESTINATION container name (or number, or 'new' to create): ",
                    destinationContainers.Select(c => c.Name).ToList(),
                    allowNew: true
                );

                // Validazione
                if (useSameDatabase && sourceContainerName == destinationContainerName)
                {
                    Console.WriteLine("\n❌ ERROR: Source and destination cannot be the same container when using the same database!");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                // Verifica container sorgente
                Console.WriteLine($"\n📂 Checking source container: {sourceContainerName}");
                var sourceContainer = sourceDatabase.GetContainer(sourceContainerName);

                ContainerProperties? sourceContainerProperties = null;
                try
                {
                    var sourceContainerResponse = await sourceContainer.ReadContainerAsync();
                    sourceContainerProperties = sourceContainerResponse.Resource;
                    var sourcePartitionKeys = string.Join(", ", sourceContainerProperties.PartitionKeyPaths);
                    var sourceDocCount = sourceContainers.FirstOrDefault(c => c.Name == sourceContainerName).DocumentCount;
                    Console.WriteLine($"   ✅ Source container exists");
                    Console.WriteLine($"   📊 Partition key: {sourcePartitionKeys}");
                    Console.WriteLine($"   📄 Document count: {sourceDocCount:N0}\n");
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"   ❌ Source container '{sourceContainerName}' not found!");
                    return;
                }

                // Gestisci container destinazione
                Console.WriteLine($"📂 Preparing destination container: {destinationContainerName}");
                Container destinationContainer;
                bool isNewContainer = !destinationContainers.Any(c => c.Name == destinationContainerName);

                if (isNewContainer)
                {
                    Console.WriteLine("   ℹ️  Container doesn't exist, it will be created");

                    // Chiedi conferma per la partition key
                    Console.WriteLine("\n   ⚙️  Configure hierarchical partition keys:");
                    Console.Write("   First partition key path (default: /tenantId): ");
                    var firstKey = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(firstKey)) firstKey = "/tenantId";
                    if (!firstKey.StartsWith("/")) firstKey = "/" + firstKey;

                    Console.Write("   Second partition key path (default: /id): ");
                    var secondKey = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(secondKey)) secondKey = "/id";
                    if (!secondKey.StartsWith("/")) secondKey = "/" + secondKey;

                    Console.WriteLine($"\n   Creating container with partition keys: [{firstKey}, {secondKey}]");

                    var containerProperties = new ContainerProperties(
                        destinationContainerName,
                        new List<string> { firstKey, secondKey }
                    );

                    // Chiedi il throughput
                    Console.Write("   Throughput mode (1=Manual 400 RU/s, 2=Autoscale 4000 RU/s, default: 2): ");
                    var throughputChoice = Console.ReadLine();

                    ThroughputProperties? throughput = throughputChoice == "1"
                        ? ThroughputProperties.CreateManualThroughput(400)
                        : ThroughputProperties.CreateAutoscaleThroughput(4000);

                    var containerResponse = await destinationDatabase.CreateContainerAsync(
                        containerProperties,
                        throughput
                    );

                    destinationContainer = containerResponse.Container;
                    Console.WriteLine($"   ✅ Container created successfully\n");
                }
                else
                {
                    // Container esiste già
                    destinationContainer = destinationDatabase.GetContainer(destinationContainerName);
                    var destContainerResponse = await destinationContainer.ReadContainerAsync();
                    var destContainerProps = destContainerResponse.Resource;
                    var destPartitionKeys = string.Join(", ", destContainerProps.PartitionKeyPaths);
                    var destDocCount = destinationContainers.FirstOrDefault(c => c.Name == destinationContainerName).DocumentCount;

                    Console.WriteLine($"   ✅ Destination container exists");
                    Console.WriteLine($"   📊 Partition key: {destPartitionKeys}");
                    Console.WriteLine($"   📄 Current document count: {destDocCount:N0}");

                    if (destDocCount > 0)
                    {
                        Console.WriteLine($"\n   ⚠️  WARNING: Destination container already has {destDocCount:N0} documents!");
                        Console.WriteLine("   Migration will use UPSERT (existing documents may be overwritten).");
                    }

                    // Mostra informazioni sulla configurazione delle partition key
                    Console.WriteLine($"\n   ℹ️  Partition key configuration: {destPartitionKeys}");
                    Console.WriteLine("   ℹ️  Note: Cosmos DB will automatically manage indexes after migration");

                    Console.WriteLine();
                }

                // Conteggio documenti sorgente (riutilizza il conteggio già fatto)
                var sourceDocumentCount = sourceContainers.FirstOrDefault(c => c.Name == sourceContainerName).DocumentCount;

                Console.WriteLine("📊 Source data summary:");
                Console.WriteLine($"   Total documents to migrate: {sourceDocumentCount:N0}\n");

                if (sourceDocumentCount == 0)
                {
                    Console.WriteLine("⚠️  No documents to migrate!");
                    return;
                }

                // Chiedi conferma finale
                Console.WriteLine("==============================================");
                Console.WriteLine("MIGRATION SUMMARY");
                Console.WriteLine("==============================================");
                Console.WriteLine($"Source Account:   {GetAccountName(sourceConnectionString)}");
                Console.WriteLine($"Source Database:  {sourceDatabaseName}");
                Console.WriteLine($"Source Container: {sourceContainerName} ({sourceDocumentCount:N0} documents)");
                Console.WriteLine();
                Console.WriteLine($"Dest Account:     {GetAccountName(destinationConnectionString)} {(useSameAccount ? "[SAME]" : "[DIFFERENT]")}");
                Console.WriteLine($"Dest Database:    {destinationDatabaseName} {(useSameDatabase ? "[SAME]" : "[DIFFERENT]")}");
                var destExistingCount = destinationContainers.FirstOrDefault(c => c.Name == destinationContainerName).DocumentCount;
                Console.WriteLine($"Dest Container:   {destinationContainerName} ({(isNewContainer ? "new container" : $"{destExistingCount:N0} existing documents")})");
                Console.WriteLine();
                Console.WriteLine($"Documents:        {sourceDocumentCount:N0}");
                Console.WriteLine($"Batch size:       {_settings.MigrationSettings.BatchSize}");
                Console.WriteLine($"Dry run:          {(_settings.MigrationSettings.DryRun ? "YES (no data will be written)" : "NO (data will be written)")}");
                Console.WriteLine("==============================================");
                Console.Write("\nProceed with migration? (y/n): ");

                var confirm = Console.ReadLine();
                if (confirm?.ToLower() != "y")
                {
                    Console.WriteLine("Migration cancelled by user.");
                    return;
                }

                Console.WriteLine();

                // Verifica che tutti i documenti abbiano i campi necessari
                Console.WriteLine("🔍 Checking data integrity...");

                // Ottieni le partition key paths del container destinazione
                var destPropsForValidation = await destinationContainer.ReadContainerAsync();
                var destinationPartitionKeyPaths = destPropsForValidation.Resource.PartitionKeyPaths
                    .Select(p => p.TrimStart('/'))
                    .ToList();

                foreach (var pkPath in destinationPartitionKeyPaths)
                {
                    var missingQuery = $"SELECT VALUE COUNT(1) FROM c WHERE NOT IS_DEFINED(c.{pkPath}) OR c.{pkPath} = null";
                    var missingIterator = sourceContainer.GetItemQueryIterator<int>(missingQuery);
                    var missingResponse = await missingIterator.ReadNextAsync();
                    var missingCount = missingResponse.FirstOrDefault();

                    if (missingCount > 0)
                    {
                        Console.WriteLine($"   ⚠️  WARNING: {missingCount:N0} documents are missing field '{pkPath}'!");
                        Console.WriteLine($"   These documents will be SKIPPED during migration.");
                    }
                    else
                    {
                        Console.WriteLine($"   ✅ All documents have field '{pkPath}'");
                    }
                }

                Console.WriteLine();

                if (_settings.MigrationSettings.DryRun)
                {
                    Console.WriteLine("🔧 DRY RUN MODE - No data will be written\n");
                }

                // Migrazione
                Console.WriteLine($"🚀 Starting migration...\n");

                var query = new QueryDefinition("SELECT * FROM c");
                var queryIterator = sourceContainer.GetItemQueryIterator<dynamic>(query);

                int processedCount = 0;
                int successCount = 0;
                int skippedCount = 0;
                int errorCount = 0;
                var errors = new List<string>();
                double totalRU = 0;

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    totalRU += response.RequestCharge;

                    foreach (var document in response)
                    {
                        processedCount++;

                        try
                        {
                            // Verifica campi obbligatori basati sulle partition keys
                            bool hasAllKeys = true;
                            var partitionKeyBuilder = new PartitionKeyBuilder();

                            foreach (var pkPath in destinationPartitionKeyPaths)
                            {
                                var value = GetPropertyValue(document, pkPath);

                                if (value == null)
                                {
                                    // DEBUG: Mostra quali chiavi sono presenti nel documento
                                    var dict = (IDictionary<string, object>)document;
                                    var availableKeys = string.Join(", ", dict.Keys.Take(10));

                                    Console.WriteLine($"   🔍 DEBUG: Looking for '{pkPath}', available keys: {availableKeys}");

                                    if (_settings.MigrationSettings.ShowDetailedErrors)
                                    {
                                        Console.WriteLine($"   ⚠️  Skipped: Document without '{pkPath}' (id: {document.id ?? "unknown"})");
                                    }
                                    hasAllKeys = false;
                                    break;
                                }

                                partitionKeyBuilder.Add(value.ToString());
                            }

                            if (!hasAllKeys)
                            {
                                skippedCount++;
                                continue;
                            }

                            if (!_settings.MigrationSettings.DryRun)
                            {
                                // Crea la partition key
                                var partitionKey = partitionKeyBuilder.Build();

                                // Inserisci il documento
                                await destinationContainer.UpsertItemAsync(
                                    document,
                                    partitionKey
                                );
                            }

                            successCount++;

                            // Progress report
                            if (processedCount % _settings.MigrationSettings.BatchSize == 0)
                            {
                                var percentage = (double)processedCount / sourceDocumentCount * 100;
                                var avgSpeed = processedCount / stopwatch.Elapsed.TotalSeconds;
                                var eta = avgSpeed > 0 ? TimeSpan.FromSeconds((sourceDocumentCount - processedCount) / avgSpeed) : TimeSpan.Zero;

                                Console.WriteLine($"   📈 Progress: {processedCount:N0}/{sourceDocumentCount:N0} ({percentage:F1}%) - " +
                                                $"Success: {successCount:N0}, Skipped: {skippedCount}, Errors: {errorCount} - " +
                                                $"Speed: {avgSpeed:F0} docs/s - ETA: {eta:hh\\:mm\\:ss}");
                            }
                        }
                        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                        {
                            // Documento già esiste
                            successCount++; // Conta come successo se già presente
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            var errorMsg = $"Document {document.id ?? "unknown"}: {ex.Message}";
                            errors.Add(errorMsg);

                            if (_settings.MigrationSettings.ShowDetailedErrors &&
                                errorCount <= _settings.MigrationSettings.MaxErrorsToDisplay)
                            {
                                Console.WriteLine($"   ❌ Error: {errorMsg}");
                            }
                        }
                    }

                    // Mostra RU consumed ogni batch
                    if (processedCount % (_settings.MigrationSettings.BatchSize * 5) == 0)
                    {
                        Console.WriteLine($"   💰 Total RU consumed so far: {totalRU:F2}");
                    }
                }

                stopwatch.Stop();

                // Report finale
                Console.WriteLine("\n==============================================");
                Console.WriteLine("MIGRATION COMPLETE");
                Console.WriteLine("==============================================");
                Console.WriteLine($"Total documents processed:  {processedCount:N0}");
                Console.WriteLine($"Successfully migrated:      {successCount:N0}");
                Console.WriteLine($"Skipped (missing data):     {skippedCount:N0}");
                Console.WriteLine($"Errors:                     {errorCount:N0}");
                Console.WriteLine($"Time elapsed:               {stopwatch.Elapsed:hh\\:mm\\:ss}");
                Console.WriteLine($"Average speed:              {(processedCount / stopwatch.Elapsed.TotalSeconds):F0} docs/sec");
                Console.WriteLine($"Total RU consumed:          {totalRU:F2}");
                Console.WriteLine($"Average RU per document:    {(totalRU / processedCount):F2}");

                if (errors.Any())
                {
                    Console.WriteLine($"\n⚠️  {errors.Count} errors occurred:");
                    var maxErrorsToShow = Math.Min(errors.Count, _settings.MigrationSettings.MaxErrorsToDisplay);
                    foreach (var error in errors.Take(maxErrorsToShow))
                    {
                        Console.WriteLine($"   - {error}");
                    }
                    if (errors.Count > maxErrorsToShow)
                    {
                        Console.WriteLine($"   ... and {errors.Count - maxErrorsToShow} more errors");
                    }
                }

                if (!_settings.MigrationSettings.DryRun)
                {
                    Console.WriteLine("\n✅ Migration completed successfully!");

                    // Verifica finale
                    var finalDestCount = await GetDocumentCountAsync(destinationContainer);
                    Console.WriteLine($"📊 Final destination document count: {finalDestCount:N0}");
                }
                else
                {
                    Console.WriteLine("\n✅ Dry run completed successfully! No data was written.");
                }

                // Cleanup del client destinazione se diverso
                if (!useSameAccount && destinationClient != sourceClient)
                {
                    destinationClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ FATAL ERROR: {ex.Message}");
                if (_settings?.MigrationSettings.ShowDetailedErrors == true)
                {
                    Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Estrae il nome dell'account dalla connection string
        /// </summary>
        private static string GetAccountName(string connectionString)
        {
            try
            {
                var parts = connectionString.Split(';');
                var accountEndpoint = parts.FirstOrDefault(p => p.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase));
                if (accountEndpoint != null)
                {
                    var uri = new Uri(accountEndpoint.Split('=')[1]);
                    return uri.Host.Split('.')[0];
                }
            }
            catch { }

            return "Unknown";
        }

        /// <summary>
        /// Conta i documenti in un container
        /// </summary>
        private static async Task<int> GetDocumentCountAsync(Container container)
        {
            try
            {
                var countQuery = "SELECT VALUE COUNT(1) FROM c";
                var iterator = container.GetItemQueryIterator<int>(countQuery);
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Carica la configurazione da appsettings.json
        /// </summary>
        private static AppSettings LoadConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);

            // Validazione Source
            if (string.IsNullOrWhiteSpace(settings.CosmosDb.Source.ConnectionString))
            {
                throw new InvalidOperationException("CosmosDb:Source:ConnectionString is missing in appsettings.json");
            }

            if (string.IsNullOrWhiteSpace(settings.CosmosDb.Source.DatabaseName))
            {
                throw new InvalidOperationException("CosmosDb:Source:DatabaseName is missing in appsettings.json");
            }

            // Se Destination.ConnectionString è vuoto, usa Source
            if (string.IsNullOrWhiteSpace(settings.CosmosDb.Destination.ConnectionString))
            {
                settings.CosmosDb.Destination.ConnectionString = settings.CosmosDb.Source.ConnectionString;
            }

            // Se Destination.DatabaseName è vuoto, usa Source
            if (string.IsNullOrWhiteSpace(settings.CosmosDb.Destination.DatabaseName))
            {
                settings.CosmosDb.Destination.DatabaseName = settings.CosmosDb.Source.DatabaseName;
            }

            return settings;
        }

        /// <summary>
        /// Chiede all'utente di selezionare un container dalla lista
        /// </summary>
        private static string GetContainerSelection(string prompt, List<string> availableContainers, bool allowNew = false)
        {
            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                {
                    Console.WriteLine("   ❌ Please enter a valid container name or number\n");
                    continue;
                }

                // Permetti "new" per creare un nuovo container
                if (allowNew && input.ToLower() == "new")
                {
                    Console.Write("   Enter new container name: ");
                    var newName = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(newName))
                    {
                        Console.WriteLine("   ❌ Invalid container name\n");
                        continue;
                    }

                    return newName;
                }

                // Controlla se è un numero (indice)
                if (int.TryParse(input, out int index) && index >= 1 && index <= availableContainers.Count)
                {
                    return availableContainers[index - 1];
                }

                // Controlla se è un nome valido
                if (availableContainers.Contains(input, StringComparer.OrdinalIgnoreCase))
                {
                    return availableContainers.First(c => c.Equals(input, StringComparison.OrdinalIgnoreCase));
                }

                // Se allowNew è true e il nome non esiste, permetti di crearlo
                if (allowNew)
                {
                    Console.Write($"   Container '{input}' doesn't exist. Create it? (y/n): ");
                    var create = Console.ReadLine();
                    if (create?.ToLower() == "y")
                    {
                        return input;
                    }
                }

                Console.WriteLine($"   ❌ Container '{input}' not found. Please try again.\n");
            }
        }

        /// <summary>
        /// Ottiene il valore di una proprietà da un oggetto dinamico (case-insensitive)
        /// </summary>
        /// <summary>
        /// Ottiene il valore di una proprietà da un oggetto dinamico (supporta JObject e Dictionary)
        /// </summary>
        private static object? GetPropertyValue(dynamic obj, string propertyName)
        {
            try
            {
                // Caso 1: Se è un JObject (Newtonsoft.Json)
                if (obj is Newtonsoft.Json.Linq.JObject jObject)
                {
                    // Prova prima case-sensitive
                    if (jObject.TryGetValue(propertyName, out var token))
                    {
                        return token?.Type == Newtonsoft.Json.Linq.JTokenType.Null ? null : token;
                    }

                    // Fallback: case-insensitive
                    var property = jObject.Properties()
                        .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

                    if (property != null)
                    {
                        return property.Value?.Type == Newtonsoft.Json.Linq.JTokenType.Null ? null : property.Value;
                    }

                    return null;
                }

                // Caso 2: Se è un Dictionary standard
                var dict = (IDictionary<string, object>)obj;

                // Prova prima case-sensitive
                if (dict.ContainsKey(propertyName))
                {
                    return dict[propertyName];
                }

                // Fallback: case-insensitive
                var key = dict.Keys.FirstOrDefault(k =>
                    k.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

                if (key != null)
                {
                    return dict[key];
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}