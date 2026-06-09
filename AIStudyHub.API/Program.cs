using AIStudyHub.API.Extensions;
using AIStudyHub.API.Middleware;
using AIStudyHub.Business.Mappings;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using AIStudyHub.Business.Validators.Authentication;
using AIStudyHub.Data.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSingleton(builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("EmailVerification").Get<EmailVerificationOptions>() ?? new EmailVerificationOptions());
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredUniqueChars = 6;
    options.SignIn.RequireConfirmedEmail = true;
});
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessServices();
builder.Services.AddAutoMapper(_ => { }, typeof(ApplicationMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

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
