# Cosmos DB Migration Tool - Hierarchical Partition Keys

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![Azure Cosmos DB](https://img.shields.io/badge/Azure-Cosmos%20DB-blue.svg)](https://azure.microsoft.com/services/cosmos-db/)

A command-line tool for migrating documents between Azure Cosmos DB containers with full support for **Hierarchical Partition Keys** (multi-level partitioning).

---

## ⚠️ Disclaimer

**USE AT YOUR OWN RISK**: This tool is provided "as is" without warranty of any kind. Always test migrations in a non-production environment first and verify results before using in production. The author(s) are not responsible for any data loss, corruption, or costs incurred from using this tool.

**ALWAYS:**
- ✅ Backup your data before migration
- ✅ Test with `DryRun: true` first
- ✅ Verify results after migration
- ✅ Monitor RU consumption and costs

---

## 📋 Table of Contents

- [Features](#-features)
- [Quick Start](#-quick-start)
- [Installation](#-installation)
- [Configuration](#️-configuration)
- [Usage Guide](#-usage-guide)
- [Hierarchical Partition Keys](#-understanding-hierarchical-partition-keys)
- [Configuration Examples](#-configuration-examples)
- [Best Practices](#-best-practices)
- [Troubleshooting](#️-troubleshooting)
- [Performance Tips](#-performance-tips)
- [Security](#-security-considerations)
- [Contributing](#-contributing)
- [License](#-license)

---

## ✨ Features

✅ **Cross-Account Migration** - Migrate between different Cosmos DB accounts  
✅ **Cross-Database Migration** - Migrate between different databases  
✅ **Same-Database Migration** - Migrate between containers in the same database  
✅ **Hierarchical Partition Keys** - Full support for multi-level partitioning (e.g., `/tenantId, /id`)  
✅ **Interactive Container Selection** - Choose source and destination from a numbered list  
✅ **Document Count Display** - See document counts for each container before migration  
✅ **Data Integrity Checks** - Validates partition key fields exist before migration  
✅ **Progress Tracking** - Real-time progress with speed and ETA calculations  
✅ **RU Monitoring** - Track Request Units consumed during migration  
✅ **Dry Run Mode** - Test migration without writing any data  
✅ **Automatic Retry** - Built-in retry logic for rate limiting (429 errors)  
✅ **Error Handling** - Robust error handling with detailed logging  
✅ **Container Creation** - Optionally create destination container during migration  
✅ **UPSERT Support** - Safely merge into existing containers  

---

## 🚀 Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Azure Cosmos DB account with connection string
- Read/Write access to source and destination containers

### Installation

```bash
# Clone the repository
git clone https://github.com/albertopola/CosmosDbMigration.git
cd CosmosDbMigration

# Restore dependencies
dotnet restore

# Build the project
dotnet build
```

### Basic Usage

1. Copy and configure `appsettings.json`:
```bash
cp appsettings.template.json appsettings.json
# Edit appsettings.json with your connection details
```

2. Run the tool:
```bash
dotnet run
```

3. Follow the interactive prompts to select source and destination containers.

---

## 📦 Installation

### Option 1: Clone from GitHub

```bash
git clone https://github.com/albertopola/CosmosDbMigration.git
cd CosmosDbMigration
dotnet restore
dotnet build
```

### Option 2: Download Release

1. Go to [Releases](https://github.com/albertopola/CosmosDbMigration/releases)
2. Download the latest release for your platform
3. Extract and run:
```bash
dotnet CosmosDbMigration.dll
```

### Option 3: Publish as Self-Contained Executable

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained

# macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

---

## ⚙️ Configuration

The tool uses `appsettings.json` for all configuration. You never need to modify the source code.

### Configuration File Structure

```json
{
  "CosmosDb": {
    "Source": {
      "ConnectionString": "AccountEndpoint=https://...;AccountKey=...;",
      "DatabaseName": "YourDatabaseName"
    },
    "Destination": {
      "ConnectionString": "",
      "DatabaseName": ""
    }
  },
  "MigrationSettings": {
    "BatchSize": 100,
    "DryRun": false,
    "ShowDetailedErrors": true,
    "MaxErrorsToDisplay": 10
  }
}
```

### Configuration Options

#### CosmosDb Section

##### Source (Required)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ConnectionString` | string | **Yes** | Cosmos DB connection string (format: `AccountEndpoint=https://...;AccountKey=...;`) |
| `DatabaseName` | string | **Yes** | Name of the source database |

**How to get your connection string:**
1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your Cosmos DB account
3. Go to **Keys** (under Settings)
4. Copy **PRIMARY CONNECTION STRING**

##### Destination (Optional)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ConnectionString` | string | No | Destination Cosmos DB connection string. **Leave empty** to use source account. |
| `DatabaseName` | string | No | Destination database name. **Leave empty** to use source database. |

**Behavior:**
- **Both empty**: Same account and database (container-to-container migration)
- **ConnectionString empty, DatabaseName set**: Same account, different database
- **Both set**: Different account (cross-account migration)

#### MigrationSettings Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BatchSize` | int | 100 | Documents to process before showing progress. Range: 50-1000. |
| `DryRun` | bool | false | If `true`, simulates migration without writing data. |
| `ShowDetailedErrors` | bool | true | Display detailed error messages for each failed document. |
| `MaxErrorsToDisplay` | int | 10 | Maximum number of errors to show in console. |

---

## 📖 Configuration Examples

### Example 1: Same Account, Same Database

**Scenario:** Migrating from `Data` container (old `/id` partition) to `Data1` container (new `/tenantId, /id` hierarchical partition) in the same database.

```json
{
  "CosmosDb": {
    "Source": {
      "ConnectionString": "AccountEndpoint=https://my-account.documents.azure.com:443/;AccountKey=abc123...;",
      "DatabaseName": "CantileverDB"
    },
    "Destination": {
      "ConnectionString": "",
      "DatabaseName": ""
    }
  },
  "MigrationSettings": {
    "BatchSize": 100,
    "DryRun": false,
    "ShowDetailedErrors": true,
    "MaxErrorsToDisplay": 10
  }
}
```

---

### Example 2: Same Account, Different Database

**Scenario:** Migrating from development database to production database.

```json
{
  "CosmosDb": {
    "Source": {
      "ConnectionString": "AccountEndpoint=https://my-account.documents.azure.com:443/;AccountKey=abc123...;",
      "DatabaseName": "CantileverDB_Dev"
    },
    "Destination": {
      "ConnectionString": "",
      "DatabaseName": "CantileverDB_Prod"
    }
  },
  "MigrationSettings": {
    "BatchSize": 200,
    "DryRun": false,
    "ShowDetailedErrors": true,
    "MaxErrorsToDisplay": 10
  }
}
```

---

### Example 3: Different Accounts (Cross-Account)

**Scenario:** Migrating from a development account to a production account.

```json
{
  "CosmosDb": {
    "Source": {
      "ConnectionString": "AccountEndpoint=https://dev-account.documents.azure.com:443/;AccountKey=devkey123...;",
      "DatabaseName": "CantileverDB"
    },
    "Destination": {
      "ConnectionString": "AccountEndpoint=https://prod-account.documents.azure.com:443/;AccountKey=prodkey456...;",
      "DatabaseName": "CantileverDB"
    }
  },
  "MigrationSettings": {
    "BatchSize": 500,
    "DryRun": false,
    "ShowDetailedErrors": false,
    "MaxErrorsToDisplay": 20
  }
}
```

---

### Example 4: Dry Run (Testing)

**Scenario:** Test migration without writing data to estimate time and costs.

```json
{
  "CosmosDb": {
    "Source": {
      "ConnectionString": "AccountEndpoint=https://my-account.documents.azure.com:443/;AccountKey=abc123...;",
      "DatabaseName": "CantileverDB"
    },
    "Destination": {
      "ConnectionString": "",
      "DatabaseName": ""
    }
  },
  "MigrationSettings": {
    "BatchSize": 100,
    "DryRun": true,
    "ShowDetailedErrors": true,
    "MaxErrorsToDisplay": 10
  }
}
```

---

## 🎯 Usage Guide

### Step-by-Step Workflow

#### 1. Start the Tool

```bash
dotnet run
```

#### 2. Review Configuration

```
==============================================
Cosmos DB Migration Tool - Hierarchical Keys
==============================================

✅ Configuration loaded successfully
   Source Database: CantileverDB
   Destination: Same account and database
   Batch size: 100
   Dry run: NO
```

#### 3. View Available Containers

The tool automatically lists all containers with document counts:

```
📂 SOURCE Database: CantileverDB

Analyzing source containers (this may take a moment)...

Available source containers:
----------------------------------------------------------------------------------------------------
#    Container Name                 Documents       Partition Key                           
----------------------------------------------------------------------------------------------------
1    Data                           12,456          /id
2    Data1                          0               /tenantId, /id [Hierarchical]
3    FattureElettroniche           3,456           /id
4    Leases                         12              /id
----------------------------------------------------------------------------------------------------
```

#### 4. Select Source Container

```
Enter SOURCE container name (or number): 1
```

**You can enter:**
- A number: `1`
- A name: `Data`

#### 5. Select Destination Container

```
Enter DESTINATION container name (or number, or 'new' to create): 2
```

**You can enter:**
- A number: `2`
- A name: `Data1`
- `new` - to create a new container

**If you choose `new`:**

```
   Enter new container name: DataMigrated

   ⚙️  Configure hierarchical partition keys:
   First partition key path (default: /tenantId): 
   Second partition key path (default: /id): 

   Creating container with partition keys: [/tenantId, /id]
   Throughput mode (1=Manual 400 RU/s, 2=Autoscale 4000 RU/s, default: 2): 2
   ✅ Container created successfully
```

#### 6. Review Migration Summary

```
==============================================
MIGRATION SUMMARY
==============================================
Source Account:   my-account [SAME]
Source Database:  CantileverDB [SAME]
Source Container: Data (12,456 documents)

Dest Account:     my-account [SAME]
Dest Database:    CantileverDB [SAME]
Dest Container:   Data1 (0 existing documents)

Documents:        12,456
Batch size:       100
Dry run:          NO (data will be written)
==============================================

Proceed with migration? (y/n): 
```

**Review carefully and type `y` to proceed.**

#### 7. Data Integrity Check

```
🔍 Checking data integrity...
   ✅ All documents have field 'tenantId'
   ✅ All documents have field 'id'
```

**Warnings you might see:**
```
   ⚠️  WARNING: 25 documents are missing field 'tenantId'!
   These documents will be SKIPPED during migration.
```

#### 8. Monitor Progress

```
🚀 Starting migration...

   📈 Progress: 100/12,456 (0.8%) - Success: 100, Skipped: 0, Errors: 0 - Speed: 125 docs/s - ETA: 00:01:38
   💰 Total RU consumed so far: 450.25
   📈 Progress: 200/12,456 (1.6%) - Success: 200, Skipped: 0, Errors: 0 - Speed: 132 docs/s - ETA: 00:01:32
   📈 Progress: 500/12,456 (4.0%) - Success: 500, Skipped: 0, Errors: 0 - Speed: 135 docs/s - ETA: 00:01:28
   ...
```

**Progress indicators:**
- **Percentage**: How much of the migration is complete
- **Success**: Documents successfully migrated
- **Skipped**: Documents missing required fields
- **Errors**: Documents that failed to migrate
- **Speed**: Documents per second
- **ETA**: Estimated time remaining

#### 9. Review Final Report

```
==============================================
MIGRATION COMPLETE
==============================================
Total documents processed:  12,456
Successfully migrated:      12,456
Skipped (missing data):     0
Errors:                     0
Time elapsed:               00:01:35
Average speed:              131 docs/sec
Total RU consumed:          5,625.48
Average RU per document:    0.45

✅ Migration completed successfully!
📊 Final destination document count: 12,456

Press any key to exit...
```

---

## 📊 Understanding Hierarchical Partition Keys

### What Are Hierarchical Partition Keys?

Hierarchical (multi-level) partition keys were introduced in Cosmos DB to improve query performance and data organization for multi-tenant applications.

**Traditional partition key:**
```json
{
  "id": "doc123",
  "tenantId": "tenant-A",
  "data": "..."
}// Partition key: /id
```

**Hierarchical partition key:**
```json
{
  "id": "doc123",
  "tenantId": "tenant-A",
  "data": "..."
}// Partition keys: [/tenantId, /id]
```

### Benefits

1. **Tenant Isolation**: Each tenant's data is in its own partition
2. **Better Query Performance**: Queries scoped by tenant are faster and cheaper
3. **Improved Distribution**: Data is distributed across both levels
4. **Automatic Filtering**: Setting tenant context automatically filters all queries

### Example Hierarchy

```
Tenant A
  ├── Document id=001
  ├── Document id=002
  └── Document id=003

Tenant B
  ├── Document id=001  (different partition than Tenant A's id=001)
  └── Document id=002

Tenant C
  └── Document id=001
```

### Uniqueness

With `[/tenantId, /id]`:
- ✅ Same `id` in different tenants: **Allowed**
- ✅ Same `tenantId` with different `id`: **Allowed**
- ❌ Same `[tenantId, id]` combination: **Not allowed** (conflict)

---

## 💡 Best Practices

### 1. Always Test with Dry Run First

```json
"DryRun": true
```

**Before any production migration:**
- Estimate total time
- Calculate RU costs
- Verify data compatibility
- Check for missing fields

**Then set:**
```json
"DryRun": false
```

---

### 2. Choose Appropriate Batch Size

| Dataset Size | Recommended BatchSize | Reason |
|--------------|----------------------|--------|
| < 1,000 docs | 50 | More frequent progress updates |
| 1,000 - 10,000 | 100 | Balanced performance/feedback |
| 10,000 - 100,000 | 250-500 | Reduce console overhead |
| > 100,000 docs | 500-1000 | Optimize for large datasets |

---

### 3. Backup Your Data

**Before any migration:**

```bash
# Option A: Use Azure Data Factory to export to Blob Storage
# Option B: Use Cosmos DB Data Migration Tool
# Option C: Use Azure Backup (if enabled)
```

**At minimum:**
- Take note of document counts
- Export a sample of documents for validation
- Ensure you can restore if needed

---

### 4. Monitor RU Consumption

The tool shows RU consumption in real-time:

```
💰 Total RU consumed so far: 5,625.48
Average RU per document: 0.45
```

**Cost estimation:**
- 1 million RUs ≈ $0.08 USD (varies by region)
- Example: 10,000 docs × 0.45 RU = 4,500 RUs ≈ $0.00036

**To reduce costs:**
- Run during off-peak hours
- Use smaller batch sizes (may be slower but more consistent)
- Temporarily increase destination throughput to avoid 429 throttling

---

### 5. Verify After Migration

**Always verify:**

```sql
-- Source
SELECT COUNT(1) FROM c

-- Destination
SELECT COUNT(1) FROM c
```

**Compare:**
- Document counts
- Sample documents (query a few by ID)
- Application functionality with new container

---

### 6. Run Close to Azure Region

**Best performance:**
- Run from Azure VM in same region as Cosmos DB (fastest, no egress charges)
- Run from local machine (slower, may incur egress costs)

**Cost impact:**
- **Same region**: No egress costs
- **Cross-region**: Egress charges may apply (check Azure pricing)

---

### 7. Handle Large Migrations

For very large datasets (>1 million documents):

**Option A: Increase destination throughput temporarily**
```
1. Set destination to 10,000+ RU/s during migration
2. Run migration
3. Scale back down after completion
```

**Option B: Segment the migration**
```sql
-- Migrate in batches
SELECT * FROM c WHERE c.createdDate >= '2024-01-01' AND c.createdDate < '2024-02-01'
-- Run multiple migrations with date ranges
```

**Option C: Use parallel instances**
- Run multiple instances of the tool
- Each targeting different date ranges or tenant IDs
- Combine results at the end

---

## ⚠️ Troubleshooting

### Issue 1: Configuration File Not Found

**Error:**
```
❌ Failed to load configuration: Could not find file 'appsettings.json'
```

**Solution:**
1. Ensure `appsettings.json` exists in the same directory as the executable
2. If running with `dotnet run`, ensure you're in the project directory
3. Check file permissions

---

### Issue 2: Invalid Connection String

**Error:**
```
❌ FATAL ERROR: The input is not a valid Base-64 string
```

**Solution:**
- Verify connection string format: `AccountEndpoint=https://...;AccountKey=...;`
- Ensure no line breaks or extra spaces
- Copy directly from Azure Portal → Keys section
- Make sure you're using PRIMARY CONNECTION STRING (not just the key)

---

### Issue 3: Missing Partition Key Fields

**Warning:**
```
⚠️  WARNING: 1,234 documents are missing field 'tenantId'!
These documents will be SKIPPED during migration.
```

**Solution:**

**Option A:** Add missing fields before migration
```csharp
// Run a script to add tenantId to all documents
// Then re-run migration
```

**Option B:** Use different partition keys
``` 
// Create destination with partition keys that match your data
// E.g., if all docs have 'userId', use [/userId, /id]
```

**Option C:** Accept documents will be skipped
``` 
// Document the skipped documents
// Handle them separately after migration
```

---

### Issue 4: Rate Limiting (429 Errors)

**Symptom:**
- Migration is very slow
- Many retry messages in console

**Solution:**

1. **Increase destination throughput:**
   ```
   Azure Portal → Container → Scale & Settings → Throughput
   Set to 10,000+ RU/s temporarily
   ```

2. **Reduce batch size:**
   ```json
   "BatchSize": 50
   ```

3. **The tool automatically retries** (up to 9 times with exponential backoff)

---

### Issue 5: Destination Already Has Documents

**Warning:**
```
⚠️  WARNING: Destination container already has 5,000 documents!
Migration will use UPSERT (existing documents may be overwritten).
```

**Options:**

**A. Continue with UPSERT (default)**
- Existing documents with same partition key will be overwritten
- New documents will be added
- Safe if you want to merge/update

**B. Delete and recreate destination**
```bash
# In Azure Portal or CLI
az cosmosdb sql container delete \
  --account-name <account> \
  --database-name <database> \
  --name <container>

# Then run migration tool again
```

**C. Create new destination container**
- Select `new` when prompted for destination
- Give it a different name

---

### Issue 6: Out of Memory

**Error:**
```
System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown
```

**Solution:**

1. **Reduce batch size:**
   ```json
   "BatchSize": 50
   ```

2. **Run on machine with more RAM**

3. **Close other applications**

4. **For very large documents:** Consider custom migration script with streaming

---

## 🚀 Performance Tips

### 1. Optimize Throughput

**Before migration:**
```bash
# Increase destination container throughput
az cosmosdb sql container throughput update \
  --account-name <account> \
  --database-name <database> \
  --name <container> \
  --throughput 10000
```

**After migration:**
```bash
# Scale back down
az cosmosdb sql container throughput update \
  --account-name <account> \
  --database-name <database> \
  --name <container> \
  --throughput 400
```

---

### 2. Run from Azure VM

**Best setup:**
- Deploy Azure VM in same region as Cosmos DB
- Copy tool and appsettings.json to VM
- Run migration from there

**Benefits:**
- 10-100x faster network speed
- No egress charges
- More consistent performance

---

### 3. Monitor Progress

**Save output to file:**
```bash
dotnet run > migration-$(date +%Y%m%d_%H%M%S).log 2>&1
```

**Watch in real-time:**
```bash
dotnet run | tee migration.log
```

---

## 🔒 Security Considerations

### 1. Protect Connection Strings

❌ **NEVER commit `appsettings.json` with real connection strings to source control**

**Add to `.gitignore`:**
```
appsettings.json
appsettings.*.json
*.log
```

**Keep a template:**
```bash
# appsettings.template.json
{
  "CosmosDb": {
    "Source": {
      "ConnectionString": "YOUR_CONNECTION_STRING_HERE",
      "DatabaseName": "YOUR_DATABASE_NAME"
    }
  }
}
```

---

### 2. Use Principle of Least Privilege

**For source (read-only migration):**
- Use **Read-only keys** from Azure Portal → Keys → Read-only Keys

**For destination:**
- Use **Read-write keys** (required for writing)

**For production:**
- Consider using Azure Key Vault
- Use Managed Identity where possible

---

### 3. Rotate Keys After Migration

If you stored keys in `appsettings.json`:

1. Complete migration
2. Regenerate keys in Azure Portal
3. Delete or secure `appsettings.json`

---

### 4. Audit Logging

**Track migrations:**
```bash
# Save complete log
dotnet run > migration-$(date +%Y%m%d_%H%M%S).log 2>&1

# Include in log:
# - Start time
# - Source/destination
# - Document counts
# - Duration
# - RU consumed
```

---

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

### How to Contribute

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
git checkout -b feature/your-feature-name
```
3. **Make your changes**
4. **Test thoroughly**
5. **Commit with clear messages**
   ```bash
git commit -m "Add feature: description"
```
6. **Push to your fork**
   ```bash
git push origin feature/your-feature-name
```
7. **Open a Pull Request**

### Code Standards

- Follow existing code style
- Add XML documentation for public methods
- Include error handling
- Update README if adding features

### Reporting Issues

When reporting bugs, please include:
- Error message (full stack trace)
- Configuration (redact connection strings)
- Steps to reproduce
- Expected vs actual behavior

---

## 📄 License

MIT License

Copyright (c) 2024 Alberto Pola

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

**THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.**

---

## ⚠️ Important Disclaimers

### Data Loss Prevention

- **Always backup your data** before running any migration
- **Test in non-production environment** first
- **Use `DryRun: true`** to validate before actual migration
- **Verify results** by comparing document counts and sample data

### Cost Awareness

- Migrations consume Request Units (RUs) which incur costs
- Use the dry run feature to estimate RU consumption
- Monitor Azure costs during and after migration
- Consider running during off-peak hours to minimize impact

### No Warranty

This tool is provided as-is without any warranty. The authors and contributors are not responsible for:
- Data loss or corruption
- Migration failures
- Azure costs incurred
- Downtime or service interruption
- Any other damages resulting from use of this tool

**Use at your own risk and always verify results.**

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/albertopola/CosmosDbMigration/issues)
- **Discussions**: [GitHub Discussions](https://github.com/albertopola/CosmosDbMigration/discussions)
- **Documentation**: This README and code comments

---

## 🎉 Acknowledgments

- Built with [Azure Cosmos DB .NET SDK](https://github.com/Azure/azure-cosmos-dotnet-v3)
- Inspired by the need for seamless hierarchical partition key migrations
- Community feedback and contributions

---

## 📚 Additional Resources

- [Azure Cosmos DB Documentation](https://docs.microsoft.com/azure/cosmos-db/)
- [Hierarchical Partition Keys](https://docs.microsoft.com/azure/cosmos-db/hierarchical-partition-keys)
- [Cosmos DB Best Practices](https://docs.microsoft.com/azure/cosmos-db/best-practices)
- [Cosmos DB Pricing](https://azure.microsoft.com/pricing/details/cosmos-db/)

---

## 🗺️ Roadmap

Potential future enhancements:

- [ ] Resume interrupted migrations
- [ ] Parallel processing for faster migrations
- [ ] Support for more than 2-level hierarchical keys
- [ ] GUI/Web interface
- [ ] Azure Key Vault integration
- [ ] Transformation rules during migration
- [ ] Scheduling and automation support

Contributions welcome! 🚀

---

**⭐ If this tool helped you, please consider giving it a star on GitHub!**

---

*Last updated: 2024*