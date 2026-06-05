# Guideline triển khai dự án .NET 8 theo 3-layer architecture

Tài liệu này rút ra từ cấu trúc hiện tại của `AIStudyHub` và chuyển hóa thành guideline áp dụng cho dự án mới dùng đúng 3 project:

- `AIStudyHub.API`
- `AIStudyHub.Business`
- `AIStudyHub.Data`

Không tách thêm project `Domain`, `Application`, `Infrastructure`. Các pattern như Controllers, service layer, repository, EF Core, FluentValidation, AutoMapper, JWT, Swagger, CORS, rate limiting, SignalR, background service và storage service đều được map vào 3 layer trên.

## 1. Tổng quan 3-layer architecture

Luồng phụ thuộc khuyến nghị:

```text
Client
-> AIStudyHub.API
-> AIStudyHub.Business
-> AIStudyHub.Data
-> Database / external storage / external providers
```

### AIStudyHub.API

`AIStudyHub.API` là presentation layer. Layer này chịu trách nhiệm nhận HTTP request, áp dụng các middleware của ASP.NET Core, cấu hình bảo mật, rồi gọi Business layer.

Nên đặt ở API:

- `Program.cs`
- `Controllers`
- `Middleware`
- `Configuration`
- `Attributes`
- `Hubs` nếu cần realtime bằng SignalR
- `Services` chỉ cho các service thuần API như current user, realtime publisher, API-specific adapter
- Extension method như `AddWebApiServices`, `AddJwtAuthentication`, `AddSwaggerDocumentation`
- CORS, rate limiting, authentication, authorization, Swagger/OpenAPI

Không nên đặt ở API:

- Business rule
- EF Core query trực tiếp
- `DbContext` injection vào controller
- Trả trực tiếp entity ra response nếu không thật sự cần
- Logic hash password, tính toán nghiệp vụ, gọi repository trực tiếp từ controller

### AIStudyHub.Business

`AIStudyHub.Business` là business layer. Layer này xử lý use case chính của app, validate request, map DTO, điều phối repository và trả response model cho API.

Nên đặt ở Business:

- `Services`
- `Interfaces` cho service
- `DTOs`
- Request/response models
- `Validators`
- `Mappings`
- `Exceptions`
- Use case logic
- CQRS handler nếu sau này dùng MediatR

Business có thể gọi Data layer thông qua repository interface. Với đúng 3 project, repository interface có thể nằm trong `AIStudyHub.Data/Repositories` hoặc `AIStudyHub.Data/Interfaces` để tránh tạo thêm project contract riêng.

Không nên đặt ở Business:

- Controller hoặc HTTP-specific type như `HttpContext`, `IActionResult`
- Migration
- Fluent API entity configuration
- Connection string hoặc provider database cụ thể
- SQL query nằm lẫn trong service nếu repository đã đủ biểu đạt

### AIStudyHub.Data

`AIStudyHub.Data` là data access layer. Layer này sở hữu database model, EF Core DbContext, repository, migration và seed data.

Nên đặt ở Data:

- `Entities`
- `Enums`
- `ApplicationDbContext` hoặc `AppDbContext`
- `Configurations` dùng Fluent API
- `Repositories`
- Repository interface và implementation
- `Migrations`
- `SeedData`
- Data access services như storage adapter, token persistence, audit writer nếu gắn với persistence

Không nên đặt ở Data:

- Controller
- HTTP response
- Business workflow
- DTO response cho API
- Logic authorization theo endpoint

## 2. Framework/package nên dùng cho .NET 8

Các version dưới đây bám theo dự án mẫu đang target `net8.0`. Với package của Microsoft, nên pin cùng nhánh `8.0.x`.

### AIStudyHub.API

Package nên cài:

