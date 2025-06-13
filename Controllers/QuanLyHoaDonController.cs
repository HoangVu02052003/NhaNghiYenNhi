using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text.Json;

namespace NhaNghiYenNhi.Controllers
{
    public class QuanLyHoaDonController : Controller
    {
        private readonly MyDbContext _context;

        public QuanLyHoaDonController(MyDbContext context)
        {
            _context = context;
        }

        public IActionResult QuanLyHoaDon()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetHoaDon(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.ThuePhongs
                .Include(t => t.IdKhachHangNavigation)
                .Include(t => t.IdPhongNavigation)
                .Include(t => t.IdLoaiPhongNavigation)
                .Include(t => t.TraPhongs)
                .Where(t => t.TraPhongs.Any())
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(t => t.ThoiGianVao >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(t => t.ThoiGianVao <= toDate.Value);
            }

            // First get the data from database
            var thuePhongs = await query.ToListAsync();

            // Then perform ordering in memory
            var orderedThuePhongs = thuePhongs
                .OrderByDescending(t => t.TraPhongs.FirstOrDefault()?.ThoiGianTra)
                .ToList();

            // Get all products for lookup
            var allProducts = await _context.SanPhamNhaNghis.ToDictionaryAsync(p => p.Id, p => new { p.TenSanPham, p.Gia });

            var hoaDons = orderedThuePhongs.Select(t => new
            {
                t.Id,
                HoTen = t.IdKhachHangNavigation != null ? t.IdKhachHangNavigation.HoTen ?? "Khách vãng lai" : "Khách vãng lai",
                CCCD = t.IdKhachHangNavigation?.Cccd,
                NgaySinh = t.IdKhachHangNavigation?.NgaySinh,
                TenPhong = t.IdPhongNavigation?.TenPhong ?? "Không xác định",
                TenLoai = t.IdLoaiPhongNavigation?.TenLoai ?? "Không xác định",
                ThoiGianVao = t.ThoiGianVao,
                ThoiGianRa = t.TraPhongs.FirstOrDefault()?.ThoiGianTra,
                TongThoiGian = t.TraPhongs.FirstOrDefault()?.TongGioThue ?? "0",
                TienPhong = decimal.Parse(t.TraPhongs.FirstOrDefault()?.GiaTien ?? "0"),
                SanPhamDaMua = t.SanPhamDaMua
            }).ToList();

            var result = hoaDons.Select(h =>
            {
                // Parse product quantities
                Dictionary<int, int> sanPhamDaMua = new Dictionary<int, int>();
                decimal tongTienSanPham = 0;
                var danhSachSanPham = new List<object>();

                if (!string.IsNullOrEmpty(h.SanPhamDaMua))
                {
                    try
                    {
                        sanPhamDaMua = JsonSerializer.Deserialize<Dictionary<int, int>>(h.SanPhamDaMua);
                        foreach (var sp in sanPhamDaMua)
                        {
                            if (allProducts.TryGetValue(sp.Key, out var product))
                            {
                                decimal thanhTien = (decimal)(product.Gia ?? 0) * sp.Value;
                                tongTienSanPham += thanhTien;
                                danhSachSanPham.Add(new
                                {
                                    TenSanPham = product.TenSanPham,
                                    SoLuong = sp.Value,
                                    DonGia = product.Gia ?? 0,
                                    ThanhTien = thanhTien
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Handle old format if necessary
                        var oldIds = h.SanPhamDaMua.Split('-').Select(int.Parse);
                        foreach (var id in oldIds)
                        {
                            if (allProducts.TryGetValue(id, out var product))
                            {
                                decimal donGia = (decimal)(product.Gia ?? 0);
                                tongTienSanPham += donGia;
                                danhSachSanPham.Add(new
                                {
                                    TenSanPham = product.TenSanPham,
                                    SoLuong = 1,
                                    DonGia = donGia,
                                    ThanhTien = donGia
                                });
                            }
                        }
                    }
                }

                return new
                {
                    h.Id,
                    h.HoTen,
                    h.CCCD,
                    h.NgaySinh,
                    h.TenPhong,
                    h.TenLoai,
                    h.ThoiGianVao,
                    h.ThoiGianRa,
                    h.TongThoiGian,
                    TienPhong = h.TienPhong,
                    TienSanPham = tongTienSanPham,
                    TongTien = h.TienPhong + tongTienSanPham,
                    DanhSachSanPham = danhSachSanPham
                };
            });

            return Json(result);
        }
    }
} 