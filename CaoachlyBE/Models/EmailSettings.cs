namespace CaoachlyBE.Models;

public class EmailSettings
{
    public string SenderName { get; set; } = null!;
    public string SenderEmail { get; set; } = null!;
    public string AppPassword { get; set; } = null!;
    public string BusinessName { get; set; } = null!;
    public string SmtpHost { get; set; } = null!;
    public int SmtpPort { get; set; }
    public string FrontendBaseUrl { get; set; } = null!;
}
