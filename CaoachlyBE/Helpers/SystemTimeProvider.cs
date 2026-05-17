namespace CaoachlyBE.Helpers;

public class SystemTimeProvider : ITimeProvider
{
    public DateTime Now => UaTime.Now;
}
