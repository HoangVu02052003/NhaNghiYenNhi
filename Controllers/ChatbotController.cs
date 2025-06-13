using Microsoft.AspNetCore.Mvc;
using NhaNghiYenNhi.Services;

namespace NhaNghiYenNhi.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost("api/chatbot/message")]
        public async Task<IActionResult> ProcessMessage([FromBody] ChatMessageRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Json(new { success = false, message = "Tin nhắn không được để trống" });
                }

                var response = await _chatbotService.ProcessUserMessageAsync(request.Message);
                
                return Json(new { 
                    success = true, 
                    response = response,
                    timestamp = DateTime.Now.ToString("HH:mm dd/MM/yyyy")
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Đã xảy ra lỗi khi xử lý tin nhắn: " + ex.Message 
                });
            }
        }
    }

    public class ChatMessageRequest
    {
        public string Message { get; set; } = "";
    }
} 