```bash
dotnet add AIStudyHub.API package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.27
dotnet add AIStudyHub.API package Swashbuckle.AspNetCore --version 6.6.2
dotnet add AIStudyHub.API package Microsoft.AspNetCore.OpenApi --version 8.0.27
dotnet add AIStudyHub.API package FluentValidation.DependencyInjectionExtensions --version 12.1.1
dotnet add AIStudyHub.API package Serilog.AspNetCore --version 10.0.0
dotnet add AIStudyHub.API package Serilog.Sinks.Console --version 6.1.1
dotnet add AIStudyHub.API package Serilog.Sinks.File --version 7.0.0
```

Ghi chú:

- Controllers, CORS, rate limiting và SignalR server đều có sẵn trong ASP.NET Core shared framework khi dùng `Microsoft.NET.Sdk.Web`.
- Rate limiting dùng `builder.Services.AddRateLimiter(...)`, thường không cần package riêng.
- SignalR dùng `builder.Services.AddSignalR()` và `app.MapHub<T>()`, thường không cần package riêng ở server.
- Có thể dùng Scalar thay Swagger UI nếu dự án muốn giao diện tài liệu API hiện đại hơn, nhưng dự án mẫu đang dùng `Swashbuckle.AspNetCore`.

### AIStudyHub.Business

Package nên cài:

```bash
dotnet add AIStudyHub.Business package AutoMapper --version 16.1.1
dotnet add AIStudyHub.Business package FluentValidation --version 12.1.1
```

Package chỉ dùng khi thật sự cần CQRS rõ ràng:

```bash
dotnet add AIStudyHub.Business package MediatR --version 12.4.1
```

Ghi chú:

- Nếu use case còn đơn giản, dùng service class bình thường như dự án mẫu là đủ.
- Chỉ thêm MediatR khi số lượng use case lớn, controller bị phình, hoặc cần pipeline behavior cho validation/logging/transaction.

### AIStudyHub.Data

Package nên cài cho SQL Server:

```bash
dotnet add AIStudyHub.Data package Microsoft.EntityFrameworkCore --version 8.0.27
dotnet add AIStudyHub.Data package Microsoft.EntityFrameworkCore.Design --version 8.0.27
dotnet add AIStudyHub.Data package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.27
```

Nếu dùng PostgreSQL thay SQL Server:

```bash
dotnet add AIStudyHub.Data package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11
```

Nếu dùng ASP.NET Core Identity:

```bash
dotnet add AIStudyHub.Data package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.27
```

Nếu tự quản lý password hash không dùng Identity:

```bash
dotnet add AIStudyHub.Data package BCrypt.Net-Next --version 4.0.3
```

Ghi chú:

- Dự án mẫu hiện có `User` và role enum tự quản lý, chưa dùng `IdentityDbContext`.
- Nếu dự án mới cần login/register/role nghiêm túc, nên cân nhắc ASP.NET Core Identity ngay từ đầu.
- Nếu không dùng Identity, cần tự triển khai `IPasswordHasher` hoặc dùng `BCrypt.Net-Next` trong Data/Business service phù hợp.

## 3. Cấu trúc folder đề xuất

```text
AIStudyHub.sln
|-- AIStudyHub.API
|   |-- Attributes
|   |-- Configuration
|   |-- Controllers
|   |-- Extensions
|   |   |-- ApiServiceExtensions.cs
|   |   |-- JwtExtensions.cs
|   |   `-- SwaggerExtensions.cs
|   |-- Hubs
|   |-- Middleware
|   |   `-- GlobalExceptionMiddleware.cs
|   |-- Services
|   |-- Program.cs
|   `-- appsettings.example.json
|
|-- AIStudyHub.Business
|   |-- DTOs
|   |   `-- Courses
|   |       |-- CourseRequestDtos.cs
|   |       `-- CourseResponseDtos.cs
|   |-- Exceptions
|   |-- Interfaces
|   |   `-- Services
|   |       `-- ICourseService.cs
|   |-- Mappings
|   |   `-- BusinessMappingProfile.cs
|   |-- Models
|   |-- Requests
|   |-- Services
|   |   |-- BusinessServiceExtensions.cs
|   |   `-- CourseService.cs
|   `-- Validators
|       `-- Courses
|           `-- CourseValidators.cs
|
`-- AIStudyHub.Data
    |-- Configurations
    |   `-- CourseConfiguration.cs
    |-- DbContext
    |   `-- AppDbContext.cs
    |-- Entities
    |   |-- BaseEntity.cs
    |   `-- Course.cs
    |-- Enums
    |-- Migrations
    |-- Repositories
    |   |-- GenericRepository.cs
    |   |-- IGenericRepository.cs
    |   |-- CourseRepository.cs
    |   |-- ICourseRepository.cs
    |   |-- IUnitOfWork.cs
    |   `-- UnitOfWork.cs
    |-- SeedData
    |   `-- SeedData.cs
    `-- DataServiceExtensions.cs
