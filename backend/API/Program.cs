using API.Endpoints;
using API.Infrastructure;
using Infrastructure;
using Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks: "database" é o único verificado em /api/health/ready.
builder.Services
    .AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? AppDbContextFactory.FallbackConnectionString,
        name: "database",
        tags: ["database"]);

// DbContext (PostgreSQL) + repositórios.
builder.Services.AddInfrastructure(builder.Configuration);

// CORS restrito à URL do frontend do laboratório (configurável via "Frontend:BaseUrl").
const string frontendPolicy = "Frontend";
var frontendBaseUrl = builder.Configuration.GetValue<string>("Frontend:BaseUrl") ?? "http://localhost:5173";
builder.Services.AddCors(options =>
    options.AddPolicy(frontendPolicy, policy =>
        policy.WithOrigins(frontendBaseUrl).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Aplica migrations pendentes e popula o seed antes de atender requisições.
await app.Services.InitializeDatabaseAsync(app.Configuration);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(frontendPolicy);

app.MapHealthEndpoints();
app.MapControllers();

app.Run();
