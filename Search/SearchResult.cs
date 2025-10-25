namespace Coflnet.Connections;
public class SearchResult
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Id { get; set; }
    public string? Link { get; set; }
    public string? Type { get; set; }
    public int Score { get; set; }
}

/// <summary>
/// Paginated search result
/// </summary>
public class SearchResultPage
{
    public List<SearchResult> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
