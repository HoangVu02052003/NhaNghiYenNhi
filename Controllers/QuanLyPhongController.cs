using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text.Json;

namespace NhaNghiYenNhi.Controllers;

public class QuanLyPhongController : Controller
{
    private readonly MyDbContext _context;

    public QuanLyPhongController(MyDbContext context)
    {
        _context = context;
    }

    [HttpGet("api/getphong")]
    public async Task<IActionResult> GetPhong()
    {
        var phongs = await _context.Phongs
            .Include(p => p.IdLoaiPhongMacDinhNavigation)
            .Select(p => new
            {
                p.Id,
                p.TenPhong,
                p.TrangThai,
                p.IdLoaiPhongMacDinh,
                LoaiPhongMacDinh = p.IdLoaiPhongMacDinhNavigation
            })
            .ToListAsync();
        return Json(phongs);
    }

    [HttpGet("api/getallloaiphong")]
    public async Task<IActionResult> GetAllLoaiPhong()
    {
        var loaiPhongs = await _context.LoaiPhongs
            .Select(lp => new
            {
                lp.Id,
                lp.TenLoai,
                lp.GioDau,
                lp.GioSau,
                lp.QuaDem,
                lp.MoTa
            })
            .ToListAsync();
        return Json(loaiPhongs);
    }

    [HttpGet("api/getloaiphong/{id}")]
    public async Task<IActionResult> GetLoaiPhong(int id)
    {
        var loaiPhong = await _context.LoaiPhongs.FindAsync(id);
        return Json(loaiPhong);
    }

