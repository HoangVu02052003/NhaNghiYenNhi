using System;
using System.Collections.Generic;

namespace NhaNghiYenNhi.Models;

public partial class SanPhamNhaNghi
{
    public int Id { get; set; }

    public string? TenSanPham { get; set; }

    public int? Gia { get; set; }

    public bool? Con { get; set; }

    public string? HinhAnh { get; set; }
}