```

## 4. Cách tổ chức dependency

Project reference nên dùng:

```text
AIStudyHub.API -> AIStudyHub.Business
AIStudyHub.API -> AIStudyHub.Data
AIStudyHub.Business -> AIStudyHub.Data
AIStudyHub.Data -> không reference ngược lại API hoặc Business
```

Lý do:

- `API` cần gọi `AddBusinessServices()` và `AddDataServices()` trong `Program.cs`.
- `Business` cần dùng entity/repository contract từ `Data` vì entity nằm ở `Data` theo 3-layer hiện tại.
- `Data` là layer thấp nhất, không biết controller, DTO response hoặc business workflow.

Quy tắc bắt buộc:

- Controller chỉ inject service từ `AIStudyHub.Business`.
- Business service có thể inject repository từ `AIStudyHub.Data`.
- Controller không inject `AppDbContext`.
- Controller không inject repository nếu service layer đã tồn tại.
- `Data` không reference `API`.
- `Data` không gọi service trong `Business`.

## 5. Template code

Module mẫu bên dưới dùng `Courses`, phù hợp với hướng học tập của AIStudyHub nhưng vẫn đủ trung tính để thay bằng domain khác.

### AIStudyHub.API/Program.cs

```csharp
using AIStudyHub.API.Extensions;
using AIStudyHub.API.Middleware;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddWebApiServices(builder.Configuration);
builder.Services.AddBusinessServices();
builder.Services.AddDataServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("DefaultCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Nếu cần realtime:
// app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
```

### AIStudyHub.API/Extensions/ApiServiceExtensions.cs

```csharp
using AIStudyHub.Business.Mappings;
using AIStudyHub.Business.Validators.Courses;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

namespace AIStudyHub.API.Extensions;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AIStudyHub API",
                Version = "v1"
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [securityScheme] = Array.Empty<string>()
            });
        });

        services.AddCors(options =>
        {
            options.AddPolicy("DefaultCors", policy =>
            {
                policy
                    .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("Default", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 20;
            });
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSection = configuration.GetSection("Jwt");
                var secretKey = jwtSection["SecretKey"]
                    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
        services.AddAutoMapper(_ => { }, typeof(BusinessMappingProfile).Assembly);
        services.AddValidatorsFromAssemblyContaining<CreateCourseRequestDtoValidator>();

        // Nếu cần realtime:
        // services.AddSignalR();

        return services;
    }
}
```

### AIStudyHub.Business/Services/BusinessServiceExtensions.cs

```csharp
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudyHub.Business.Services;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<ICourseService, CourseService>();
        return services;
    }
}
```

### AIStudyHub.Data/DataServiceExtensions.cs

```csharp
using AIStudyHub.Data.DbContext;
using AIStudyHub.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudyHub.Data;

public static class DataServiceExtensions
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
```

### AIStudyHub.Data/Entities/BaseEntity.cs

```csharp
namespace AIStudyHub.Data.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

### AIStudyHub.Data/Entities/Course.cs

```csharp
namespace AIStudyHub.Data.Entities;

public sealed class Course : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
}
```

### AIStudyHub.Data/DbContext/AppDbContext.cs

```csharp
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Data.DbContext;

public sealed class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Course> Courses => Set<Course>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
```

