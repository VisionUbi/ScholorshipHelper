using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using ScholarshipAutoFill.Api.Configuration;
using ScholarshipAutoFill.Api.Data;
using ScholarshipAutoFill.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Scholarship AutoFill API v2",
        Version = "v1",
        Description = "Simple one-project backend for AI-assisted scholarship research. Chunk 1 includes PostgreSQL and profile APIs."
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddHttpClient<IContentAggregationService, ContentAggregationService>();
builder.Services.AddHttpClient<IOfficialSourceSearchService, OfficialSourceSearchService>();
builder.Services.AddHttpClient<ILlmAnalysisService, LlmAnalysisService>();
builder.Services.AddScoped<IProgramRecommendationService, ProgramRecommendationService>();
builder.Services.AddScoped<IAiResearchService, AiResearchService>();
builder.Services.AddScoped<IAiSearchService, AiSearchService>();
builder.Services.AddScoped<IAiSearchProvider, ManualDeepSeekProvider>();
builder.Services.AddScoped<IAiSearchProvider, MockAiSearchProvider>();
builder.Services.AddHttpClient<DeepSeekApiProvider>();
builder.Services.AddScoped<IAiSearchProvider>(sp => sp.GetRequiredService<DeepSeekApiProvider>());
builder.Services.AddHttpClient<OpenAiProvider>();
builder.Services.AddScoped<IAiSearchProvider>(sp => sp.GetRequiredService<OpenAiProvider>());
builder.Services.Configure<ChatPortalCredentialsOptions>(
    builder.Configuration.GetSection(ChatPortalCredentialsOptions.SectionName));
builder.Services.AddSingleton<IChatPortalCredentialsReader, ChatPortalCredentialsReader>();
builder.Services.AddSingleton<IUniversityResearchPromptBuilder, UniversityResearchPromptBuilder>();
builder.Services.AddSingleton<IChatPortalPlaywrightWorkflow, ChatPortalPlaywrightWorkflow>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Scholarship AutoFill API v2");
    options.DocumentTitle = "Scholarship AutoFill API v2";
    options.RoutePrefix = "swagger";
});

app.UseCors();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
