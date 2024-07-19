using Coflnet.Auth;
using Coflnet.Connections;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public async Task<IEnumerable<SearchResult>> Serach(string value)
    {
        return await _searchService.Search(this.GetUserId(),value);
    }

    [HttpPost]
    [Authorize]
    public async Task AddEntry(SearchEntry entry)
    {
        entry.UserId = this.GetUserId();
        await _searchService.AddEntry(entry);
    }
    [HttpPost]
    [Route("mock")]
    [Authorize]
    public async Task AddMockEntries()
    {
        var userId = this.GetUserId();
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
