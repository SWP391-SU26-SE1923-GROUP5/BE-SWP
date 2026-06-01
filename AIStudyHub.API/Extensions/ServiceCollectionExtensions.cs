using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Services;

namespace AIStudyHub.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IFlashcardService, FlashcardService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IAnswerService, AnswerService>();
        services.AddScoped<IQuizSubmissionService, QuizSubmissionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAIChatService, AIChatService>();

        return services;
    }
}
