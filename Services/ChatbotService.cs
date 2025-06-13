using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace NhaNghiYenNhi.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly MyDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _groqApiKey;
        private readonly string _groqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

        public ChatbotService(MyDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _groqApiKey = configuration["GroqApiKey"] ?? "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";
        }

        public async Task<string> ProcessUserMessageAsync(string userMessage)
        {
            try
            {
                // Phân tích intent của user và lấy dữ liệu cần thiết
                var contextData = await GatherContextDataAsync();
                
                // Tạo prompt cho AI với context data
                var systemPrompt = CreateSystemPrompt(contextData);
                
                // Gọi Groq API
                var response = await CallGroqApiAsync(systemPrompt, userMessage);
                
                return response;
            }
            catch (Exception ex)
            {
                return $"Xin lỗi, tôi gặp lỗi khi xử lý câu hỏi của bạn: {ex.Message}";
            }
        }

        private async Task<ContextData> GatherContextDataAsync()
        {
            var currentTime = DateTime.Now;
            
            // Lấy thông tin tất cả phòng
            var phongs = await _context.Phongs
                .Include(p => p.IdLoaiPhongMacDinhNavigation)
                .ToListAsync();

            // Lấy thông tin các loại phòng
            var loaiPhongs = await _context.LoaiPhongs.ToListAsync();

            // Lấy thông tin các sản phẩm
            var sanPhams = await _context.SanPhamNhaNghis.ToListAsync();

            // Lấy thông tin các phòng đang được thuê
            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.IdKhachHangNavigation)
                .Include(tp => tp.IdLoaiPhongNavigation)
                .Where(tp => !tp.TraPhongs.Any()) // Phòng chưa trả
                .ToListAsync();

            // Lấy thông tin chi tiết về phòng đang thuê với thời gian
            var phongDangThueDetails = new List<PhongDangThueInfo>();
            foreach (var thue in thuePhongs)
            {
                var phong = phongs.FirstOrDefault(p => p.Id == thue.IdPhong);
                if (phong != null && thue.ThoiGianVao.HasValue)
                {
                    var thoiGianThue = currentTime - thue.ThoiGianVao.Value;
                    
                    // Lấy thông tin sản phẩm đã mua
                    var sanPhamInfo = "";
                    if (!string.IsNullOrEmpty(thue.SanPhamDaMua))
                    {
                        try
                        {
                            var sanPhamDict = JsonConvert.DeserializeObject<Dictionary<int, int>>(thue.SanPhamDaMua);
                            if (sanPhamDict != null && sanPhamDict.Any())
                            {
                                                            var sanPhamIds = sanPhamDict.Keys.ToList();
                            var sanPhamsInfo = await _context.SanPhamNhaNghis
                                .Where(sp => sanPhamIds.Contains(sp.Id))
                                .ToListAsync();
                                
                                                            var sanPhamList = sanPhamsInfo.Select(sp => 
                                $"{sp.TenSanPham} (SL: {sanPhamDict.GetValueOrDefault(sp.Id, 0)})").ToList();
                                sanPhamInfo = string.Join(", ", sanPhamList);
                            }
                        }
                        catch
                        {
                            // Format cũ
                            if (thue.SanPhamDaMua != "0")
                            {
                                                            var oldIds = thue.SanPhamDaMua.Split('-').Where(s => int.TryParse(s, out _)).Select(int.Parse);
                            var sanPhamsOld = await _context.SanPhamNhaNghis
                                .Where(sp => oldIds.Contains(sp.Id))
                                .ToListAsync();
                            sanPhamInfo = string.Join(", ", sanPhamsOld.Select(sp => sp.TenSanPham));
                            }
                        }
                    }

                    phongDangThueDetails.Add(new PhongDangThueInfo
                    {
                        TenPhong = phong.TenPhong,
                        ThoiGianVao = thue.ThoiGianVao.Value,
                        ThoiGianThue = thoiGianThue,
                        KhachHang = thue.IdKhachHangNavigation?.HoTen ?? "Khách vãng lai",
                        LoaiPhong = thue.IdLoaiPhongNavigation?.TenLoai ?? "Không xác định",
                        SanPhamDaMua = sanPhamInfo
                    });
                }
            }

            // Thống kê theo ngày
            var thuePhongHomNay = await _context.ThuePhongs
                .Where(tp => tp.ThoiGianVao.HasValue && tp.ThoiGianVao.Value.Date == currentTime.Date)
                .CountAsync();

            var traPhongHomNay = await _context.TraPhongs
                .Where(tp => tp.ThoiGianTra.HasValue && tp.ThoiGianTra.Value.Date == currentTime.Date)
                .CountAsync();

            return new ContextData
            {
                TongSoPhong = phongs.Count,
                PhongTrong = phongs.Count(p => p.TrangThai == 0),
                PhongDangThue = phongs.Count(p => p.TrangThai == 1),
                PhongDangDonDep = phongs.Count(p => p.TrangThai == 3),
                PhongDangThueDetails = phongDangThueDetails,
                ThuePhongHomNay = thuePhongHomNay,
                TraPhongHomNay = traPhongHomNay,
                NgayHienTai = currentTime.ToString("dd/MM/yyyy"),
                GioHienTai = currentTime.ToString("HH:mm"),
                LoaiPhongs = loaiPhongs,
                SanPhams = sanPhams
            };
        }

        private string CreateSystemPrompt(ContextData context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là trợ lý AI cho QUẢN LÝ nhà nghỉ Yến Nhi. Người bạn đang nói chuyện là QUẢN LÝ của nhà nghỉ, không phải khách hàng. Hãy trả lời NGẮN GỌN, CHÍNH XÁC và CHUYÊN NGHIỆP như báo cáo cho sếp.");
            sb.AppendLine();
            sb.AppendLine("THÔNG TIN HIỆN TẠI:");
            sb.AppendLine($"- Ngày giờ: {context.NgayHienTai} {context.GioHienTai}");
            sb.AppendLine($"- Tổng số phòng: {context.TongSoPhong}");
            sb.AppendLine($"- Phòng trống: {context.PhongTrong}");
            sb.AppendLine($"- Phòng đang thuê: {context.PhongDangThue}");
            sb.AppendLine($"- Phòng đang dọn dẹp: {context.PhongDangDonDep}");
            sb.AppendLine($"- Số lượt thuê phòng hôm nay: {context.ThuePhongHomNay}");
            sb.AppendLine($"- Số lượt trả phòng hôm nay: {context.TraPhongHomNay}");
            sb.AppendLine();
            
            if (context.PhongDangThueDetails.Any())
            {
                sb.AppendLine("CHI TIẾT PHÒNG ĐANG THUÊ:");
                foreach (var phong in context.PhongDangThueDetails)
                {
                    sb.AppendLine($"- {phong.TenPhong}:");
                    sb.AppendLine($"  + Khách hàng: {phong.KhachHang}");
                    sb.AppendLine($"  + Loại phòng: {phong.LoaiPhong}");
                    sb.AppendLine($"  + Thời gian vào: {phong.ThoiGianVao:dd/MM/yyyy HH:mm}");
                    sb.AppendLine($"  + Đã thuê: {phong.ThoiGianThue.Days} ngày {phong.ThoiGianThue.Hours} giờ {phong.ThoiGianThue.Minutes} phút");
                    if (!string.IsNullOrEmpty(phong.SanPhamDaMua))
                    {
                        sb.AppendLine($"  + Sản phẩm đã mua: {phong.SanPhamDaMua}");
                    }
                    else
                    {
                        sb.AppendLine($"  + Chưa mua sản phẩm gì thêm");
                    }
                }
            }
            else
            {
                sb.AppendLine("Hiện tại không có phòng nào đang được thuê.");
            }

            // Thông tin loại phòng
            sb.AppendLine();
            sb.AppendLine("THÔNG TIN LOẠI PHÒNG CÓ SẴN:");
            foreach (var loaiPhong in context.LoaiPhongs)
            {
                sb.AppendLine($"- {loaiPhong.TenLoai}:");
                sb.AppendLine($"  + Giá giờ đầu: {loaiPhong.GioDau}");
                sb.AppendLine($"  + Giá giờ sau: {loaiPhong.GioSau}");
                sb.AppendLine($"  + Giá qua đêm: {loaiPhong.QuaDem}");
                if (!string.IsNullOrEmpty(loaiPhong.MoTa))
                {
                    sb.AppendLine($"  + Mô tả: {loaiPhong.MoTa}");
                }
            }

            // Thông tin sản phẩm
            sb.AppendLine();
            sb.AppendLine("SẢN PHẨM/DỊCH VỤ CÓ SẴN:");
            foreach (var sanPham in context.SanPhams)
            {
                var trangThai = sanPham.Con == true ? "Còn hàng" : "Hết hàng";
                sb.AppendLine($"- {sanPham.TenSanPham}: {sanPham.Gia:N0} VNĐ ({trangThai})");
            }
            
            sb.AppendLine();
            sb.AppendLine("PHONG CÁCH TRẢ LỜI:");
            sb.AppendLine("- Trả lời NGẮN GỌN, đi thẳng vào vấn đề");
            sb.AppendLine("- Sử dụng số liệu cụ thể, tránh dài dòng");
            sb.AppendLine("- Xưng hô: 'Cô Chủ' (với quản lý), 'Em' (tự xưng)");
            sb.AppendLine("- Chỉ cung cấp thông tin cần thiết, không giải thích quá chi tiết");
            sb.AppendLine("- Tập trung vào dữ liệu quan trọng cho việc quản lý");
            sb.AppendLine("- Khi không có thông tin: 'Em chưa có dữ liệu này trong hệ thống ạ'");
            
            return sb.ToString();
        }

        private async Task<string> CallGroqApiAsync(string systemPrompt, string userMessage)
        {
            var httpClient = _httpClientFactory.CreateClient();
            
            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.7,
                max_tokens = 1024
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_groqApiKey}");

            var response = await httpClient.PostAsync(_groqApiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Groq API error: {response.StatusCode} - {responseString}");
            }

            dynamic result = JsonConvert.DeserializeObject(responseString);
            return result.choices[0].message.content;
        }

        private class ContextData
        {
            public int TongSoPhong { get; set; }
            public int PhongTrong { get; set; }
            public int PhongDangThue { get; set; }
            public int PhongDangDonDep { get; set; }
            public List<PhongDangThueInfo> PhongDangThueDetails { get; set; } = new();
            public int ThuePhongHomNay { get; set; }
            public int TraPhongHomNay { get; set; }
            public string NgayHienTai { get; set; } = "";
            public string GioHienTai { get; set; } = "";
            public List<LoaiPhong> LoaiPhongs { get; set; } = new();
            public List<SanPhamNhaNghi> SanPhams { get; set; } = new();
        }

        private class PhongDangThueInfo
        {
            public string TenPhong { get; set; } = "";
            public DateTime ThoiGianVao { get; set; }
            public TimeSpan ThoiGianThue { get; set; }
            public string KhachHang { get; set; } = "";
            public string LoaiPhong { get; set; } = "";
            public string SanPhamDaMua { get; set; } = "";
        }
    }
}