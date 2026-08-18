using System.Reflection;
using System.Text.Json.Serialization;
using Claims;
using Claims.Hosting;
using Claims.Infrastructure;
using Claims.Infrastructure.Auditing;
using Claims.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var persistence = builder.Configuration
                      .GetSection(PersistenceOptions.SectionName)
                      .Get<PersistenceOptions>()
                  ?? new PersistenceOptions();

// Development starts throwaway databases; hosted environments and integration tests
// supply their own connection strings through configuration.
await using var localContainers = persistence.UseTestContainers
    ? await LocalContainers.StartAsync()
    : null;

if (localContainers is not null)
{
    persistence.Mongo.ConnectionString = localContainers.MongoConnectionString;
    persistence.AuditDbConnectionString = localContainers.SqlConnectionString;
}

builder.Services
    .AddControllers()
    .AddJsonOptions(x =>
    {
        x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddClaimsCore();
builder.Services.AddInfrastructure(builder.Configuration, persistence);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services.AddHealthChecks().AddCheck<PersistenceHealthCheck>("persistence");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Insurance Claims API",
        Version = "v1",
        Description = "Maintains insurance covers and the claims made against them."
    });

    var xmlPath = Path.Combine(
        AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (persistence.ApplyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AuditContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program { }