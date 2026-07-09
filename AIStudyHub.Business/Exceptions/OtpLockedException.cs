namespace AIStudyHub.Business.Exceptions;

public class OtpLockedException : Exception
{
    public int LockoutMinutes { get; }

    public OtpLockedException(int lockoutMinutes)
        : base($"Too many failed attempts. Please wait {lockoutMinutes} minutes before trying again.")
    {
        LockoutMinutes = lockoutMinutes;
    }

    public OtpLockedException(string message, int lockoutMinutes = 0) : base(message)
    {
        LockoutMinutes = lockoutMinutes;
    }
}
