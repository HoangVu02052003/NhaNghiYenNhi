using Microsoft.AspNetCore.SignalR;

namespace NhaNghiYenNhi.Hubs
{
    public class OrderHub : Hub
    {
        public async Task JoinQuanLyPhongGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "QuanLyPhong");
            Console.WriteLine($"[OrderHub] Client {Context.ConnectionId} joined QuanLyPhong group");
        }

        public async Task LeaveQuanLyPhongGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "QuanLyPhong");
        }

        public async Task SendOrderNotification(int phongId, string tenSanPham, int soLuong, string tenPhong)
        {
            await Clients.Group("QuanLyPhong").SendAsync("ReceiveOrderNotification", phongId, tenSanPham, soLuong, tenPhong);
        }

        public async Task ConfirmOrder(int phongId, int sanPhamId, int soLuong, string tenPhong)
        {
            await Clients.Group("QuanLyPhong").SendAsync("OrderConfirmed", phongId, sanPhamId, soLuong, tenPhong);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "QuanLyPhong");
            await base.OnDisconnectedAsync(exception);
        }
    }
} 