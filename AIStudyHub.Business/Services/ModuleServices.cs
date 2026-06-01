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
using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public sealed class UserService : CrudService<UserResponseDto, CreateUserRequestDto, UpdateUserRequestDto>, IUserService
{
}

public sealed class DocumentService : CrudService<DocumentResponseDto, CreateDocumentRequestDto, UpdateDocumentRequestDto>, IDocumentService
{
}

public sealed class VoteService : CrudService<VoteResponseDto, CreateVoteRequestDto, UpdateVoteRequestDto>, IVoteService
{
}

public sealed class ReportService : CrudService<ReportResponseDto, CreateReportRequestDto, UpdateReportRequestDto>, IReportService
{
}

public sealed class FlashcardService : CrudService<FlashcardResponseDto, CreateFlashcardRequestDto, UpdateFlashcardRequestDto>, IFlashcardService
{
}

public sealed class QuizService : CrudService<QuizResponseDto, CreateQuizRequestDto, UpdateQuizRequestDto>, IQuizService
{
}

public sealed class QuestionService : CrudService<QuestionResponseDto, CreateQuestionRequestDto, UpdateQuestionRequestDto>, IQuestionService
{
}

public sealed class AnswerService : CrudService<AnswerResponseDto, CreateAnswerRequestDto, UpdateAnswerRequestDto>, IAnswerService
{
}

public sealed class QuizSubmissionService : CrudService<QuizSubmissionResponseDto, CreateQuizSubmissionRequestDto, UpdateQuizSubmissionRequestDto>, IQuizSubmissionService
{
}

public sealed class NotificationService : CrudService<NotificationResponseDto, CreateNotificationRequestDto, UpdateNotificationRequestDto>, INotificationService
{
}

public sealed class PaymentService : CrudService<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>, IPaymentService
{
}
