using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text.Json;

namespace NhaNghiYenNhi.Services
{
    public class ActionService : IActionService
    {
        private readonly MyDbContext _context;

        public ActionService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<ActionResult> BookRoomAsync(int roomNumber, string customerName = "Khách vãng lai")
        {
            try
            {
                // Tìm phòng theo số
                var phong = await _context.Phongs
                    .Include(p => p.IdLoaiPhongMacDinhNavigation)
                    .FirstOrDefaultAsync(p => p.TenPhong.Contains(roomNumber.ToString()));

                if (phong == null)
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Không tìm thấy phòng số {roomNumber}" 
                    };
                }

                if (phong.TrangThai != 0) // 0 = trống
                {
                    var trangThaiText = phong.TrangThai switch
                    {
                        1 => "đang được thuê",
                        3 => "đang dọn dẹp",
                        _ => "không khả dụng"
                    };
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Phòng {roomNumber} {trangThaiText}" 
                    };
                }

                // Tạo hoặc tìm khách hàng
                var khachHang = await _context.KhachHangs
                    .FirstOrDefaultAsync(kh => kh.HoTen == customerName);
                
                if (khachHang == null && customerName != "Khách vãng lai")
                {
                    khachHang = new KhachHang
                    {
                        HoTen = customerName,
                        GioiTinh = "Không xác định"
                    };
                    _context.KhachHangs.Add(khachHang);
                    await _context.SaveChangesAsync();
                }

                // Tạo đơn thuê phòng
                var thuePhong = new ThuePhong
                {
                    IdPhong = phong.Id,
                    IdKhachHang = khachHang?.Id,
                    IdLoaiPhong = phong.IdLoaiPhongMacDinh,
                    ThoiGianVao = DateTime.Now,
                    SanPhamDaMua = "0"
                };

                _context.ThuePhongs.Add(thuePhong);

                // Cập nhật trạng thái phòng
                phong.TrangThai = 1; // 1 = đang thuê
                
                await _context.SaveChangesAsync();

