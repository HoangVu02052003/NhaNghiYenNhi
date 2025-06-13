using Microsoft.AspNetCore.Mvc;
using NhaNghiYenNhi.Services;

namespace NhaNghiYenNhi.Controllers
{
    public class ChatbotAgentController : Controller
    {
        private readonly IChatbotAgentService _chatbotAgentService;

        public ChatbotAgentController(IChatbotAgentService chatbotAgentService)
        {
            _chatbotAgentService = chatbotAgentService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult RealTime()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessage message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message.Message))
                {
                    return BadRequest("Message cannot be empty");
                }

                var response = await _chatbotAgentService.ProcessUserMessageAsync(message.Message);
                
                return Json(new { success = true, response = response });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    public class ChatMessage
    {
        public string Message { get; set; } = "";
    }
} 