using System;
using System.Collections.Generic;

namespace NhaNghiYenNhi.Models;

public partial class KhachHang
{
    public int Id { get; set; }

    public string? HoTen { get; set; }

    public string? GioiTinh { get; set; }

    public string? NgaySinh { get; set; }

    public string? Cccd { get; set; }

    public virtual ICollection<ThuePhong> ThuePhongs { get; set; } = new List<ThuePhong>();
}
