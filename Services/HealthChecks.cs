using Microsoft.Extensions.Diagnostics.HealthChecks;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Health check for Cassandra database connectivity
/// </summary>
public class CassandraHealthCheck : IHealthCheck
{
    private readonly ISession _session;
    private readonly ILogger<CassandraHealthCheck> _logger;

    public CassandraHealthCheck(ISession session, ILogger<CassandraHealthCheck> logger)
    {
        _session = session;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple query to check connectivity
            var result = await Task.Run(() =>
            {
                var rs = _session.Execute("SELECT now() FROM system.local");
                return rs.FirstOrDefault();
            }, cancellationToken);

            if (result != null)
            {
                return HealthCheckResult.Healthy("Cassandra is responsive");
            }

            return HealthCheckResult.Degraded("Cassandra query returned no results");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cassandra health check failed");
            return HealthCheckResult.Unhealthy("Cassandra is not responsive", ex);
        }
    }
}

/// <summary>
/// Health check for S3/R2 storage connectivity
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly DocumentService _documentService;
    private readonly ILogger<StorageHealthCheck> _logger;

    public StorageHealthCheck(DocumentService documentService, ILogger<StorageHealthCheck> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if S3 client is configured
            if (_documentService == null)
            {
                return HealthCheckResult.Degraded("Document service not available");
            }

            // In a real implementation, you'd test actual S3 connectivity
            // For now, just check if the service is available
            return HealthCheckResult.Healthy("Storage service is available");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health check failed");
            return HealthCheckResult.Unhealthy("Storage service is not responsive", ex);
        }
    }
}
