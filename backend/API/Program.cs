using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext (PostgreSQL) + repositórios.
builder.Services.AddInfrastructure(builder.Configuration);

// CORS restrito à URL do frontend do laboratório (configurável via "Frontend:BaseUrl").
const string frontendPolicy = "Frontend";
var frontendBaseUrl = builder.Configuration.GetValue<string>("Frontend:BaseUrl") ?? "http://localhost:5173";
builder.Services.AddCors(options =>
    options.AddPolicy(frontendPolicy, policy =>
        policy.WithOrigins(frontendBaseUrl).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(frontendPolicy);
app.MapControllers();

app.Run();
