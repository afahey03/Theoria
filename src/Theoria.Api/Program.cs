using Theoria.Engine;
using Theoria.Engine.Crawling;
using Theoria.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Register the shared search engine as a singleton ---
// Both ISearchEngine and IIndexer resolve to the same instance,
// ensuring the web API uses the exact same engine core as the desktop client.
var storagePath = Path.Combine(AppContext.BaseDirectory, "theoria-index");
var engine = SearchEngineFactory.Create(storagePath);

// Load any previously persisted index from disk
await engine.LoadIndexAsync();

builder.Services.AddSingleton<ISearchEngine>(engine);
builder.Services.AddSingleton<IIndexer>(engine);
builder.Services.AddSingleton(engine); // for direct access if needed

// --- Register the web crawler ---
builder.Services.AddSingleton<WebCrawler>(sp =>
    new WebCrawler(sp.GetRequiredService<IIndexer>()));

// --- Register live internet search pipeline ---
builder.Services.AddSingleton<WebSearchProvider>();
builder.Services.AddSingleton<LiveSearchOrchestrator>(sp =>
    new LiveSearchOrchestrator(
        sp.GetRequiredService<WebSearchProvider>(),
        sp.GetRequiredService<WebCrawler>()));

// --- CORS for the React frontend ---
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();
