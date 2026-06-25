using AIStudyHub.Business.Options;
using AIStudyHub.Business.Configuration;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.AI.Orchestration;
using AIStudyHub.Business.AI.Search;
using AIStudyHub.Business.AI.VectorStore;
using AIStudyHub.Business.AI.Guardrails;
using AIStudyHub.Business.AI.LLM;
using AIStudyHub.Business.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Guardrails;
using AIStudyHub.Business.Interfaces.AI.Search;
using AIStudyHub.Business.Interfaces.AI.VectorStore;
using AIStudyHub.Business.Interfaces.AI.Orchestration;
using AIStudyHub.Business.Interfaces.AI.LLM;
using AIStudyHub.Business.Interfaces.AI.Chat;
using AIStudyHub.Business.Interfaces.AI.Generators;
using AIStudyHub.Business.AI.Generators;
using AIStudyHub.Business.Workers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;


namespace AIStudyHub.Business.Services;

public static class BusinessServiceExtensions
{
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
        services.AddScoped<IVectorStoreService, QdrantVectorService>();
        services.AddScoped<ILocalAIService,LocalAIService>();
       // services.AddScoped<IOpenAIService, OpenAIService>();
        services.AddScoped<IFlashcardAiService, FlashcardAiService>();
        services.AddScoped<IQuizAiService, QuizAiService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Channel-based queue for background document processing
        services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();

        // Background processor for document queue
        services.AddHostedService<DocumentBackgroundProcessor>();

        // Kernel Memory
        services.Configure<KernelMemorySettings>(configuration.GetSection("KernelMemory"));
        
        services.AddSingleton<IKernelMemory>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KernelMemorySettings>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("KernelMemory");
            

            
            var openAiConfig = new OpenAIConfig
            {
                APIKey = settings.OpenAI.ApiKey,
                EmbeddingModel = settings.OpenAI.EmbeddingModel,
                TextModel = settings.OpenAI.TextModel
            };
            
            return new KernelMemoryBuilder()
                .WithOpenAITextEmbeddingGeneration(openAiConfig)
                .WithOpenAITextGeneration(openAiConfig)
                .WithQdrantMemoryDb(settings.Qdrant.Host, settings.Qdrant.VectorSize.ToString())
                .WithCustomTextPartitioningOptions(new Microsoft.KernelMemory.Configuration.TextPartitioningOptions
                {
                    MaxTokensPerParagraph = settings.Chunking.MaxTokensPerChunk,
                    OverlappingTokens = settings.Chunking.OverlapTokens
                })
                .Build<MemoryServerless>(new KernelMemoryBuilderBuildOptions
                {
                    AllowMixingVolatileAndPersistentData = true
                });
        });
        services.AddScoped<IKernelMemoryService, KernelMemoryService>();

        // L3: Search Services
        services.Configure<RetrievalOptions>(configuration.GetSection("Retrieval"));
        services.AddSingleton<ISparseVectorGenerator, Bm25SparseGenerator>();
        services.AddScoped<IHybridSearchService, HybridSearchService>();
        services.AddScoped<IRerankingService, RerankingService>();

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
