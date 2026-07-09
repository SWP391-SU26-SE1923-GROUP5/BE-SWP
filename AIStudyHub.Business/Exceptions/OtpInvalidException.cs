namespace AIStudyHub.Business.Exceptions;

public class OtpInvalidException : Exception
{
    public OtpInvalidException()
        : base("Invalid OTP.")
    {
    }

    public OtpInvalidException(string message) : base(message)
    {
    }
}
