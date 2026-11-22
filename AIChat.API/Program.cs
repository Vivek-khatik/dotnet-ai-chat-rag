using AIChat.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register AI service
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<QdrantService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ChunkingService>();

// Register HttpClient factory for Ollama communication.
builder.Services.AddHttpClient();

// Register local AI services.
builder.Services.AddScoped<OllamaEmbeddingService>();
builder.Services.AddScoped<OllamaAIService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "AIChat.API v1");
    });
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
