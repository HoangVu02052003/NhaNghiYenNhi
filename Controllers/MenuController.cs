using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NhaNghiYenNhi.Hubs;
using NhaNghiYenNhi.Models;
using Microsoft.EntityFrameworkCore;
using System.Speech.Synthesis;
using System.Net;
using System.Net.Sockets;

namespace NhaNghiYenNhi.Controllers
{
    public class MenuController : Controller
    {
        private readonly MyDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly SpeechSynthesizer _synthesizer;

        public MenuController(MyDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            
            // Cấu hình TTS - tắt vì sẽ dùng Web Audio API thay thế
            try
            {
                _synthesizer.Rate = -2; // Tốc độ chậm hơn
                _synthesizer.Volume = 80; // Âm lượng vừa phải
            }
            catch
            {
                // Ignore errors
            }
        }

        public IActionResult Index(int? phongId)
        {
            if (!phongId.HasValue)
                return RedirectToAction("SelectRoom");

            var phong = _context.Phongs.Find(phongId.Value);
            if (phong == null)
                return NotFound("Phòng không tồn tại");

            ViewBag.PhongId = phongId.Value;
            ViewBag.TenPhong = phong.TenPhong;
            
            var sanPham = _context.SanPhamNhaNghis.ToList();
            return View(sanPham);
        }

        public IActionResult SelectRoom()
        {
            var phongs = _context.Phongs.ToList();
            return View(phongs);
        }

        [HttpPost]
        [Route("Menu/DatMon")]
        public async Task<IActionResult> DatMon([FromBody] DatMonRequest request)
        {
            var sanPham = await _context.SanPhamNhaNghis.FindAsync(request.SanPhamId);
            
            if (sanPham == null)
                return NotFound();

            // Lấy thông tin phòng
            var phong = await _context.Phongs
                .FirstOrDefaultAsync(p => p.Id == request.PhongId);

            if (phong == null)
                return NotFound("Phòng không tồn tại");

            string thongBao = $"Có yêu cầu đặt món từ phòng {phong.TenPhong}: {request.SoLuong} {sanPham.TenSanPham}";
            
            // Debug log
            Console.WriteLine($"[MenuController] Sending order notification: {thongBao}");
            
            // Gửi thông báo chỉ đến group QuanLyPhong
            await _hubContext.Clients.Group("QuanLyPhong").SendAsync("ReceiveOrderNotification", request.PhongId, sanPham.TenSanPham, request.SoLuong, phong.TenPhong, sanPham.Gia, sanPham.Id, thongBao);
            
            // Cũng gửi đến tất cả clients để debug
            await _hubContext.Clients.All.SendAsync("ReceiveOrderNotification", request.PhongId, sanPham.TenSanPham, request.SoLuong, phong.TenPhong, sanPham.Gia, sanPham.Id, thongBao);
            
            return Ok(new { success = true });
        }

        [HttpPost]
        [Route("Menu/XacNhanDatMon")]
        public async Task<IActionResult> XacNhanDatMon([FromBody] XacNhanDatMonRequest request)
        {
            try
            {
                // Tìm ThuePhong hiện tại của phòng
                var thuePhong = await _context.ThuePhongs
                    .Where(tp => tp.IdPhong == request.PhongId)
                    .OrderByDescending(tp => tp.ThoiGianVao)
                    .FirstOrDefaultAsync(tp => !_context.TraPhongs.Any(tr => tr.IdThuePhong == tp.Id));

                if (thuePhong == null)
                    return BadRequest(new { success = false, message = "Phòng chưa được thuê hoặc đã trả phòng" });

                var phong = await _context.Phongs.FindAsync(request.PhongId);
                var sanPham = await _context.SanPhamNhaNghis.FindAsync(request.SanPhamId);

                if (phong == null)
                    return BadRequest(new { success = false, message = "Phòng không tồn tại" });

                if (sanPham == null)
                    return BadRequest(new { success = false, message = "Sản phẩm không tồn tại" });

                // Thêm sản phẩm vào ThuePhong
                var sanPhamDaMua = new Dictionary<int, int>();
                
                // Xử lý JSON một cách an toàn
                if (!string.IsNullOrEmpty(thuePhong.SanPhamDaMua) && thuePhong.SanPhamDaMua.Trim() != "")
                {
                    try
                    {
                        sanPhamDaMua = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(thuePhong.SanPhamDaMua);
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"[XacNhanDatMon] JSON parse error: {ex.Message}");
                        Console.WriteLine($"[XacNhanDatMon] Invalid JSON: {thuePhong.SanPhamDaMua}");
                        // Nếu JSON không hợp lệ, khởi tạo dictionary mới
                        sanPhamDaMua = new Dictionary<int, int>();
                    }
                }

                if (sanPhamDaMua.ContainsKey(request.SanPhamId))
                {
                    sanPhamDaMua[request.SanPhamId] += request.SoLuong;
                }
                else
                {
                    sanPhamDaMua[request.SanPhamId] = request.SoLuong;
                }

                thuePhong.SanPhamDaMua = System.Text.Json.JsonSerializer.Serialize(sanPhamDaMua);
                await _context.SaveChangesAsync();

                string thongBao = $"Đã xác nhận đơn hàng từ phòng {phong.TenPhong}";
                Console.WriteLine($"[XacNhanDatMon] Success: {thongBao}");

                await _hubContext.Clients.Group("QuanLyPhong").SendAsync("OrderConfirmed", request.PhongId, request.SanPhamId, request.SoLuong, phong.TenPhong);
                
                return Ok(new { success = true, message = thongBao });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[XacNhanDatMon] Error: {ex.Message}");
                Console.WriteLine($"[XacNhanDatMon] StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xác nhận đơn hàng", error = ex.Message });
            }
        }

        [HttpGet]
        [Route("Menu/CheckVoices")]
        public IActionResult CheckVoices()
        {
            try
            {
                var voices = _synthesizer.GetInstalledVoices()
                    .Select(voice => new {
                        Name = voice.VoiceInfo.Name,
                        Culture = voice.VoiceInfo.Culture.Name,
                        Gender = voice.VoiceInfo.Gender.ToString(),
                        Age = voice.VoiceInfo.Age.ToString(),
                        Description = voice.VoiceInfo.Description
                    }).ToList();

                return Json(voices);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        public IActionResult QRCode(int phongId)
        {
            var phong = _context.Phongs.Find(phongId);
            if (phong == null)
                return NotFound("Phòng không tồn tại");

            ViewBag.PhongId = phongId;
            ViewBag.TenPhong = phong.TenPhong;
            
            // Lấy địa chỉ IP thay vì localhost
            string hostUrl = GetLocalIPAddress();
            ViewBag.MenuUrl = $"http://{hostUrl}:5157/Menu?phongId={phongId}";
            ViewBag.ServerIP = hostUrl;
            
            return View();
        }

        private string GetLocalIPAddress()
        {
            try
            {
                // Phương pháp 1: Kết nối thử để lấy IP local
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null)
                        return endPoint.Address.ToString();
                }
            }
            catch { }

            try
            {
                // Phương pháp 2: Lấy từ hostname
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            // Fallback
            return "192.168.1.100"; // IP mặc định, có thể thay đổi theo mạng của bạn
        }
    }

    public class DatMonRequest
    {
        public int PhongId { get; set; }
        public int SanPhamId { get; set; }
        public int SoLuong { get; set; }
    }

    public class XacNhanDatMonRequest
    {
        public int PhongId { get; set; }
        public int SanPhamId { get; set; }
        public int SoLuong { get; set; }
    }
} 