using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text.Json;
using System.Globalization;

namespace NhaNghiYenNhi.Controllers
{
    public class ThongKeController : Controller
    {
        private readonly MyDbContext _context;

        public ThongKeController(MyDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("api/thongke/doanhthu")]
        public async Task<IActionResult> GetDoanhThu(DateTime? fromDate = null, DateTime? toDate = null, string loaiThongKe = "ngay")
        {
            try
            {
                var query = _context.TraPhongs
                    .Include(tp => tp.IdThuePhongNavigation)
                    .Where(tp => tp.ThoiGianTra != null);

                if (fromDate.HasValue)
                {
                    query = query.Where(tp => tp.ThoiGianTra >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(tp => tp.ThoiGianTra <= toDate.Value);
                }

                var traPhongs = await query.ToListAsync();

                var result = new List<object>();
                decimal tongDoanhThu = 0;

                switch (loaiThongKe.ToLower())
                {
                    case "ngay":
                        result = traPhongs
                            .GroupBy(tp => tp.ThoiGianTra?.Date)
                            .Select(g => new
                            {
                                ThoiGian = g.Key?.ToString("dd/MM/yyyy"),
                                DoanhThu = g.Sum(tp => decimal.Parse(tp.GiaTien ?? "0")),
                                SoLuotThue = g.Count()
                            })
                            .OrderBy(x => x.ThoiGian)
                            .Cast<object>()
                            .ToList();
                        break;

                    case "tuan":
                        result = traPhongs
                            .GroupBy(tp => new
                            {
                                Year = tp.ThoiGianTra?.Year,
                                Week = tp.ThoiGianTra.HasValue ? GetIso8601WeekOfYear(tp.ThoiGianTra.Value) : 0
                            })
                            .Select(g => new
                            {
                                ThoiGian = $"Tuần {g.Key.Week}, {g.Key.Year}",
                                DoanhThu = g.Sum(tp => decimal.Parse(tp.GiaTien ?? "0")),
                                SoLuotThue = g.Count()
                            })
                            .OrderBy(x => x.ThoiGian)
                            .Cast<object>()
                            .ToList();
                        break;

                    case "thang":
                        result = traPhongs
                            .GroupBy(tp => new { tp.ThoiGianTra?.Year, tp.ThoiGianTra?.Month })
                            .Select(g => new
                            {
                                ThoiGian = $"{g.Key.Month}/{g.Key.Year}",
                                DoanhThu = g.Sum(tp => decimal.Parse(tp.GiaTien ?? "0")),
                                SoLuotThue = g.Count()
                            })
                            .OrderBy(x => x.ThoiGian)
                            .Cast<object>()
                            .ToList();
                        break;

                    case "nam":
                        result = traPhongs
                            .GroupBy(tp => tp.ThoiGianTra?.Year)
                            .Select(g => new
                            {
                                ThoiGian = g.Key.ToString(),
                                DoanhThu = g.Sum(tp => decimal.Parse(tp.GiaTien ?? "0")),
                                SoLuotThue = g.Count()
                            })
                            .OrderBy(x => x.ThoiGian)
                            .Cast<object>()
                            .ToList();
                        break;
                }

                tongDoanhThu = traPhongs.Sum(tp => decimal.Parse(tp.GiaTien ?? "0"));

                return Json(new
                {
                    success = true,
                    data = result,
                    tongDoanhThu = tongDoanhThu,
                    tongLuotThue = traPhongs.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private int GetIso8601WeekOfYear(DateTime time)
        {
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }

            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
} 