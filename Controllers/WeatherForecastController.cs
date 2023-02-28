using Coflnet.Connections;
using Microsoft.AspNetCore.Mvc;

namespace Connections.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;

    public SearchController(ILogger<SearchController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<SearchResult> Serach(string value)
    {
        return Enumerable.Range(1, 5).Select(index => new SearchResult
        {
            Name = "Name",
            Description = "Description",
            Image = "Image",
            Link = "Link"
        })
        .ToArray();
    }
}
