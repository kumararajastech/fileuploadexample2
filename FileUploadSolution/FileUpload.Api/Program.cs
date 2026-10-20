using FileUpload.Api.Middleware;
using FileUpload.Api.Options;
using FileUpload.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Options & Options Pattern Startup Validation
builder.Services.AddOptions<FileStorageOptions>()
    .Bind(builder.Configuration.GetSection(FileStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
    {
        if (string.IsNullOrWhiteSpace(options.SharedFolderPath)) return false;
        if (options.AllowedExtensions == null || options.AllowedExtensions.Length == 0) return false;
        return options.AllowedExtensions.All(ext => ext.StartsWith("."));
    }, "All extensions in AllowedExtensions must start with a dot (e.g. '.pdf').")
    .ValidateOnStart();

// 2. Configure Multipart Body Length Limits (Request Body Size)
var maxFileSizeMb = builder.Configuration.GetValue<int>("FileStorage:MaxFileSizeInMB", 20);
long maxSizeBytes = (long)maxFileSizeMb * 1024 * 1024;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxSizeBytes;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = maxSizeBytes;
});

// 3. Register Application Services (Scoped lifetime suits per-request handling)
builder.Services.AddScoped<IFileStorageService, SharedFolderStorageService>();

// 4. Add Controllers & OpenAPI/Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = ".NET 10 File Upload API",
        Version = "v1",
        Description = "ASP.NET Core Web API for uploading files securely to network shared folders (UNC) or local storage."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 5. Register Global Exception Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// 6. Configure Swagger UI
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", ".NET 10 File Upload API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
