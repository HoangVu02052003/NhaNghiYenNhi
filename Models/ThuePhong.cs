using System;
using System.Collections.Generic;

namespace NhaNghiYenNhi.Models;

public partial class ThuePhong
{
    public int Id { get; set; }

    public int? IdPhong { get; set; }

    public int? IdLoaiPhong { get; set; }

    public int? IdKhachHang { get; set; }

    public DateTime? ThoiGianVao { get; set; }

    public string? SanPhamDaMua { get; set; }

    public virtual KhachHang? IdKhachHangNavigation { get; set; }

    public virtual LoaiPhong? IdLoaiPhongNavigation { get; set; }

    public virtual Phong? IdPhongNavigation { get; set; }

    public virtual ICollection<TraPhong> TraPhongs { get; set; } = new List<TraPhong>();
}
