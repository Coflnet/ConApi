using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Connections.DTOs;
using ISession = Cassandra.ISession;
using Amazon.S3;
using Amazon.S3.Model;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing documents and file storage
/// </summary>
public class DocumentService
{
    private readonly ISession _session;
    private readonly Table<Document> _documents;
    private readonly Table<DocumentLink> _documentLinks;
    private readonly Table<DocumentByEntity> _documentsByEntity;
    private readonly Table<StorageQuota> _storageQuotas;
    private readonly ILogger<DocumentService> _logger;
    private readonly IAmazonS3? _s3Client;
    private readonly string _bucketName;

    public DocumentService(ISession session, ILogger<DocumentService> logger, IConfiguration configuration)
    {
        _session = session;
        _logger = logger;
        _bucketName = configuration["S3:BucketName"] ?? "connections-documents";

        // S3/R2 client configuration (optional - can be null if not configured)
        var s3Endpoint = configuration["S3:Endpoint"];
        var accessKey = configuration["S3:AccessKey"];
        var secretKey = configuration["S3:SecretKey"];

        if (!string.IsNullOrEmpty(s3Endpoint) && !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            var config = new AmazonS3Config
            {
                ServiceURL = s3Endpoint,
                ForcePathStyle = true
            };
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
            _logger.LogInformation("S3 client initialized with endpoint {Endpoint}", s3Endpoint);
        }
        else
        {
            _logger.LogWarning("S3 configuration not complete - document storage will not be available");
        }

        var documentMapping = new MappingConfiguration()
            .Define(new Map<Document>()
                .PartitionKey(t => t.UserId, t => t.FileName)
                .ClusteringKey(t => t.Id)
                .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
                .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        var linkMapping = new MappingConfiguration()
            .Define(new Map<DocumentLink>()
                .PartitionKey(t => t.UserId)
                .ClusteringKey(t => t.EntityId)
                .ClusteringKey(t => t.DocumentId)
                .Column(t => t.EntityType, c => c.WithName("entity_type").WithDbType<int>()));

        var byEntityMapping = new MappingConfiguration()
            .Define(new Map<DocumentByEntity>()
                .PartitionKey(t => t.UserId)
                .ClusteringKey(t => t.EntityId)
                .ClusteringKey(t => t.DisplayOrder)
                .ClusteringKey(t => t.DocumentId)
                .Column(t => t.Type, c => c.WithName("type").WithDbType<int>()));

        var quotaMapping = new MappingConfiguration()
            .Define(new Map<StorageQuota>()
                .PartitionKey(t => t.UserId));

        _documents = new Table<Document>(session, documentMapping);
        _documentLinks = new Table<DocumentLink>(session, linkMapping);
        _documentsByEntity = new Table<DocumentByEntity>(session, byEntityMapping);
        _storageQuotas = new Table<StorageQuota>(session, quotaMapping);
    }

    /// <summary>
    /// Get presigned URL for upload
    /// </summary>
    public async Task<PresignedUrlDto?> GetUploadUrl(Guid userId, string fileName, string contentType, int expirationMinutes = 15)
    {
        if (_s3Client == null)
        {
            _logger.LogWarning("S3 client not configured");
            return null;
        }

        // Check quota
        var quota = await GetStorageQuota(userId);
        if (quota.UsedBytes >= quota.QuotaBytes)
        {
            _logger.LogWarning("User {UserId} has exceeded storage quota", userId);
            return null;
        }

        var storageKey = $"{userId}/{Guid.NewGuid()}/{fileName}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            ContentType = contentType
        };

        var url = await Task.FromResult(_s3Client.GetPreSignedURL(request));

        return new PresignedUrlDto
        {
            Url = url,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            FileName = storageKey
        };
    }

    /// <summary>
    /// Get presigned URL for download
    /// </summary>
    public async Task<PresignedUrlDto?> GetDownloadUrl(Guid userId, Guid documentId, int expirationMinutes = 60)
    {
        if (_s3Client == null)
        {
            _logger.LogWarning("S3 client not configured");
            return null;
        }

        var document = await GetDocumentById(userId, documentId);
        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            return null;
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = document.BucketName,
            Key = document.StorageKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
        };

        var url = await Task.FromResult(_s3Client.GetPreSignedURL(request));

