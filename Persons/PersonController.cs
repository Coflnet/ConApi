using Coflnet.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Services;

[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly ILogger<PersonController> _logger;
    private readonly PersonService _personService;
    private readonly SearchService _searchService;

    public PersonController(ILogger<PersonController> logger, PersonService personService, SearchService searchService)
    {
        _logger = logger;
        _personService = personService;
        _searchService = searchService;
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IEnumerable<PersonData>> GetPersonData(string id)
    {
        var parts = id.Split(";");
        if (parts.Length == 1)
        {
            return await _personService.GetPersonData(this.GetUserId(), parts[0]);
        }
        else if (parts.Length == 2)
        {
            return await _personService.GetPersonData(this.GetUserId(), parts[0], DateTime.Parse(parts[1]));
        }
        else if (parts.Length == 3)
        {
            return await _personService.GetPersonData(this.GetUserId(), parts[0], DateTime.Parse(parts[1]), parts[2]);
        }
        return new List<PersonData>();
    }

    [HttpPost]
    [Authorize]
    public async Task AddPersonData(PersonData data)
    {
        data.UserId = this.GetUserId();
        await _personService.AddPersonData(data);
        if (data.Key.ToLower() == "name")
        {
            var date = data.Birthday.ToString("yyyy-MM-dd");
            await _searchService.AddEntry(data.UserId, data.Value, $"{data.Name};{date};{data.BirthPlace}", SearchEntry.ResultType.Person);
        }
    }
}