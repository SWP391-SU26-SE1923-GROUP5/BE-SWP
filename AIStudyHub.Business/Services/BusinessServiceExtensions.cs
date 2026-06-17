using AIStudyHub.Business.Options;
using AIStudyHub.Business.Behaviors;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudyHub.Business.Services;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(BusinessServiceExtensions).Assembly);
        });
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IFlashcardService, FlashcardService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IAnswerService, AnswerService>();
        services.AddScoped<IQuizSubmissionService, QuizSubmissionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.Configure<OtpOptions>(configuration.GetSection("Otp"));
        services.Configure<AIStudyHub.Business.Options.VnPayOptions>(configuration.GetSection(AIStudyHub.Business.Options.VnPayOptions.SectionName));
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITierMembershipService, TierMembershipService>();
        services.AddScoped<IAIChatService, AIChatService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IVectorStoreService, VectorStoreService>();
        services.AddScoped<ICitationService, CitationService>();
        services.AddScoped<ILocalAIService,LocalAIService>();
       // services.AddScoped<IOpenAIService, OpenAIService>();
        services.AddScoped<IRagChatService, RagChatService>();
        services.AddScoped<AIStudyHub.Business.Interfaces.Services.IFlashcardAiService, AIStudyHub.Business.Services.FlashcardAiService>();
        services.AddScoped<AIStudyHub.Business.Interfaces.Services.IQuizAiService, AIStudyHub.Business.Services.QuizAiService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