### AIStudyHub.Data/Configurations/CourseConfiguration.cs

```csharp
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIStudyHub.Data.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");
        builder.HasKey(course => course.Id);
        builder.Property(course => course.Title).HasMaxLength(200).IsRequired();
        builder.Property(course => course.Description).HasMaxLength(2000);
        builder.Property(course => course.IsPublished).HasDefaultValue(false);
    }
}
```

### AIStudyHub.Data/Repositories/IGenericRepository.cs

```csharp
using AIStudyHub.Data.Entities;
using System.Linq.Expressions;

namespace AIStudyHub.Data.Repositories;

public interface IGenericRepository<TEntity>
    where TEntity : BaseEntity
{
    IQueryable<TEntity> Query();
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
```

### AIStudyHub.Data/Repositories/GenericRepository.cs

```csharp
using AIStudyHub.Data.DbContext;
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AIStudyHub.Data.Repositories;

public class GenericRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : BaseEntity
{
    protected readonly AppDbContext DbContext;
    protected readonly DbSet<TEntity> DbSet;

    public GenericRepository(AppDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<TEntity>();
    }

    public IQueryable<TEntity> Query()
    {
        return DbSet.AsQueryable();
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        DbSet.Remove(entity);
    }
}
```

### AIStudyHub.Data/Repositories/ICourseRepository.cs

```csharp
using AIStudyHub.Data.Entities;

namespace AIStudyHub.Data.Repositories;

public interface ICourseRepository : IGenericRepository<Course>
{
    Task<bool> TitleExistsAsync(string title, CancellationToken cancellationToken = default);
}
```

### AIStudyHub.Data/Repositories/CourseRepository.cs

```csharp
using AIStudyHub.Data.DbContext;
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Data.Repositories;

public sealed class CourseRepository : GenericRepository<Course>, ICourseRepository
{
    public CourseRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<bool> TitleExistsAsync(string title, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(course => course.Title == title, cancellationToken);
    }
}
```

### AIStudyHub.Data/Repositories/IUnitOfWork.cs

```csharp
namespace AIStudyHub.Data.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### AIStudyHub.Data/Repositories/UnitOfWork.cs

```csharp
using AIStudyHub.Data.DbContext;

namespace AIStudyHub.Data.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

### AIStudyHub.Business/DTOs/Courses/CourseDtos.cs

```csharp
namespace AIStudyHub.Business.DTOs.Courses;

public sealed record CourseResponseDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    DateTime CreatedAt);

public sealed record CreateCourseRequestDto(
    string Title,
    string? Description);

public sealed record UpdateCourseRequestDto(
    string Title,
    string? Description,
    bool IsPublished);
```

### AIStudyHub.Business/Interfaces/Services/ICourseService.cs

```csharp
using AIStudyHub.Business.DTOs.Courses;

namespace AIStudyHub.Business.Interfaces.Services;

public interface ICourseService
{
    Task<IReadOnlyList<CourseResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CourseResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequestDto request, CancellationToken cancellationToken = default);
    Task<CourseResponseDto> UpdateAsync(Guid id, UpdateCourseRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

### AIStudyHub.Business/Services/CourseService.cs

```csharp
using AIStudyHub.Business.DTOs.Courses;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Repositories;
using AutoMapper;
using FluentValidation;

namespace AIStudyHub.Business.Services;

