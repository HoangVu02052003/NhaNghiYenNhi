using System;
using System.Collections.Generic;

namespace NhaNghiYenNhi.Models;

public partial class TraPhong
{
    public int Id { get; set; }

    public int? IdThuePhong { get; set; }

    public DateTime? ThoiGianTra { get; set; }

    public string? TongGioThue { get; set; }

    public string? GiaTien { get; set; }

    public virtual ThuePhong? IdThuePhongNavigation { get; set; }
}
