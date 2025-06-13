using System;
using System.Collections.Generic;

namespace NhaNghiYenNhi.Models;

public partial class Phong
{
    public int Id { get; set; }

    public string? TenPhong { get; set; }

    public int? IdLoaiPhongMacDinh { get; set; }

    public int? TrangThai { get; set; }

    public int? ViTri { get; set; }

    public virtual LoaiPhong? IdLoaiPhongMacDinhNavigation { get; set; }

    public virtual ICollection<ThuePhong> ThuePhongs { get; set; } = new List<ThuePhong>();
}