public sealed class CourseService : ICourseService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateCourseRequestDto> _createValidator;
    private readonly IValidator<UpdateCourseRequestDto> _updateValidator;

    public CourseService(
        ICourseRepository courseRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<CreateCourseRequestDto> createValidator,
        IValidator<UpdateCourseRequestDto> updateValidator)
    {
        _courseRepository = courseRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<CourseResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _courseRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<CourseResponseDto>>(courses);
    }

    public async Task<CourseResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courseRepository.GetByIdAsync(id, cancellationToken);
        return course is null ? null : _mapper.Map<CourseResponseDto>(course);
    }

    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequestDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _courseRepository.TitleExistsAsync(request.Title, cancellationToken))
        {
            throw new InvalidOperationException("Course title already exists.");
        }

        var course = _mapper.Map<Course>(request);

        await _courseRepository.AddAsync(course, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CourseResponseDto>(course);
    }

    public async Task<CourseResponseDto> UpdateAsync(Guid id, UpdateCourseRequestDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var course = await _courseRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Course not found.");

        _mapper.Map(request, course);

        _courseRepository.Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CourseResponseDto>(course);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courseRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Course not found.");

        _courseRepository.Remove(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

Ghi chú: `IUnitOfWork` nằm trong `AIStudyHub.Data`, nên vẫn giữ đúng 3 project nhưng Business service không cần inject trực tiếp `AppDbContext`.

### AIStudyHub.Business/Validators/Courses/CourseValidators.cs

```csharp
using AIStudyHub.Business.DTOs.Courses;
using FluentValidation;

namespace AIStudyHub.Business.Validators.Courses;

public sealed class CreateCourseRequestDtoValidator : AbstractValidator<CreateCourseRequestDto>
{
    public CreateCourseRequestDtoValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(2000);
    }
}

public sealed class UpdateCourseRequestDtoValidator : AbstractValidator<UpdateCourseRequestDto>
{
    public UpdateCourseRequestDtoValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(2000);
    }
}
```

### AIStudyHub.Business/Mappings/BusinessMappingProfile.cs

```csharp
using AIStudyHub.Business.DTOs.Courses;
using AIStudyHub.Data.Entities;
using AutoMapper;

namespace AIStudyHub.Business.Mappings;

public sealed class BusinessMappingProfile : Profile
{
    public BusinessMappingProfile()
    {
        CreateMap<Course, CourseResponseDto>();
        CreateMap<CreateCourseRequestDto, Course>();
        CreateMap<UpdateCourseRequestDto, Course>();
    }
}
```

### AIStudyHub.API/Controllers/CourseController.cs

```csharp
using AIStudyHub.Business.DTOs.Courses;
using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CourseController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CourseController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CourseResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var courses = await _courseService.GetAllAsync(cancellationToken);
        return Ok(courses);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CourseResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var course = await _courseService.GetByIdAsync(id, cancellationToken);
        return course is null ? NotFound() : Ok(course);
    }

    [HttpPost]
    public async Task<ActionResult<CourseResponseDto>> Create(CreateCourseRequestDto request, CancellationToken cancellationToken)
    {
        var course = await _courseService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CourseResponseDto>> Update(
        Guid id,
        UpdateCourseRequestDto request,
        CancellationToken cancellationToken)
    {
        var course = await _courseService.UpdateAsync(id, request, cancellationToken);
        return Ok(course);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _courseService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

### AIStudyHub.API/Middleware/GlobalExceptionMiddleware.cs

```csharp
using FluentValidation;
using System.Net;
using System.Text.Json;

namespace AIStudyHub.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.BadRequest, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.NotFound, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorResponseAsync(context, HttpStatusCode.Conflict, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred.");
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
```

## 6. Quy tắc implement

### Controller

- Controller không chứa business logic.
- Controller chỉ nhận request, gọi service, trả response.
- Controller không inject `AppDbContext`.
- Controller không gọi repository trực tiếp nếu đã có service.
- Controller trả DTO, không trả entity trực tiếp.
- Controller nhận `CancellationToken` ở các action async.

### Business service

- Service xử lý nghiệp vụ, ownership check, rule check và orchestration.
- Service gọi repository/Data layer để đọc ghi dữ liệu.
- Service validate request bằng FluentValidation trước khi xử lý.
- Service map entity sang DTO bằng AutoMapper.
- Service không trả `IQueryable` ra ngoài.
- Service không phụ thuộc vào `HttpContext`, `ControllerBase`, `IActionResult`.

### Data repository

- Repository chỉ xử lý query/database.
- Repository làm việc với entity, không làm việc với API DTO.
- Repository không chứa business rule.
- Query đọc nên dùng `AsNoTracking()` nếu không cần update.
- Method async phải nhận `CancellationToken`.

### DbContext và Entity

- `DbContext` chỉ nằm ở `AIStudyHub.Data`.
- Entity nằm ở `AIStudyHub.Data/Entities`.
- Enum nằm ở `AIStudyHub.Data/Enums`.
- Fluent API configuration nằm ở `AIStudyHub.Data/Configurations`.
- Không dùng entity làm request/response trực tiếp ở API nếu không thật sự cần.
- Field audit như `CreatedAt`, `UpdatedAt` nên xử lý tập trung trong `SaveChangesAsync`.

### Validation

- Request DTO nào có input từ client thì nên có validator.
- Validator đặt theo module: `Validators/{Module}`.
- Tên validator: `{RequestDtoName}Validator`.
- Dùng `ValidateAndThrowAsync` trong service hoặc tích hợp validation pipeline nếu sau này dùng MediatR.

### Mapping

- Mapping profile nằm ở `AIStudyHub.Business/Mappings`.
- Tên profile: `BusinessMappingProfile` hoặc `{Module}MappingProfile`.
- Mapping chỉ chuyển đổi dữ liệu, không nhét business rule phức tạp vào profile.

### Exception

- Exception được xử lý tập trung bằng `GlobalExceptionMiddleware`.
- Controller không dùng `try/catch` rộng.
- Validation lỗi trả `400`.
- Không tìm thấy resource trả `404`.
- Trùng dữ liệu hoặc conflict nghiệp vụ trả `409`.
- Lỗi không dự đoán được trả `500` và không lộ stack trace.

### Dependency Injection

- API registration đặt ở `AIStudyHub.API/Extensions`.
- Business registration đặt ở `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`.
- Data registration đặt ở `AIStudyHub.Data/DataServiceExtensions.cs`.
- Service, repository, DbContext dùng lifetime `Scoped`.
- Không đăng ký singleton nếu dependency bên trong là scoped service.

## 7. Checklist áp dụng vào dự án mới

### Bước 1: Tạo solution và 3 project

```bash
dotnet new sln -n AIStudyHub
dotnet new webapi -n AIStudyHub.API -f net8.0
dotnet new classlib -n AIStudyHub.Business -f net8.0
dotnet new classlib -n AIStudyHub.Data -f net8.0
dotnet sln AIStudyHub.sln add AIStudyHub.API/AIStudyHub.API.csproj
dotnet sln AIStudyHub.sln add AIStudyHub.Business/AIStudyHub.Business.csproj
dotnet sln AIStudyHub.sln add AIStudyHub.Data/AIStudyHub.Data.csproj
```

### Bước 2: Add project reference

```bash
dotnet add AIStudyHub.API reference AIStudyHub.Business
dotnet add AIStudyHub.API reference AIStudyHub.Data
dotnet add AIStudyHub.Business reference AIStudyHub.Data
```

Không add reference từ `AIStudyHub.Data` ngược lại `AIStudyHub.API` hoặc `AIStudyHub.Business`.

### Bước 3: Cài package

API:

```bash
dotnet add AIStudyHub.API package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.27
dotnet add AIStudyHub.API package Microsoft.AspNetCore.OpenApi --version 8.0.27
dotnet add AIStudyHub.API package Swashbuckle.AspNetCore --version 6.6.2
dotnet add AIStudyHub.API package FluentValidation.DependencyInjectionExtensions --version 12.1.1
dotnet add AIStudyHub.API package Serilog.AspNetCore --version 10.0.0
dotnet add AIStudyHub.API package Serilog.Sinks.Console --version 6.1.1
dotnet add AIStudyHub.API package Serilog.Sinks.File --version 7.0.0
```

Business:

```bash
dotnet add AIStudyHub.Business package AutoMapper --version 16.1.1
dotnet add AIStudyHub.Business package FluentValidation --version 12.1.1
```

Data:

```bash
dotnet add AIStudyHub.Data package Microsoft.EntityFrameworkCore --version 8.0.27
dotnet add AIStudyHub.Data package Microsoft.EntityFrameworkCore.Design --version 8.0.27
dotnet add AIStudyHub.Data package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.27
```

Optional:

```bash
dotnet add AIStudyHub.Business package MediatR --version 12.4.1
dotnet add AIStudyHub.Data package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.27
dotnet add AIStudyHub.Data package BCrypt.Net-Next --version 4.0.3
```

### Bước 4: Tạo folder

```text
AIStudyHub.API/Controllers
AIStudyHub.API/Configuration
AIStudyHub.API/Middleware
AIStudyHub.API/Attributes
AIStudyHub.API/Hubs
AIStudyHub.API/Services
AIStudyHub.API/Extensions

AIStudyHub.Business/DTOs
AIStudyHub.Business/Services
AIStudyHub.Business/Interfaces
AIStudyHub.Business/Validators
AIStudyHub.Business/Mappings
AIStudyHub.Business/Exceptions
AIStudyHub.Business/Models
AIStudyHub.Business/Requests

AIStudyHub.Data/Entities
AIStudyHub.Data/Enums
AIStudyHub.Data/Repositories
AIStudyHub.Data/Configurations
AIStudyHub.Data/Migrations
AIStudyHub.Data/DbContext
AIStudyHub.Data/SeedData
```

### Bước 5: Implement theo thứ tự

Thứ tự nên làm:

1. Tạo entity và enum trong `AIStudyHub.Data`.
2. Tạo `AppDbContext`.
3. Tạo Fluent API configuration.
4. Tạo repository interface và implementation.
5. Tạo DTO request/response trong `AIStudyHub.Business`.
6. Tạo validator.
7. Tạo AutoMapper profile.
8. Tạo service interface và implementation.
9. Tạo controller trong `AIStudyHub.API`.
10. Register DI trong `ApiServiceExtensions`, `BusinessServiceExtensions`, `DataServiceExtensions`.
11. Cấu hình `Program.cs`.
12. Tạo migration và chạy database update.

### Bước 6: Cấu hình appsettings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AIStudyHub;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "AIStudyHub",
    "Audience": "AIStudyHub.Client",
    "SecretKey": "CHANGE_THIS_TO_A_SECURE_LOCAL_SECRET",
    "ExpirationMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173"
    ]
  },
  "AllowedHosts": "*"
}
```

Không commit secret thật. Dùng user secrets, environment variables hoặc secret store cho môi trường thật.

### Bước 7: Migration

Cài EF tool nếu máy chưa có:

```bash
dotnet tool install --global dotnet-ef
```

Tạo migration:

```bash
dotnet ef migrations add InitialCreate --project AIStudyHub.Data --startup-project AIStudyHub.API --output-dir Migrations
```

Apply database:

```bash
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

### Bước 8: Build và run

```bash
dotnet restore AIStudyHub.sln
dotnet build AIStudyHub.sln
dotnet run --project AIStudyHub.API
```

Chạy với URL cố định:

```bash
dotnet run --project AIStudyHub.API --urls http://localhost:5000
```

Swagger:

```text
http://localhost:5000/swagger
```

### Bước 9: Checklist trước khi mở rộng module

- Controller chỉ gọi Business service.
- Service không trả entity trực tiếp.
- Repository không trả DTO.
- `DbContext` chỉ nằm trong Data.
- Entity nằm trong Data.
- DTO nằm trong Business.
- Validator đã được register.
- AutoMapper profile đã được register.
- JWT config không thiếu `Issuer`, `Audience`, `SecretKey`.
- CORS chỉ mở đúng frontend origin cần dùng.
- Middleware exception đã được đặt trước authentication/authorization.
- Migration build được từ `AIStudyHub.Data` với startup project là `AIStudyHub.API`.
- `dotnet build` pass trước khi thêm module tiếp theo.
