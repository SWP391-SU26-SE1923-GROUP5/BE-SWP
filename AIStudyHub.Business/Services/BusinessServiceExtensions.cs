using AIStudyHub.Business.Options;
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.AI.Search;
using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.AI.Guardrails;
using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.AI.Tracking;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.Interfaces.AI.Tracking;
using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.Workers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIStudyHub.Business.Services;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessHostedServices(this IServiceCollection services)
    {
        services.AddHostedService<DocumentBackgroundProcessor>();
        services.AddHostedService<DocumentReindexWorker>();
        services.AddHostedService<TierExpiryWorker>();
        services.AddHostedService<UnverifiedAccountCleanupService>();
        services.AddHostedService<TierExpirationCleanupService>();
        services.AddHostedService<DailyStreakResetWorker>();
        services.AddHostedService<StreakWarningWorker>();
        services.AddHostedService<QuotaWarningWorker>();

        return services;
    }

    public static IServiceCollection AddBusinessServices(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(BusinessServiceExtensions).Assembly);
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IFlashcardService, FlashcardService>();
        services.AddScoped<IFlashcardReviewService, FlashcardReviewService>();
        services.AddScoped<IGamificationService, GamificationService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IAnswerService, AnswerService>();
        services.AddScoped<IQuizSubmissionService, QuizSubmissionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.Configure<OtpOptions>(configuration.GetSection("Otp"));
        services.Configure<DocumentReindexOptions>(configuration.GetSection(DocumentReindexOptions.SectionName));
        services.Configure<AIStudyHub.Business.Options.VnPayOptions>(configuration.GetSection(AIStudyHub.Business.Options.VnPayOptions.SectionName));
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITierMembershipService, TierMembershipService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IAIChatService, AIChatService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IVectorStoreService, QdrantVectorService>();
        services.AddScoped<IOpenAIService, OpenAIService>();
        services.AddScoped<IFlashcardAiService, FlashcardAiService>();
        services.AddScoped<IQuizAiService, QuizAiService>();
        services.AddScoped<ITokenTrackerService, TokenTrackerService>();
        services.AddScoped<ITokenWalletService, TokenWalletService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IDocumentReindexClaimService, DocumentReindexClaimService>();

        // Channel-based queue for background document processing
        services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();

        services.AddBusinessHostedServices();

        // L3: Search Services
        services.Configure<RetrievalOptions>(configuration.GetSection("Retrieval"));
        services.AddSingleton<ISparseVectorGenerator, Bm25SparseGenerator>();
        services.AddScoped<IHybridSearchService, HybridSearchService>();
        services.AddScoped<IRerankingService, RerankingService>();
        services.AddScoped<RagContextExpander>();
        services.AddScoped<RagRetrievalPipeline>();

        // L4: SK Orchestrator
        services.Configure<SemanticKernelOptions>(configuration.GetSection("SemanticKernel"));
        services.AddScoped<ISemanticKernelOrchestrator, SemanticKernelOrchestrator>();

        // L5: Guardrails
        services.Configure<GuardrailsOptions>(configuration.GetSection("Guardrails"));
        services.AddScoped<IFaithfulnessFilter, FaithfulnessFilter>();
        services.AddScoped<IGroundingVerifier, GroundingVerifier>();
        services.AddScoped<IConfidenceScorer, ConfidenceScorer>();

        return services;
    }
}
