using Microsoft.EntityFrameworkCore;
using SmartStore.BLL.Interfaces;
using SmartStore.BLL.Services;
using SmartStore.DAL.Data;
using SmartStore.DAL.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var openAiApiKey = builder.Configuration["Groq:ApiKey"]!;

builder.Services.AddDbContext<SmartStoreContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<RecommendationRepository>();
builder.Services.AddScoped<AILogRepository>();
builder.Services.AddScoped<InventoryRepository>(_ => new InventoryRepository(connectionString));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IAILogService, AILogService>();
builder.Services.AddScoped<IInventoryService>(_ =>
    new InventoryService(new InventoryRepository(connectionString), openAiApiKey));

builder.Services.AddControllers()
    .AddJsonOptions(o => {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.MapControllers();
app.Run();
