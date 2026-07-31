using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.QuizSubmissions;
using AIStudyHub.Business.DTOs.Recommendations;
using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.DTOs.Subjects;
using AIStudyHub.Business.DTOs.TierMemberships;
using AIStudyHub.Business.DTOs.TokenWallet;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Business.Services;
using AIStudyHub.Data.Entities;
using AutoMapper;

namespace AIStudyHub.Business.Mappings;

public sealed class ApplicationMappingProfile : Profile
{
    public ApplicationMappingProfile()
    {
        CreateMap<User, UserResponseDto>()
            .ConstructUsing(src => new UserResponseDto(
                src.Id,
                src.FullName,
                src.Email ?? string.Empty,
                src.DateOfBirth,
                src.CurrentStorageCapacity,
                src.CurrentAiTokenUsage,
                src.Status,
                src.Role,
                src.TierId,
                src.TierMembership != null ? src.TierMembership.TierName : "Unknown",
                src.TierMembership != null ? src.TierMembership.StorageLimitMb : 0,
                src.TierMembership != null ? src.TierMembership.AiTokens : 0,
                src.TierExpireAt,
                src.CreatedAt,
                src.UpdatedAt
            ));
        CreateMap<CreateUserRequestDto, User>()
            .ForMember(dest => dest.CurrentAiTokenUsage, opt => opt.MapFrom(src => src.CurrentAiTokenUsage));
        CreateMap<UpdateUserRequestDto, User>()
            .ForMember(dest => dest.CurrentAiTokenUsage, opt => opt.Ignore());

        CreateMap<Document, DocumentResponseDto>()
            .ConvertUsing(source => MapDocument(source));
        CreateMap<CreateDocumentRequestDto, Document>();
        CreateMap<UpdateDocumentRequestDto, Document>();

        CreateMap<Vote, VoteResponseDto>();
        CreateMap<CreateVoteRequestDto, Vote>();

        CreateMap<Report, ReportResponseDto>()
            .ForMember(d => d.UserFullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
            .ForMember(d => d.DocumentTitle, o => o.MapFrom(s => s.Document != null ? s.Document.Title : string.Empty))
            .ForMember(d => d.ResolvedByFullName,
                o => o.MapFrom(s => s.ResolvedByUser != null ? s.ResolvedByUser.FullName : null));
        CreateMap<CreateReportRequestDto, Report>();

        CreateMap<Flashcard, FlashcardResponseDto>();
        CreateMap<CreateFlashcardRequestDto, Flashcard>();
        CreateMap<UpdateFlashcardRequestDto, Flashcard>();

        CreateMap<Quiz, QuizResponseDto>();
        CreateMap<CreateQuizRequestDto, Quiz>();
        CreateMap<UpdateQuizRequestDto, Quiz>();

        CreateMap<Question, QuestionResponseDto>();
        CreateMap<CreateQuestionRequestDto, Question>();
        CreateMap<UpdateQuestionRequestDto, Question>();
        CreateMap<Answer, AnswerResponseDto>();

        CreateMap<QuizSubmission, QuizSubmissionResponseDto>()
            .ConstructUsing(src => new QuizSubmissionResponseDto(
                src.Id, src.UserId, src.QuizId,
                string.Empty,
                string.Empty,
                string.Empty,
                src.Score, src.MaxScore, src.TotalCorrect,
                src.DurationSeconds,
                src.MaxScore > 0 ? Math.Round((double)src.Score / src.MaxScore * 100, 1) : 0,
                src.GradedAt, src.SubmittedAt, src.CreatedAt, src.UpdatedAt));
        CreateMap<CreateQuizSubmissionRequestDto, QuizSubmission>();

        CreateMap<Notification, NotificationResponseDto>();

        CreateMap<Payment, PaymentResponseDto>();

        CreateMap<TierMembership, TierMembershipResponseDto>();
        CreateMap<CreateTierMembershipRequestDto, TierMembership>();
        CreateMap<UpdateTierMembershipRequestDto, TierMembership>();

        CreateMap<Subject, SubjectResponseDto>();
        CreateMap<CreateSubjectRequestDto, Subject>()
            .ForMember(destination => destination.OwnerUserId, option => option.Ignore())
            .ForMember(destination => destination.OwnerUser, option => option.Ignore());
        CreateMap<UpdateSubjectRequestDto, Subject>()
            .ForMember(destination => destination.OwnerUserId, option => option.Ignore())
            .ForMember(destination => destination.OwnerUser, option => option.Ignore());

        CreateMap<ChatSession, ChatSessionResponseDto>();
        CreateMap<CreateChatSessionRequestDto, ChatSession>();
        CreateMap<ChatMessage, ChatMessageResponseDto>();
        CreateMap<CreateChatMessageRequestDto, ChatMessage>()
            .ForMember(dest => dest.ChatSessionId, opt => opt.MapFrom(src => src.SessionId))
            .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Message))
            .ForMember(dest => dest.Sender, opt => opt.MapFrom(_ => "user"));
        CreateMap<ChatSessionDocument, ChatSessionDocumentResponseDto>()
            .ConstructUsing((source, _) => MapChatSessionDocument(source));

        CreateMap<TokenLedger, TokenWalletHistoryDto>()
            .ConstructUsing(src => new TokenWalletHistoryDto(
                src.Id, src.OperationType, src.Status.ToString(), src.EstimatedTokens,
                src.ActualTokens, src.FailureReason, src.CreatedAt));
        CreateMap<Recommendation, RecommendationResponseDto>();
    }

    private static DocumentResponseDto MapDocument(Document source)
    {
        var readiness = DocumentReadinessEvaluator.Evaluate(source);
        return new DocumentResponseDto(
            source.Id,
            source.UserId,
            source.SubjectId,
            source.Title,
            source.FileLink,
            source.FileName,
            source.FileExtension,
            source.FileType,
            source.FileSizeBytes,
            source.ShareStatus,
            source.Status,
            readiness.IsChatReady,
            readiness.Message,
            readiness.CanRetry,
            source.Votes != null ? source.Votes.Count : 0,
            source.LifecycleStatus,
            source.TrashedAt,
            source.CreatedAt,
            source.UpdatedAt);
    }

    private static ChatSessionDocumentResponseDto MapChatSessionDocument(ChatSessionDocument source)
    {
        var readiness = DocumentReadinessEvaluator.Evaluate(source.Document);
        return new ChatSessionDocumentResponseDto(
            source.ChatSessionId,
            source.DocumentId,
            source.Document.Title,
            source.Document.FileName,
            source.CreatedAt,
            readiness.Status,
            readiness.IsChatReady,
            readiness.Message,
            readiness.CanRetry);
    }
}
