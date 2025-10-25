namespace Coflnet.Connections.DTOs;

/// <summary>
/// Advanced search request with filtering and pagination
/// </summary>
public class AdvancedSearchRequest
{
    /// <summary>
    /// Search query text
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Filter by entity types
    /// </summary>
    public List<SearchEntry.ResultType>? EntityTypes { get; set; }

    /// <summary>
    /// Start date for filtering (if applicable)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for filtering (if applicable)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Page number (0-indexed)
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Results per page
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Maximum results to fetch before pagination
    /// </summary>
    public int? MaxResults { get; set; }
}
