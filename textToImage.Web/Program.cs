using Microsoft.Extensions.AI;
using textToImage.Web.Components;
using textToImage.Web.Services;
using textToImage.Web.Services.Ingestion;
using OpenAI;
using textToImage.Web.Services.Images;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Add controllers for the image API
builder.Services.AddControllers();

var openai = builder.AddAzureOpenAIClient("openai");
openai.AddChatClient("gpt-4o-mini")
    .UseFunctionInvocation()
    .UseOpenTelemetry(configure: c =>
        c.EnableSensitiveData = builder.Environment.IsDevelopment());
openai.AddEmbeddingGenerator("text-embedding-3-small");
openai.AddTextToImageClient("gpt-image-1");

builder.AddAzureSearchClient("azureAISearch");
builder.Services.AddAzureAISearchCollection<IngestedChunk>("data-texttoimage-chunks");
builder.Services.AddAzureAISearchCollection<IngestedDocument>("data-texttoimage-documents");
builder.Services.AddScoped<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();

// Register image cache services
builder.Services.AddSingleton<IImageCacheService, ImageCacheService>();
builder.Services.AddHostedService<ImageCacheBackgroundService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();

// Map controllers for the image API
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// By default, we ingest PDF files from the /wwwroot/Data directory. You can ingest from
// other sources by implementing IIngestionSource.
// Important: ensure that any content you ingest is trusted, as it may be reflected back
// to users or could be a source of prompt injection risk.
await DataIngestor.IngestDataAsync(
    app.Services,
    new PDFDirectorySource(Path.Combine(builder.Environment.WebRootPath, "Data")));

app.Run();
