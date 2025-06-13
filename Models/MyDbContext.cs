using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NhaNghiYenNhi.Models;

public partial class MyDbContext : DbContext
{
    public MyDbContext()
    {
    }

    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<KhachHang> KhachHangs { get; set; }

    public virtual DbSet<LoaiPhong> LoaiPhongs { get; set; }

    public virtual DbSet<Phong> Phongs { get; set; }

    public virtual DbSet<SanPhamNhaNghi> SanPhamNhaNghis { get; set; }

    public virtual DbSet<ThuePhong> ThuePhongs { get; set; }

    public virtual DbSet<TraPhong> TraPhongs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=MSI;Database=NhaNghiYenNhi;Integrated Security=true;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__KhachHan__3213E83FD0ECAE34");

            entity.ToTable("KhachHang");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Cccd)
                .HasMaxLength(50)
                .HasColumnName("CCCD");
            entity.Property(e => e.GioiTinh).HasMaxLength(50);
            entity.Property(e => e.HoTen).HasMaxLength(50);
            entity.Property(e => e.NgaySinh).HasMaxLength(50);
        });

        modelBuilder.Entity<LoaiPhong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LoaiPhon__3213E83F4B66B1E7");

            entity.ToTable("LoaiPhong");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GioDau).HasMaxLength(50);
            entity.Property(e => e.GioSau).HasMaxLength(50);
            entity.Property(e => e.MoTa).HasMaxLength(50);
            entity.Property(e => e.QuaDem).HasMaxLength(50);
            entity.Property(e => e.TenLoai).HasMaxLength(50);
        });

        modelBuilder.Entity<Phong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Phong__3213E83F64DA1F0A");

            entity.ToTable("Phong");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdLoaiPhongMacDinh).HasColumnName("idLoaiPhongMacDinh");
            entity.Property(e => e.TenPhong).HasMaxLength(50);

            entity.HasOne(d => d.IdLoaiPhongMacDinhNavigation).WithMany(p => p.Phongs)
                .HasForeignKey(d => d.IdLoaiPhongMacDinh)
                .HasConstraintName("FK__Phong__idLoaiPho__3D5E1FD2");
        });

        modelBuilder.Entity<SanPhamNhaNghi>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SanPhamN__3213E83FB1EB48E6");

            entity.ToTable("SanPhamNhaNghi");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.HinhAnh).HasMaxLength(50);
            entity.Property(e => e.TenSanPham).HasMaxLength(50);
        });

        modelBuilder.Entity<ThuePhong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ThuePhon__3213E83F13D45703");

            entity.ToTable("ThuePhong");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdKhachHang).HasColumnName("idKhachHang");
            entity.Property(e => e.IdLoaiPhong).HasColumnName("idLoaiPhong");
            entity.Property(e => e.IdPhong).HasColumnName("idPhong");
            entity.Property(e => e.SanPhamDaMua).HasMaxLength(500);
            entity.Property(e => e.ThoiGianVao).HasColumnType("datetime");

            entity.HasOne(d => d.IdKhachHangNavigation).WithMany(p => p.ThuePhongs)
                .HasForeignKey(d => d.IdKhachHang)
                .HasConstraintName("FK__ThuePhong__idKha__4222D4EF");

            entity.HasOne(d => d.IdLoaiPhongNavigation).WithMany(p => p.ThuePhongs)
                .HasForeignKey(d => d.IdLoaiPhong)
                .HasConstraintName("FK__ThuePhong__idLoa__412EB0B6");

            entity.HasOne(d => d.IdPhongNavigation).WithMany(p => p.ThuePhongs)
                .HasForeignKey(d => d.IdPhong)
                .HasConstraintName("FK__ThuePhong__idPho__403A8C7D");
        });

        modelBuilder.Entity<TraPhong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TraPhong__3213E83F9DC2CE00");

            entity.ToTable("TraPhong");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GiaTien).HasMaxLength(50);
            entity.Property(e => e.IdThuePhong).HasColumnName("idThuePhong");
            entity.Property(e => e.ThoiGianTra).HasColumnType("datetime");
            entity.Property(e => e.TongGioThue).HasMaxLength(50);

            entity.HasOne(d => d.IdThuePhongNavigation).WithMany(p => p.TraPhongs)
                .HasForeignKey(d => d.IdThuePhong)
                .HasConstraintName("FK__TraPhong__idThue__44FF419A");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
