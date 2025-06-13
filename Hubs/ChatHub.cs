using Microsoft.AspNetCore.SignalR;
using NhaNghiYenNhi.Services;

namespace NhaNghiYenNhi.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IChatbotAgentService _chatbotAgentService;

        public ChatHub(IChatbotAgentService chatbotAgentService)
        {
            _chatbotAgentService = chatbotAgentService;
        }

        /// <summary>
        /// Xử lý tin nhắn từ client và trả về response real-time
        /// </summary>
        public async Task SendMessage(string message)
        {
            var connectionId = Context.ConnectionId;

            try
            {
                // Thông báo cho client rằng AI đang xử lý
                await Clients.Caller.SendAsync("TypingIndicator", true);

                // Xử lý tin nhắn bằng AI Agent
                var response = await _chatbotAgentService.ProcessUserMessageAsync(message);

                // Tắt typing indicator
                await Clients.Caller.SendAsync("TypingIndicator", false);

                // Gửi phản hồi về client
                await Clients.Caller.SendAsync("ReceiveMessage", new
                {
                    success = true,
                    response = response,
                    type = "success",
                    timestamp = DateTime.Now
                });

                // Log cho debug
                Console.WriteLine($"🔄 [SignalR] User: {message} | AI: {response}");
            }
            catch (Exception ex)
            {
                // Tắt typing indicator nếu có lỗi
                await Clients.Caller.SendAsync("TypingIndicator", false);

                // Gửi thông báo lỗi
                await Clients.Caller.SendAsync("ReceiveMessage", new
                {
                    success = false,
                    response = $"❌ Xin lỗi, có lỗi xảy ra: {ex.Message}",
                    type = "error",
                    timestamp = DateTime.Now
                });

                Console.WriteLine($"❌ [SignalR Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo khi client kết nối
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"🔗 [SignalR] Client connected: {Context.ConnectionId}");
            
            // Chào mừng client mới
            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                success = true,
                response = "🎉 Kết nối real-time thành công! Bây giờ bạn có thể trò chuyện với AI Agent trong thời gian thực.",
                type = "success",
                timestamp = DateTime.Now
            });

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Thông báo khi client ngắt kết nối
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"🔌 [SignalR] Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join room theo phòng (nếu cần phân chia theo phòng)
        /// </summary>
        public async Task JoinRoom(string roomName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
            Console.WriteLine($"🏠 [SignalR] {Context.ConnectionId} joined room: {roomName}");
        }

        /// <summary>
        /// Leave room
        /// </summary>
        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            Console.WriteLine($"🚪 [SignalR] {Context.ConnectionId} left room: {roomName}");
        }

        /// <summary>
        /// Broadcast tin nhắn cho tất cả clients (nếu cần)
        /// </summary>
        public async Task BroadcastMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", new
            {
                success = true,
                response = $"📢 Thông báo: {message}",
                type = "broadcast",
                timestamp = DateTime.Now
            });
        }
    }
} 