                return new ActionResult 
                { 
                    Success = true, 
                    Message = $"Đã đặt phòng {roomNumber} cho {customerName} lúc {DateTime.Now:HH:mm dd/MM/yyyy}",
                    Data = new { RoomId = phong.Id, BookingId = thuePhong.Id }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult 
                { 
                    Success = false, 
                    Message = $"Lỗi khi đặt phòng: {ex.Message}" 
                };
            }
        }

        public async Task<ActionResult> AddProductToRoomAsync(int roomNumber, string productName, int quantity = 1)
        {
            try
            {
                // Tìm phòng đang thuê
                var thuePhong = await _context.ThuePhongs
                    .Include(tp => tp.IdPhongNavigation)
                    .Where(tp => tp.IdPhongNavigation.TenPhong.Contains(roomNumber.ToString()) 
                                && !tp.TraPhongs.Any()) // Chưa trả phòng
                    .FirstOrDefaultAsync();

                if (thuePhong == null)
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Phòng {roomNumber} không đang được thuê" 
                    };
                }

                // Tìm sản phẩm
                var sanPham = await _context.SanPhamNhaNghis
                    .FirstOrDefaultAsync(sp => sp.TenSanPham.ToLower().Contains(productName.ToLower()));

                if (sanPham == null)
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Không tìm thấy sản phẩm '{productName}'" 
                    };
                }

                // Kiểm tra tình trạng hàng - chỉ cảnh báo nhưng vẫn cho phép đặt
                var trangThaiHang = sanPham.Con == true ? "có sẵn" : "có thể hết hàng";
                var canhBaoHetHang = sanPham.Con != true ? " (⚠️ Lưu ý: sản phẩm có thể hết hàng)" : "";

                // Cập nhật sản phẩm đã mua
                Dictionary<int, int> sanPhamDict;
                try
                {
                    if (string.IsNullOrEmpty(thuePhong.SanPhamDaMua) || thuePhong.SanPhamDaMua == "0")
                    {
                        sanPhamDict = new Dictionary<int, int>();
                    }
                    else
                    {
                        sanPhamDict = JsonSerializer.Deserialize<Dictionary<int, int>>(thuePhong.SanPhamDaMua) 
                                     ?? new Dictionary<int, int>();
                    }
                }
                catch
                {
                    sanPhamDict = new Dictionary<int, int>();
                }

                // Thêm sản phẩm
                if (sanPhamDict.ContainsKey(sanPham.Id))
                {
                    sanPhamDict[sanPham.Id] += quantity;
                }
                else
                {
                    sanPhamDict[sanPham.Id] = quantity;
                }

                thuePhong.SanPhamDaMua = JsonSerializer.Serialize(sanPhamDict);
                await _context.SaveChangesAsync();

                var tongTien = sanPhamDict[sanPham.Id] * (sanPham.Gia ?? 0);

                return new ActionResult 
                { 
                    Success = true, 
                    Message = $"Đã thêm {quantity} {sanPham.TenSanPham} vào phòng {roomNumber}. Tổng: {tongTien:N0} VNĐ{canhBaoHetHang}",
                    Data = new { ProductId = sanPham.Id, Quantity = sanPhamDict[sanPham.Id], Total = tongTien }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult 
                { 
                    Success = false, 
                    Message = $"Lỗi khi thêm sản phẩm: {ex.Message}" 
                };
            }
        }

        public async Task<ActionResult> CheckoutRoomAsync(int roomNumber)
        {
            try
            {
                var thuePhong = await _context.ThuePhongs
                    .Include(tp => tp.IdPhongNavigation)
                    .Include(tp => tp.IdLoaiPhongNavigation)
                    .Where(tp => tp.IdPhongNavigation.TenPhong.Contains(roomNumber.ToString()) 
                                && !tp.TraPhongs.Any())
                    .FirstOrDefaultAsync();

                if (thuePhong == null)
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Phòng {roomNumber} không đang được thuê" 
                    };
                }

                var thoiGianTra = DateTime.Now;
                var thoiGianThue = thoiGianTra - (thuePhong.ThoiGianVao ?? DateTime.Now);

                // Tính tiền phòng
                var gioThue = (int)Math.Ceiling(thoiGianThue.TotalHours);
                var tienPhong = 0;
                if (gioThue >= 12) // Qua đêm
                {
                    tienPhong = int.TryParse(thuePhong.IdLoaiPhongNavigation?.QuaDem, out var quaDem) ? quaDem : 0;
                }
                else if (gioThue <= 1)
                {
                    tienPhong = int.TryParse(thuePhong.IdLoaiPhongNavigation?.GioDau, out var gioDau) ? gioDau : 0;
                }
                else
                {
                    var gioDau = int.TryParse(thuePhong.IdLoaiPhongNavigation?.GioDau, out var gd) ? gd : 0;
                    var gioSau = int.TryParse(thuePhong.IdLoaiPhongNavigation?.GioSau, out var gs) ? gs : 0;
                    tienPhong = gioDau + (gioThue - 1) * gioSau;
                }

                // Tính tiền sản phẩm
                var tienSanPham = 0;
                if (!string.IsNullOrEmpty(thuePhong.SanPhamDaMua) && thuePhong.SanPhamDaMua != "0")
                {
                    try
                    {
                        var sanPhamDict = JsonSerializer.Deserialize<Dictionary<int, int>>(thuePhong.SanPhamDaMua);
                        if (sanPhamDict != null)
                        {
                            var sanPhamIds = sanPhamDict.Keys.ToList();
                            var sanPhams = await _context.SanPhamNhaNghis
                                .Where(sp => sanPhamIds.Contains(sp.Id))
                                .ToListAsync();
                            
                            tienSanPham = sanPhams.Sum(sp => (sp.Gia ?? 0) * sanPhamDict.GetValueOrDefault(sp.Id, 0));
                        }
                    }
                    catch { }
                }

                var tongTien = tienPhong + tienSanPham;

                // Tạo hóa đơn trả phòng
                var traPhong = new TraPhong
                {
                    IdThuePhong = thuePhong.Id,
                    ThoiGianTra = thoiGianTra,
                    GiaTien = tongTien.ToString(),
                    TongGioThue = gioThue.ToString()
                };

                _context.TraPhongs.Add(traPhong);

                // Cập nhật trạng thái phòng thành dọn dẹp
                thuePhong.IdPhongNavigation.TrangThai = 3;

                await _context.SaveChangesAsync();

                return new ActionResult 
                { 
                    Success = true, 
                    Message = $"Đã trả phòng {roomNumber}. Thời gian thuê: {gioThue}h. Tổng tiền: {tongTien:N0} VNĐ",
                    Data = new { 
                        CheckoutId = traPhong.Id, 
                        Hours = gioThue, 
                        RoomFee = tienPhong, 
                        ProductFee = tienSanPham, 
                        Total = tongTien 
                    }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult 
                { 
                    Success = false, 
                    Message = $"Lỗi khi trả phòng: {ex.Message}" 
                };
            }
        }

        public async Task<ActionResult> GetRoomStatusAsync(int? roomNumber = null)
        {
            try
            {
                if (roomNumber.HasValue)
                {
                    var phong = await _context.Phongs
                        .Include(p => p.IdLoaiPhongMacDinhNavigation)
                        .FirstOrDefaultAsync(p => p.TenPhong.Contains(roomNumber.Value.ToString()));

                    if (phong == null)
                    {
                        return new ActionResult 
                        { 
                            Success = false, 
                            Message = $"Không tìm thấy phòng số {roomNumber}" 
                        };
                    }

                    var trangThai = phong.TrangThai switch
                    {
                        0 => "Trống",
                        1 => "Đang thuê",
                        3 => "Đang dọn dẹp",
                        _ => "Không xác định"
                    };

                    return new ActionResult 
                    { 
                        Success = true, 
                        Message = $"Phòng {roomNumber}: {trangThai}",
                        Data = new { RoomNumber = roomNumber, Status = trangThai, StatusCode = phong.TrangThai }
                    };
                }
                else
                {
                    var phongs = await _context.Phongs.ToListAsync();
                    var thongKe = new
                    {
                        Trong = phongs.Count(p => p.TrangThai == 0),
                        DangThue = phongs.Count(p => p.TrangThai == 1),
                        DangDonDep = phongs.Count(p => p.TrangThai == 3)
                    };

                    return new ActionResult 
                    { 
                        Success = true, 
                        Message = $"Trống: {thongKe.Trong}, Đang thuê: {thongKe.DangThue}, Dọn dẹp: {thongKe.DangDonDep}",
                        Data = thongKe
                    };
                }
            }
            catch (Exception ex)
            {
                return new ActionResult 
                { 
                    Success = false, 
                    Message = $"Lỗi khi kiểm tra trạng thái: {ex.Message}" 
                };
            }
        }

        public async Task<ActionResult> CleanRoomAsync(int roomNumber)
        {
            try
            {
                var phong = await _context.Phongs
                    .FirstOrDefaultAsync(p => p.TenPhong.Contains(roomNumber.ToString()));

                if (phong == null)
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Không tìm thấy phòng số {roomNumber}" 
                    };
                }

                if (phong.TrangThai == 1)
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Phòng {roomNumber} đang được thuê, không thể dọn dẹp" 
                    };
                }

                phong.TrangThai = 0; // Trống, sẵn sàng
                await _context.SaveChangesAsync();

                return new ActionResult 
                { 
                    Success = true, 
                    Message = $"Đã hoàn thành dọn dẹp phòng {roomNumber}. Phòng sẵn sàng cho khách mới"
                };
            }
            catch (Exception ex)
            {
                return new ActionResult 
                { 
                    Success = false, 
                    Message = $"Lỗi khi dọn dẹp phòng: {ex.Message}" 
                };
            }
        }

        public async Task<ActionResult> FindProductAsync(string productName)
        {
            try
            {
                var sanPhams = await _context.SanPhamNhaNghis
                    .Where(sp => sp.TenSanPham.ToLower().Contains(productName.ToLower()))
                    .ToListAsync();

                if (!sanPhams.Any())
                {
                    return new ActionResult 
                    { 
                        Success = false, 
                        Message = $"Không tìm thấy sản phẩm nào chứa '{productName}'" 
                    };
                }

                var ketQua = sanPhams.Select(sp => new 
                {
                    Id = sp.Id,
                    Ten = sp.TenSanPham,
                    Gia = sp.Gia ?? 0,
                    TrangThai = sp.Con == true ? "Còn hàng" : "Hết hàng"
                }).ToList();

                var thongBao = string.Join(", ", ketQua.Select(k => $"{k.Ten} ({k.Gia:N0} VNĐ - {k.TrangThai})"));

                return new ActionResult 
                { 
                    Success = true, 
                    Message = $"Tìm thấy {sanPhams.Count} sản phẩm: {thongBao}",
                    Data = ketQua
                };
            }
            catch (Exception ex)
            {
                return new ActionResult 
                { 
                    Success = false, 
                    Message = $"Lỗi khi tìm sản phẩm: {ex.Message}" 
                };
            }
        }
    }
} 