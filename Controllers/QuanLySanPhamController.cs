using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text.Json;

namespace NhaNghiYenNhi.Controllers
{
    public class QuanLySanPhamController : Controller
    {
        private readonly MyDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public QuanLySanPhamController(MyDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;

            // Ensure img directory exists
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Copy default image if it doesn't exist
            string defaultImagePath = Path.Combine(uploadsFolder, "default.jpg");
            if (!System.IO.File.Exists(defaultImagePath))
            {
                string sourceDefaultImage = Path.Combine(_webHostEnvironment.WebRootPath, "images", "default.jpg");
                if (System.IO.File.Exists(sourceDefaultImage))
                {
                    System.IO.File.Copy(sourceDefaultImage, defaultImagePath);
                }
            }
        }

        [HttpGet("img/{filename}")]
        public IActionResult GetImage(string filename)
        {
            var path = Path.Combine(_webHostEnvironment.WebRootPath, "img", filename);
            
            // If the image doesn't exist, return a default image
            if (!System.IO.File.Exists(path))
            {
                // Return a simple transparent 1x1 pixel GIF
                byte[] transparentPixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
                return File(transparentPixel, "image/gif");
            }

            return PhysicalFile(path, GetContentType(filename));
        }

        private string GetContentType(string filename)
        {
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }

        public IActionResult QuanLySanPham()
        {
            return View();
        }

        [HttpGet("api/getsanpham")]
        public async Task<IActionResult> GetSanPham(string? search = null)
        {
            var query = _context.SanPhamNhaNghis.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(sp => sp.TenSanPham.ToLower().Contains(search));
            }

            var sanPhams = await query.Select(sp => new
            {
                sp.Id,
                sp.TenSanPham,
                sp.HinhAnh,
                sp.Gia
            }).ToListAsync();

            return Json(sanPhams);
        }

        [HttpPost("api/themsanpham")]
        public async Task<IActionResult> ThemSanPham([FromForm] SanPhamRequest request)
        {
            try
            {
                string uniqueFileName = null;
                if (request.HinhAnh != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");
                    
                    // Ensure the uploads directory exists
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.HinhAnh.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.HinhAnh.CopyToAsync(fileStream);
                    }
                }

                var sanPham = new SanPhamNhaNghi
                {
                    TenSanPham = request.TenSanPham,
                    HinhAnh = uniqueFileName,
                    Gia = request.Gia
                };

                _context.SanPhamNhaNghis.Add(sanPham);
                await _context.SaveChangesAsync();

                return Json(new { success = true, sanPham });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("api/suasanpham")]
        public async Task<IActionResult> SuaSanPham([FromForm] SanPhamRequest request)
        {
            try
            {
                var sanPham = await _context.SanPhamNhaNghis.FindAsync(request.Id);
                if (sanPham == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

                if (request.HinhAnh != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");
                    
                    // Ensure the uploads directory exists
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Xóa ảnh cũ nếu có
                    if (!string.IsNullOrEmpty(sanPham.HinhAnh))
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, sanPham.HinhAnh);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                            catch
                            {
                                // Ignore deletion errors
                            }
                        }
                    }

                    // Lưu ảnh mới
                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.HinhAnh.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.HinhAnh.CopyToAsync(fileStream);
                    }

                    sanPham.HinhAnh = uniqueFileName;
                }

                sanPham.TenSanPham = request.TenSanPham;
                sanPham.Gia = request.Gia;

                await _context.SaveChangesAsync();
                return Json(new { success = true, sanPham });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("api/xoasanpham/{id}")]
        public async Task<IActionResult> XoaSanPham(int id)
        {
            try
            {
                var sanPham = await _context.SanPhamNhaNghis.FindAsync(id);
                if (sanPham == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

                // Xóa ảnh
                if (!string.IsNullOrEmpty(sanPham.HinhAnh))
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");
                    var imagePath = Path.Combine(uploadsFolder, sanPham.HinhAnh);
                    if (System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(imagePath);
                        }
                        catch
                        {
                            // Ignore deletion errors
                        }
                    }
                }

                _context.SanPhamNhaNghis.Remove(sanPham);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("api/muasanpham")]
        public async Task<IActionResult> MuaSanPham([FromBody] MuaSanPhamRequest request)
        {
            try
            {
                var thuePhong = await _context.ThuePhongs.FindAsync(request.ThuePhongId);
                if (thuePhong == null)
                    return Json(new { success = false, message = "Không tìm thấy thông tin thuê phòng" });

                // Parse existing products or create new dictionary
                Dictionary<int, int> sanPhamDaMua;
                if (string.IsNullOrEmpty(thuePhong.SanPhamDaMua))
                {
                    sanPhamDaMua = new Dictionary<int, int>();
                }
                else
                {
                    try
                    {
                        sanPhamDaMua = JsonSerializer.Deserialize<Dictionary<int, int>>(thuePhong.SanPhamDaMua);
                    }
                    catch
                    {
                        // Convert old format to new format
                        var oldIds = thuePhong.SanPhamDaMua.Split('-').Select(int.Parse);
                        sanPhamDaMua = oldIds.GroupBy(id => id)
                                           .ToDictionary(g => g.Key, g => g.Count());
                    }
                }

                // Update quantity
                if (sanPhamDaMua.ContainsKey(request.SanPhamId))
                {
                    sanPhamDaMua[request.SanPhamId] += request.SoLuong;
                }
                else
                {
                    sanPhamDaMua[request.SanPhamId] = request.SoLuong;
                }

                // Save as JSON
                thuePhong.SanPhamDaMua = JsonSerializer.Serialize(sanPhamDaMua);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class SanPhamRequest
    {
        public int? Id { get; set; }
        public required string TenSanPham { get; set; }
        public IFormFile? HinhAnh { get; set; }
        public int Gia { get; set; }
    }

    public class MuaSanPhamRequest
    {
        public int ThuePhongId { get; set; }
        public int SanPhamId { get; set; }
        public int SoLuong { get; set; } = 1;
    }
} 