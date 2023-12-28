using Coflnet.Connections;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Mvc;

namespace Connections.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly SearchService _searchService;

    public SearchController(ILogger<SearchController> logger, SearchService searchService)
    {
        _logger = logger;
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IEnumerable<SearchResult>> Serach(string userId, string value)
    {
        return await _searchService.Search(userId,value);
    }

    [HttpPost]
    public async Task AddEntry(SearchEntry entry)
    {
        await _searchService.AddEntry(entry);
    }
    [HttpPost]
    [Route("mock")]
    public async Task AddMockEntries(string userId)
    {
        await _searchService.AddEntry(new SearchEntry
        {
            UserId = userId,
            KeyWord = "Fritz",
            Type = SearchEntry.ResultType.Person,
            FullId = "Test",
            Text = "Test"
        });
        await _searchService.AddEntry(new SearchEntry
        {
            UserId = userId,
            KeyWord = "Fritz",
            Type = SearchEntry.ResultType.Person,
            FullId = "FritzMr",
            Text = "MrFritz"
        });
    }
}
