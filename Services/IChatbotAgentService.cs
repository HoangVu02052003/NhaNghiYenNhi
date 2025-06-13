namespace NhaNghiYenNhi.Services
{
    public interface IChatbotAgentService
    {
        Task<string> ProcessUserMessageAsync(string userMessage);
    }

    public interface IActionService
    {
        Task<ActionResult> BookRoomAsync(int roomNumber, string customerName = "Khách vãng lai");
        Task<ActionResult> AddProductToRoomAsync(int roomNumber, string productName, int quantity = 1);
        Task<ActionResult> CheckoutRoomAsync(int roomNumber);
        Task<ActionResult> GetRoomStatusAsync(int? roomNumber = null);
        Task<ActionResult> CleanRoomAsync(int roomNumber);
        Task<ActionResult> FindProductAsync(string productName);
    }

    public class ActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }
} 