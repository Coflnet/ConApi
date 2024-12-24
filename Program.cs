using System.Text.Json.Serialization;
using Coflnet.Auth;
using Coflnet.Connections.Services;
using Coflnet.Core;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<PersonService>();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.AddCoflAuthService();
builder.Services.AddCoflnetCore();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCoflnetCore();
app.UseCors("AllowAll");
// log every request
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Path}");
    await next();
    var responseCode = context.Response.StatusCode;
    Console.WriteLine($"Response: {responseCode}");
});
app.UseCoflAuthService();
app.MapControllers();

var googleConfigpath = app.Configuration["GOOGLE_APPLICATION_CREDENTIALS"];
if (googleConfigpath == null)
    app.Logger.LogWarning("GOOGLE_APPLICATION_CREDENTIALS not set, not initializing Firebase");
else
{
    var credentials = await  GoogleCredential.FromFileAsync(googleConfigpath, default);
    FirebaseApp.Create(new AppOptions()
    {
        Credential = credentials
    });
}

app.Run();