        return new PresignedUrlDto
        {
            Url = url,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            FileName = document.FileName
        };
    }

    /// <summary>
    /// Create document metadata after successful upload
    /// </summary>
    public async Task<Document> CreateDocument(Guid userId, string fileName, string storageKey, 
        long fileSizeBytes, UploadDocumentDto dto)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = fileName,
            Title = dto.Title,
            Description = dto.Description,
            ContentType = "application/octet-stream", // Should be set from upload
            FileSizeBytes = fileSizeBytes,
            StorageKey = storageKey,
            BucketName = _bucketName,
            Type = dto.Type,
            DocumentDate = dto.DocumentDate,
            Tags = dto.Tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _documents.Insert(document).ExecuteAsync();

        // Update storage quota
        await UpdateQuota(userId, fileSizeBytes, 1);

        _logger.LogInformation("Created document {DocumentId} for user {UserId}", document.Id, userId);

        return document;
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    public async Task<Document?> GetDocumentById(Guid userId, Guid documentId)
    {
        var result = await _documents
            .Where(d => d.UserId == userId && d.Id == documentId)
            .ExecuteAsync();
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Link document to an entity
    /// </summary>
    public async Task<bool> LinkDocumentToEntity(Guid userId, LinkDocumentDto dto)
    {
        var link = new DocumentLink
        {
            UserId = userId,
            EntityId = dto.EntityId,
            DocumentId = dto.DocumentId,
            EntityType = dto.EntityType,
            Caption = dto.Caption,
            DisplayOrder = dto.DisplayOrder,
            LinkedAt = DateTime.UtcNow
        };

        await _documentLinks.Insert(link).ExecuteAsync();

        // Denormalized table for entity queries
        var document = await GetDocument(userId, dto.DocumentId);
        if (document != null)
        {
            var byEntity = new DocumentByEntity
            {
                UserId = userId,
                EntityId = dto.EntityId,
                DisplayOrder = dto.DisplayOrder,
                DocumentId = dto.DocumentId,
                FileName = document.FileName,
                Title = document.Title,
                ContentType = document.ContentType,
                FileSizeBytes = document.FileSizeBytes,
                Type = document.Type,
                CreatedAt = document.CreatedAt
            };

            await _documentsByEntity.Insert(byEntity).ExecuteAsync();
        }

        _logger.LogInformation("Linked document {DocumentId} to entity {EntityId}", dto.DocumentId, dto.EntityId);

        return true;
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    public async Task<Document?> GetDocument(Guid userId, Guid documentId)
    {
        var result = await _documents
            .Where(d => d.UserId == userId && d.Id == documentId)
            .ExecuteAsync();
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Get documents for an entity
    /// </summary>
    public async Task<IEnumerable<DocumentByEntity>> GetDocumentsByEntity(Guid userId, Guid entityId)
    {
        return await _documentsByEntity
            .Where(d => d.UserId == userId && d.EntityId == entityId)
            .ExecuteAsync();
    }

    /// <summary>
    /// Get storage quota for a user
    /// </summary>
    public async Task<StorageQuota> GetStorageQuota(Guid userId)
    {
        var result = await _storageQuotas
            .Where(q => q.UserId == userId)
            .ExecuteAsync();

        var quota = result.FirstOrDefault();
        if (quota == null)
        {
            // Default quota: 1GB
            quota = new StorageQuota
            {
                UserId = userId,
                UsedBytes = 0,
                QuotaBytes = 1024L * 1024 * 1024, // 1GB
                DocumentCount = 0,
                UpdatedAt = DateTime.UtcNow
            };
            await _storageQuotas.Insert(quota).ExecuteAsync();
        }

        return quota;
    }

    /// <summary>
    /// Update storage quota
    /// </summary>
    private async Task UpdateQuota(Guid userId, long bytesChange, int documentCountChange)
    {
        var quota = await GetStorageQuota(userId);
        quota.UsedBytes += bytesChange;
        quota.DocumentCount += documentCountChange;
        quota.UpdatedAt = DateTime.UtcNow;

        await _storageQuotas.Insert(quota).ExecuteAsync();
    }

    /// <summary>
    /// Delete document (soft delete - mark for deletion)
    /// </summary>
    public async Task<bool> DeleteDocument(Guid userId, Guid documentId)
    {
        var document = await GetDocument(userId, documentId);
        if (document == null)
        {
            return false;
        }

        // TODO: Actually delete from S3 (can be done async/batch)
        // For now, just remove metadata
        
        // Update quota
        await UpdateQuota(userId, -document.FileSizeBytes, -1);

        _logger.LogInformation("Deleted document {DocumentId} for user {UserId}", documentId, userId);

        return true;
    }

    /// <summary>
    /// Ensure document-related schema exists
    /// </summary>
    public void EnsureSchema()
    {
        _documents.CreateIfNotExists();
        _documentLinks.CreateIfNotExists();
        _documentsByEntity.CreateIfNotExists();
        _storageQuotas.CreateIfNotExists();

        TryEnsureLcs("document");
        TryEnsureLcs("document_link");
        TryEnsureLcs("document_by_entity");
        TryEnsureLcs("storage_quota");
    }

    private void TryEnsureLcs(string tableName)
    {
        try
        {
            var keyspace = _session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace) || _session == null)
            {
                _logger.LogDebug("Session has no keyspace; skipping LCS check for {Table}", tableName);
                return;
            }

            var select = new global::Cassandra.SimpleStatement(
                "SELECT compaction FROM system_schema.tables WHERE keyspace_name = ? AND table_name = ?",
                keyspace, tableName);

            global::Cassandra.Row? row = null;
            for (int i = 0; i < 6; i++)
            {
                var rs = _session.Execute(select);
                row = rs.FirstOrDefault();
                if (row != null) break;
                System.Threading.Thread.Sleep(500);
            }

            if (row == null)
            {
                _logger.LogDebug("Table {Table} not yet visible in system_schema; skipping LCS", tableName);
                return;
            }

            try
            {
                var compaction = row.GetValue<IDictionary<string, string>>("compaction");
                if (compaction != null && compaction.TryGetValue("class", out var cls) &&
                    !string.IsNullOrEmpty(cls) && cls.Contains("LeveledCompactionStrategy", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Table {Table} already uses compaction {Class}", tableName, cls);
                    return;
                }
            }
            catch { }

            var target = $"{keyspace}.{tableName}";
            var cql = $"ALTER TABLE {target} WITH compaction = {{'class':'LeveledCompactionStrategy'}}";
            _session.Execute(new global::Cassandra.SimpleStatement(cql));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to ensure LCS for table {Table}", tableName);
        }
    }
}
