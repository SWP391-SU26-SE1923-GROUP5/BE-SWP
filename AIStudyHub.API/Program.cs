using AIStudyHub.API.Extensions;
using AIStudyHub.API.Middleware;
using AIStudyHub.Business.Mappings;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using AIStudyHub.Business.Validators.Authentication;
using AIStudyHub.Data.Extensions;
using CloudinaryDotNet;
using FluentValidation;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddAppRateLimiting();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpOptions>>().Value);

builder.Services.Configure<EmailVerificationOptions>(builder.Configuration.GetSection("EmailVerification"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailVerificationOptions>>().Value);

builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OtpOptions>>().Value);

builder.Services.Configure<CleanupOptions>(builder.Configuration.GetSection("Cleanup"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CleanupOptions>>().Value);

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RagOptions>>().Value);

builder.Services.Configure<DocumentStorageOptions>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocumentStorageOptions>>().Value);

builder.Services.AddHostedService<UnverifiedAccountCleanupService>();
builder.Services.AddHostedService<TierExpirationCleanupService>();

builder.Services.AddHttpClient("EmbeddingClient");
builder.Services.AddHttpClient("VectorStoreClient");
builder.Services.AddHttpClient("LlmClient");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessServices(builder.Configuration);
builder.Services.AddAutoMapper(_ => { }, typeof(ApplicationMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration.GetSection("Cloudinary");

    var account = new Account(
        config["User"],
        config["ApiKey"],
        config["ApiSecret"]);

    var cloudinary = new Cloudinary(account);
    cloudinary.Api.Secure = true;

    return cloudinary;
});

var app = builder.Build();

await app.Services.SeedConfiguredAdminAsync(app.Configuration);

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
    RequestPath = "/uploads"
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
