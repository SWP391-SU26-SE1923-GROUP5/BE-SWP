using AIStudyHub.API.Extensions;
using AIStudyHub.API.Middleware;
using AIStudyHub.Business.Mappings;
using AIStudyHub.Business.Services;
using AIStudyHub.Business.Validators.Authentication;
using AIStudyHub.Data.Extensions;
using FluentValidation;
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
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessServices();
builder.Services.AddAutoMapper(_ => { }, typeof(ApplicationMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

var app = builder.Build();

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
