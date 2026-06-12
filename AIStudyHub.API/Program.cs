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
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSingleton(builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("EmailVerification").Get<EmailVerificationOptions>() ?? new EmailVerificationOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Otp").Get<OtpOptions>() ?? new OtpOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Cleanup").Get<CleanupOptions>() ?? new CleanupOptions());
builder.Services.AddHostedService<UnverifiedAccountCleanupService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessServices();
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

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
