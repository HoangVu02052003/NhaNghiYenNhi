namespace NhaNghiYenNhi.Services
{
    public interface IChatbotService
    {
        Task<string> ProcessUserMessageAsync(string userMessage);
    }
} 