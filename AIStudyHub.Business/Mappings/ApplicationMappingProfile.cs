using AIStudyHub.Business.DTOs.AIChat;
using AIStudyHub.Business.DTOs.Answers;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.DTOs.Flashcards;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.DTOs.Questions;
using AIStudyHub.Business.DTOs.Quizzes;
using AIStudyHub.Business.DTOs.QuizSubmissions;
using AIStudyHub.Business.DTOs.Reports;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.DTOs.Votes;
using AIStudyHub.Data.Entities;
using AutoMapper;

namespace AIStudyHub.Business.Mappings;

public sealed class ApplicationMappingProfile : Profile
{
    public ApplicationMappingProfile()
    {
        CreateMap<User, UserResponseDto>();
        CreateMap<CreateUserRequestDto, User>()
            .ForMember(dest => dest.CurrentAiTokenUsage, opt => opt.MapFrom(src => src.CurrentAiTokenUsage));
        CreateMap<UpdateUserRequestDto, User>()
            .ForMember(dest => dest.CurrentAiTokenUsage, opt => opt.MapFrom(src => src.CurrentAiTokenUsage));

        CreateMap<Document, DocumentResponseDto>();
        CreateMap<CreateDocumentRequestDto, Document>();
        CreateMap<UpdateDocumentRequestDto, Document>();

        CreateMap<Vote, VoteResponseDto>();
        CreateMap<CreateVoteRequestDto, Vote>();
        CreateMap<UpdateVoteRequestDto, Vote>();

        CreateMap<Report, ReportResponseDto>();
        CreateMap<CreateReportRequestDto, Report>();
        CreateMap<UpdateReportRequestDto, Report>();

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
        CreateMap<CreateAnswerRequestDto, Answer>();
        CreateMap<UpdateAnswerRequestDto, Answer>();

        CreateMap<QuizSubmission, QuizSubmissionResponseDto>();
        CreateMap<CreateQuizSubmissionRequestDto, QuizSubmission>();
        CreateMap<UpdateQuizSubmissionRequestDto, QuizSubmission>();

        CreateMap<Notification, NotificationResponseDto>();
        CreateMap<CreateNotificationRequestDto, Notification>();
        CreateMap<UpdateNotificationRequestDto, Notification>();

        CreateMap<Payment, PaymentResponseDto>();
        CreateMap<CreatePaymentRequestDto, Payment>()
            .ForMember(dest => dest.PaymentDate, opt => opt.MapFrom(src => src.PaymentDate ?? DateTime.UtcNow));
        CreateMap<UpdatePaymentRequestDto, Payment>();

        CreateMap<ChatSession, ChatSessionResponseDto>();
        CreateMap<CreateChatSessionRequestDto, ChatSession>();
        CreateMap<ChatMessage, ChatMessageResponseDto>();
        CreateMap<CreateChatMessageRequestDto, ChatMessage>()
            .ForMember(dest => dest.ChatSessionId, opt => opt.MapFrom(src => src.SessionId))
            .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Message))
            .ForMember(dest => dest.Sender, opt => opt.MapFrom(_ => "user"));
    }
}
