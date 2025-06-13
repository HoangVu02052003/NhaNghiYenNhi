using System;
using System.Collections.Generic;

namespace NhaNghiYenNhi.Models;

public partial class LoaiPhong
{
    public int Id { get; set; }

    public string? TenLoai { get; set; }

    public string? GioDau { get; set; }

    public string? GioSau { get; set; }

    public string? QuaDem { get; set; }

    public string? MoTa { get; set; }

    public virtual ICollection<Phong> Phongs { get; set; } = new List<Phong>();

    public virtual ICollection<ThuePhong> ThuePhongs { get; set; } = new List<ThuePhong>();
}
