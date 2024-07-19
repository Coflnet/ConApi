using System.Reflection;
using System.Text.Json.Serialization;
using Coflnet.Auth;
using Coflnet.Connections.Services;
using Coflnet.Core;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<PersonService>();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ConApi",
                    Version = "v1",
                    Description = ""
                });
                // baarer token 
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                });
                c.CustomOperationIds(apiDesc =>
                {
                    return (apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : "xy") + (apiDesc.HttpMethod == "OPTIONS" ? "Options" : "");
                });

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath, true);
            });
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

app.UseSwagger(c =>
{
    c.RouteTemplate = "api/openapi/{documentName}/openapi.json";
})
.UseSwaggerUI(c =>
{
    c.RoutePrefix = "api";
    c.SwaggerEndpoint("/api/openapi/v1/openapi.json", "Con");
    c.EnablePersistAuthorization();
});

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