    [HttpGet("api/getthongtinthue/{idPhong}")]
    public async Task<IActionResult> GetThongTinThue(int idPhong)
    {
        try
        {
            var thuePhong = await _context.ThuePhongs
                .Include(tp => tp.IdLoaiPhongNavigation)
                .Include(tp => tp.IdKhachHangNavigation)
                .Where(tp => tp.IdPhong == idPhong && !tp.TraPhongs.Any())
                .FirstOrDefaultAsync();

            if (thuePhong == null)
            {
                return NotFound(new { message = "Không tìm thấy thông tin thuê phòng" });
            }

            if (thuePhong.ThoiGianVao == null)
                return BadRequest(new { message = "Thời gian vào không hợp lệ" });

            var thoiGianHienTai = DateTime.Now;
            var loaiPhong = thuePhong.IdLoaiPhongNavigation;
            
            if (loaiPhong == null)
                return BadRequest(new { message = "Không tìm thấy thông tin loại phòng" });

            var thongTinGia = TinhGiaPhong(thuePhong.ThoiGianVao.Value, thoiGianHienTai, loaiPhong);

            // Tính tiền sản phẩm
            decimal tongTienSanPham = 0;
            Dictionary<int, int> sanPhamDaMua = new Dictionary<int, int>();
            if (!string.IsNullOrEmpty(thuePhong.SanPhamDaMua))
            {
                try
                {
                    sanPhamDaMua = JsonSerializer.Deserialize<Dictionary<int, int>>(thuePhong.SanPhamDaMua);
                    var sanPhamIds = sanPhamDaMua.Keys.ToList();
                    var sanPhams = await _context.SanPhamNhaNghis.Where(sp => sanPhamIds.Contains(sp.Id)).ToListAsync();
                    
                    foreach (var sp in sanPhams)
                    {
                        if (sanPhamDaMua.TryGetValue(sp.Id, out int soLuong))
                        {
                            tongTienSanPham += (sp.Gia ?? 0) * soLuong;
                        }
                    }
                }
                catch
                {
                    // Nếu là format cũ
                    var oldIds = thuePhong.SanPhamDaMua.Split('-').Select(int.Parse);
                    var sanPhams = await _context.SanPhamNhaNghis.Where(sp => oldIds.Contains(sp.Id)).ToListAsync();
                    foreach (var sp in sanPhams)
                    {
                        tongTienSanPham += sp.Gia ?? 0;
                    }
                }
            }

            // Cập nhật tổng tiền bao gồm cả sản phẩm
            thongTinGia.TongTien += tongTienSanPham;

            var khachHang = thuePhong.IdKhachHangNavigation;
            var result = new
            {
                Id = thuePhong.Id,
                ThoiGianVao = thuePhong.ThoiGianVao,
                KhachHang = khachHang == null ? new
                {
                    HoTen = "Không có",
                    Cccd = "Không có",
                    GioiTinh = "Không có",
                    NgaySinh = "Không có"
                } : new
                {
                    HoTen = khachHang.HoTen ?? "Không có",
                    Cccd = khachHang.Cccd ?? "Không có",
                    GioiTinh = khachHang.GioiTinh ?? "Không có",
                    NgaySinh = khachHang.NgaySinh ?? "Không có"
                },
                LoaiPhong = new
                {
                    thuePhong.IdLoaiPhongNavigation.Id,
                    thuePhong.IdLoaiPhongNavigation.TenLoai,
                    thuePhong.IdLoaiPhongNavigation.GioDau,
                    thuePhong.IdLoaiPhongNavigation.GioSau,
                    thuePhong.IdLoaiPhongNavigation.QuaDem
                },
                ThoiGianHienTai = thoiGianHienTai,
                ThongTinGia = new
                {
                    thongTinGia.SoGio,
                    thongTinGia.GiaGioDau,
                    thongTinGia.GiaGioSau,
                    TienPhong = thongTinGia.TongTien - tongTienSanPham,
                    TienSanPham = tongTienSanPham,
                    TongTien = thongTinGia.TongTien,
                    thongTinGia.IsQuaDem,
                    thongTinGia.GiaQuaDem
                },
                SanPhamDaMua = sanPhamDaMua
            };

            return Json(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    [HttpPost("api/thuephong")]
    public async Task<IActionResult> ThuePhong([FromBody] ThuePhongRequest request)
    {
        try
        {
            // Validate loại phòng
            var loaiPhong = await _context.LoaiPhongs.FindAsync(request.IdLoaiPhong);
            if (loaiPhong == null)
                return Json(new { success = false, message = "Loại phòng không hợp lệ" });

            // Tạo khách hàng mới nếu có thông tin
            KhachHang? khachHang = null;
            if (!string.IsNullOrWhiteSpace(request.HoTen) || !string.IsNullOrWhiteSpace(request.Cccd))
            {
                khachHang = new KhachHang
                {
                    HoTen = request.HoTen ?? "Khách vãng lai",
                    Cccd = request.Cccd ?? "Không có",
                    GioiTinh = request.GioiTinh ?? "Không xác định",
                    NgaySinh = request.NgaySinh ?? "Không có"
                };
                _context.KhachHangs.Add(khachHang);
                await _context.SaveChangesAsync();
            }

            // Tạo thuê phòng
            var thuePhong = new ThuePhong
            {
                IdPhong = request.IdPhong,
                IdLoaiPhong = request.IdLoaiPhong,
                IdKhachHang = khachHang?.Id,
                ThoiGianVao = DateTime.Now
            };
            _context.ThuePhongs.Add(thuePhong);

            // Cập nhật trạng thái phòng
            var phong = await _context.Phongs.FindAsync(request.IdPhong);
            if (phong != null)
            {
                phong.TrangThai = 1; // Đánh dấu phòng đang được thuê
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, thuePhongId = thuePhong.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("api/traphong")]
    public async Task<IActionResult> TraPhong([FromBody] TraPhongRequest request)
    {
        try
        {
            var thuePhong = await _context.ThuePhongs
                .Include(tp => tp.IdLoaiPhongNavigation)
                .FirstOrDefaultAsync(tp => tp.Id == request.ThuePhongId);

            if (thuePhong == null)
                return Json(new { success = false, message = "Không tìm thấy thông tin thuê phòng" });

            if (thuePhong.ThoiGianVao == null)
                return Json(new { success = false, message = "Thời gian vào không hợp lệ" });

            var thoiGianTra = DateTime.Now;
            var loaiPhong = thuePhong.IdLoaiPhongNavigation;
            
            if (loaiPhong == null)
                return Json(new { success = false, message = "Không tìm thấy thông tin loại phòng" });

            var thongTinGia = TinhGiaPhong(thuePhong.ThoiGianVao.Value, thoiGianTra, loaiPhong);

            var traPhong = new TraPhong
            {
                IdThuePhong = thuePhong.Id,
                ThoiGianTra = thoiGianTra,
                TongGioThue = thongTinGia.SoGio.ToString("F1"),
                GiaTien = thongTinGia.TongTien.ToString()
            };
            _context.TraPhongs.Add(traPhong);

            // Cập nhật trạng thái phòng thành đang dọn dẹp
            var phong = await _context.Phongs.FindAsync(thuePhong.IdPhong);
            if (phong != null)
            {
                phong.TrangThai = 3; // Đánh dấu phòng đang dọn dẹp
            }

            await _context.SaveChangesAsync();
            return Json(new { 
                success = true, 
                giaTien = thongTinGia.TongTien,
                thoiGianThue = thongTinGia.SoGio,
                chiTietTinhGia = new {
                    soGioLamTron = thongTinGia.SoGio,
                    giaGioDau = thongTinGia.GiaGioDau,
                    giaGioSau = thongTinGia.GiaGioSau,
                    tongTien = thongTinGia.TongTien,
                    isQuaDem = thongTinGia.IsQuaDem,
                    giaQuaDem = thongTinGia.GiaQuaDem
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("api/capnhatthongtinthue")]
    public async Task<IActionResult> CapNhatThongTinThue([FromBody] CapNhatThuePhongRequest request)
    {
        try
        {
            var thuePhong = await _context.ThuePhongs
                .Include(tp => tp.IdKhachHangNavigation)
                .FirstOrDefaultAsync(tp => tp.Id == request.ThuePhongId);

            if (thuePhong == null)
                return Json(new { success = false, message = "Không tìm thấy thông tin thuê phòng" });

            // Cập nhật thông tin khách hàng
            if (thuePhong.IdKhachHangNavigation == null)
            {
                // Tạo mới khách hàng nếu chưa có
                var khachHang = new KhachHang
                {
                    HoTen = request.HoTen ?? "Khách vãng lai",
                    Cccd = request.Cccd ?? "Không có",
                    GioiTinh = request.GioiTinh ?? "Không xác định",
                    NgaySinh = request.NgaySinh ?? "Không có"
                };
                _context.KhachHangs.Add(khachHang);
                await _context.SaveChangesAsync();
                thuePhong.IdKhachHang = khachHang.Id;
            }
            else
            {
                // Cập nhật thông tin khách hàng hiện có
                thuePhong.IdKhachHangNavigation.HoTen = request.HoTen ?? thuePhong.IdKhachHangNavigation.HoTen;
                thuePhong.IdKhachHangNavigation.Cccd = request.Cccd ?? thuePhong.IdKhachHangNavigation.Cccd;
                thuePhong.IdKhachHangNavigation.GioiTinh = request.GioiTinh ?? thuePhong.IdKhachHangNavigation.GioiTinh;
                thuePhong.IdKhachHangNavigation.NgaySinh = request.NgaySinh ?? thuePhong.IdKhachHangNavigation.NgaySinh;
            }

            // Cập nhật loại phòng nếu có thay đổi
            if (request.IdLoaiPhong.HasValue && request.IdLoaiPhong != thuePhong.IdLoaiPhong)
            {
                thuePhong.IdLoaiPhong = request.IdLoaiPhong.Value;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("api/dondepphong")]
    public async Task<IActionResult> dondepphong([FromBody] DonDepRequest request)
    {
        try
        {
            var phong = await _context.Phongs.FindAsync(request.roomId);
            if (phong == null)
                return Json(new { success = false, message = "Không tìm thấy thông tin phòng" });

            if (phong.TrangThai != 3)
                return Json(new { success = false, message = "Phòng không ở trạng thái đang dọn dẹp" });

            phong.TrangThai = 0; // Đánh dấu phòng trống và sẵn sàng
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    private class ThongTinGiaPhong
    {
        public double SoGio { get; set; }
        public decimal GiaGioDau { get; set; }
        public decimal GiaGioSau { get; set; }
        public decimal TongTien { get; set; }
        public bool IsQuaDem { get; set; }
        public decimal GiaQuaDem { get; set; }
    }

    private ThongTinGiaPhong TinhGiaPhong(DateTime thoiGianVao, DateTime thoiGianRa, LoaiPhong loaiPhong)
    {
        var thoiGianThue = thoiGianRa - thoiGianVao;
        var gioVao = thoiGianVao.TimeOfDay;
        var gioRa = thoiGianRa.TimeOfDay;
        
        // Làm tròn số giờ lên nếu có phút lẻ
        var soGio = Math.Ceiling(thoiGianThue.TotalHours);
        var giaGioDau = decimal.Parse(loaiPhong.GioDau ?? "0");
        var giaGioSau = decimal.Parse(loaiPhong.GioSau ?? "0");
        var giaQuaDem = decimal.Parse(loaiPhong.QuaDem ?? "0");

        decimal giaTien = 0;
        bool isQuaDem = false;

        // Chỉ áp dụng giá qua đêm nếu thời gian thuê trên 5 tiếng
        if (soGio > 5)
        {
            // Kiểm tra thời gian qua đêm (từ 23h đến 8h sáng hôm sau)
            if (gioVao >= TimeSpan.FromHours(23))
            {
                isQuaDem = true;
                giaTien = giaQuaDem;

                // Nếu thời gian ra > 8h sáng hôm sau
                if (thoiGianRa.Date > thoiGianVao.Date && gioRa > TimeSpan.FromHours(8))
                {
                    var gioVuotQua = Math.Ceiling((gioRa - TimeSpan.FromHours(8)).TotalHours);
                    giaTien += giaGioSau * (decimal)gioVuotQua;
                }
            }
            else if (gioVao <= TimeSpan.FromHours(8) && thoiGianVao.Date == thoiGianRa.Date)
            {
                isQuaDem = true;
                giaTien = giaQuaDem;

                if (gioRa > TimeSpan.FromHours(8))
                {
                    var gioVuotQua = Math.Ceiling((gioRa - TimeSpan.FromHours(8)).TotalHours);
                    giaTien += giaGioSau * (decimal)gioVuotQua;
                }
            }
            else
            {
                // Tính giá theo giờ thông thường
                giaTien = giaGioDau + (giaGioSau * (decimal)(soGio - 1));
            }
        }
        else
        {
            // Nếu thời gian thuê <= 5 tiếng, luôn tính theo giá giờ
            if (soGio <= 1)
            {
                giaTien = giaGioDau;
            }
            else
            {
                giaTien = giaGioDau + (giaGioSau * (decimal)(soGio - 1));
            }
        }

        return new ThongTinGiaPhong
        {
            SoGio = soGio,
            GiaGioDau = giaGioDau,
            GiaGioSau = giaGioSau,
            TongTien = giaTien,
            IsQuaDem = isQuaDem,
            GiaQuaDem = giaQuaDem
        };
    }

    [HttpGet("QuanLyPhong")]
    public IActionResult QuanLyPhong()
    {
        return View();
    }

    public IActionResult QuanLyDanhSachPhong()
    {
        var danhSachPhong = _context.Phongs
            .Include(p => p.IdLoaiPhongMacDinhNavigation)
            .OrderBy(p => p.ViTri)
            .ToList();
        return View(danhSachPhong);
    }

    [HttpGet("api/phong/{id}")]
    public IActionResult LayThongTinPhong(int id)
    {
        try
        {
            var phong = _context.Phongs.Find(id);
            if (phong == null)
                return Json(new { success = false, message = "Không tìm thấy phòng" });

            return Json(new { success = true, data = phong });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("api/phong/them")]
    public async Task<IActionResult> ThemPhong([FromBody] Phong phong)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            // Kiểm tra tên phòng đã tồn tại
            if (await _context.Phongs.AnyAsync(p => p.TenPhong == phong.TenPhong))
                return Json(new { success = false, message = "Tên phòng đã tồn tại" });

            // Kiểm tra loại phòng tồn tại
            if (!await _context.LoaiPhongs.AnyAsync(lp => lp.Id == phong.IdLoaiPhongMacDinh))
                return Json(new { success = false, message = "Loại phòng không tồn tại" });

            _context.Phongs.Add(phong);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("api/phong/capnhat")]
    public async Task<IActionResult> CapNhatPhong([FromBody] Phong phong)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var existingPhong = await _context.Phongs.FindAsync(phong.Id);
            if (existingPhong == null)
                return Json(new { success = false, message = "Không tìm thấy phòng" });

            // Kiểm tra tên phòng đã tồn tại (trừ phòng hiện tại)
            if (await _context.Phongs.AnyAsync(p => p.TenPhong == phong.TenPhong && p.Id != phong.Id))
                return Json(new { success = false, message = "Tên phòng đã tồn tại" });

            // Kiểm tra loại phòng tồn tại
            if (!await _context.LoaiPhongs.AnyAsync(lp => lp.Id == phong.IdLoaiPhongMacDinh))
                return Json(new { success = false, message = "Loại phòng không tồn tại" });

            existingPhong.TenPhong = phong.TenPhong;
            existingPhong.IdLoaiPhongMacDinh = phong.IdLoaiPhongMacDinh;
            existingPhong.ViTri = phong.ViTri;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("api/phong/xoa/{id}")]
    public async Task<IActionResult> XoaPhong(int id)
    {
        try
        {
            var phong = await _context.Phongs.FindAsync(id);
            if (phong == null)
                return Json(new { success = false, message = "Không tìm thấy phòng" });

            // Kiểm tra xem phòng có đang được thuê không
            if (phong.TrangThai == 1)
                return Json(new { success = false, message = "Không thể xóa phòng đang được thuê" });

            _context.Phongs.Remove(phong);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("api/loaiphong/danhsach")]
    public async Task<IActionResult> GetDanhSachLoaiPhong()
    {
        try
        {
            var loaiPhongs = await _context.LoaiPhongs
                .Select(lp => new
                {
                    lp.Id,
                    lp.TenLoai,
                    lp.GioDau,
                    lp.GioSau,
                    lp.QuaDem,
                    lp.MoTa
                })
                .ToListAsync();
            return Json(loaiPhongs);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("api/loaiphong/them")]
    public async Task<IActionResult> ThemLoaiPhong([FromBody] LoaiPhong loaiPhong)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            _context.LoaiPhongs.Add(loaiPhong);
            await _context.SaveChangesAsync();

            return Json(new { success = true, data = loaiPhong });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("api/loaiphong/capnhat")]
    public async Task<IActionResult> CapNhatLoaiPhong([FromBody] LoaiPhong loaiPhong)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var existingLoaiPhong = await _context.LoaiPhongs.FindAsync(loaiPhong.Id);
            if (existingLoaiPhong == null)
                return Json(new { success = false, message = "Không tìm thấy loại phòng" });

            existingLoaiPhong.TenLoai = loaiPhong.TenLoai;
            existingLoaiPhong.GioDau = loaiPhong.GioDau;
            existingLoaiPhong.GioSau = loaiPhong.GioSau;
            existingLoaiPhong.QuaDem = loaiPhong.QuaDem;
            existingLoaiPhong.MoTa = loaiPhong.MoTa;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("api/loaiphong/xoa/{id}")]
    public async Task<IActionResult> XoaLoaiPhong(int id)
    {
        try
        {
            var loaiPhong = await _context.LoaiPhongs.FindAsync(id);
            if (loaiPhong == null)
                return Json(new { success = false, message = "Không tìm thấy loại phòng" });

            // Kiểm tra xem có phòng nào đang sử dụng loại phòng này không
            var phongSuDung = await _context.Phongs.AnyAsync(p => p.IdLoaiPhongMacDinh == id);
            if (phongSuDung)
                return Json(new { success = false, message = "Không thể xóa loại phòng đang được sử dụng" });

            _context.LoaiPhongs.Remove(loaiPhong);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

public class ThuePhongRequest
{
    public int IdPhong { get; set; }
    public int IdLoaiPhong { get; set; }
    public string? HoTen { get; set; }
    public string? Cccd { get; set; }
    public string? GioiTinh { get; set; }
    public string? NgaySinh { get; set; }
}

public class TraPhongRequest
{
    public int ThuePhongId { get; set; }
}

public class CapNhatThuePhongRequest
{
    public int ThuePhongId { get; set; }
    public int? IdLoaiPhong { get; set; }
    public string? HoTen { get; set; }
    public string? Cccd { get; set; }
    public string? GioiTinh { get; set; }
    public string? NgaySinh { get; set; }
}

public class DonDepRequest
{
    public int roomId { get; set; }
}
