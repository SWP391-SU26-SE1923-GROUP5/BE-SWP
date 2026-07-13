namespace AIStudyHub.Business.Exceptions;

public class OtpExpiredException : Exception
{
    public OtpExpiredException()
        : base("OTP has expired. Please request a new one.")
    {
    }

    public OtpExpiredException(string message) : base(message)
    {
    }
}
