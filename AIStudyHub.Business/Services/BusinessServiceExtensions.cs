using AIStudyHub.Business.Behaviors;
using AIStudyHub.Business.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudyHub.Business.Services;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(BusinessServiceExtensions).Assembly);
        });
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<IAuthService, AuthService>();
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
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITierMembershipService, TierMembershipService>();
        services.AddScoped<ITierUserService, TierUserService>();
        services.AddScoped<IAIChatService, AIChatService>();

        return services;
    }
}
