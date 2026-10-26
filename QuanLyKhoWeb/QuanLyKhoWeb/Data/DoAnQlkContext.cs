using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuanLyKhoWeb.Models.Entities;

namespace QuanLyKhoWeb.Data;

public partial class DoAnQlkContext : DbContext
{
    public DoAnQlkContext()
    {
    }

    public DoAnQlkContext(DbContextOptions<DoAnQlkContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BoDemMaSkuTheoThang> BoDemMaSkuTheoThangs { get; set; }

    public virtual DbSet<ChiTietDonHang> ChiTietDonHangs { get; set; }

    public virtual DbSet<ChiTietPhieuNhapKho> ChiTietPhieuNhapKhos { get; set; }

    public virtual DbSet<DanhMuc> DanhMucs { get; set; }

    public virtual DbSet<DonHang> DonHangs { get; set; }

    public virtual DbSet<KeHang> KeHangs { get; set; }

    public virtual DbSet<LoHang> LoHangs { get; set; }

    public virtual DbSet<KhachHang> KhachHangs { get; set; }

    public virtual DbSet<KhuVucKho> KhuVucKhos { get; set; }

    public virtual DbSet<KhuyenMai> KhuyenMais { get; set; }

    public virtual DbSet<MatKhauDatLai> MatKhauDatLais { get; set; }

    public virtual DbSet<MauSanPham> MauSanPhams { get; set; }

    public virtual DbSet<NguoiDung> NguoiDungs { get; set; }

    public virtual DbSet<NhaCungCap> NhaCungCaps { get; set; }

    public virtual DbSet<NhatKyThayDoiNguoiDung> NhatKyThayDoiNguoiDungs { get; set; }

    public virtual DbSet<NhatKyTonKho> NhatKyTonKhos { get; set; }

    public virtual DbSet<PhanBoXuatKhoTheoKe> PhanBoXuatKhoTheoKes { get; set; }

    public virtual DbSet<PhieuNhapKho> PhieuNhapKhos { get; set; }

    public virtual DbSet<SanPhamSku> SanPhamSkus { get; set; }

    public virtual DbSet<TonKhoKeHang> TonKhoKeHangs { get; set; }

    public virtual DbSet<TonKhoKhu> TonKhoKhus { get; set; }

    public virtual DbSet<VaiTro> VaiTros { get; set; }

    public virtual DbSet<VanDon> VanDons { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoDemMaSkuTheoThang>(entity =>
        {
            entity.HasKey(e => e.ThangNam).HasName("PK__bo_dem_m__1717834D1C191C80");

            entity.ToTable("bo_dem_ma_sku_theo_thang");

            entity.Property(e => e.ThangNam)
                .HasMaxLength(4)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("thang_nam");
            entity.Property(e => e.SoLuongDaDung).HasColumnName("so_luong_da_dung");
        });

        modelBuilder.Entity<ChiTietDonHang>(entity =>
        {
            entity.HasKey(e => e.MaChiTietDon).HasName("PK__chi_tiet__B366EC05E77895EE");

            entity.ToTable("chi_tiet_don_hang", tb =>
            {
                tb.HasTrigger("trg_cthd_hoan_ton_khi_xoa_dong");
                tb.HasTrigger("trg_cthd_tru_ton_khi_ban");
            });

            entity.HasIndex(e => e.MaDonHang, "IX_cthd_madonhang");

            entity.HasIndex(e => e.MaSku, "IX_cthd_masku");

            entity.Property(e => e.MaChiTietDon).HasColumnName("ma_chi_tiet_don");
            entity.Property(e => e.DonGia)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("don_gia");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.SoLuong)
                .HasDefaultValue(1)
                .HasColumnName("so_luong");
            entity.Property(e => e.ThanhTienDong)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("thanh_tien_dong");

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.ChiTietDonHangs)
                .HasForeignKey(d => d.MaDonHang)
                .HasConstraintName("FK_cthd_donhang");

            entity.HasOne(d => d.MaSkuNavigation).WithMany(p => p.ChiTietDonHangs)
                .HasForeignKey(d => d.MaSku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cthd_sku");
        });

        modelBuilder.Entity<ChiTietPhieuNhapKho>(entity =>
        {
            entity.HasKey(e => e.MaChiTietPhieuNhap).HasName("PK__chi_tiet__F86419831E165A1E");

            entity.ToTable("chi_tiet_phieu_nhap_kho", tb => tb.HasTrigger("trg_phieunhap_capnhat_tongtien"));

            entity.HasIndex(e => e.MaPhieuNhap, "IX_ctpn_phieunhap");

            entity.HasIndex(e => e.MaSku, "IX_ctpn_sku");

            entity.Property(e => e.MaChiTietPhieuNhap).HasColumnName("ma_chi_tiet_phieu_nhap");
            entity.Property(e => e.DonGiaNhap)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("don_gia_nhap");
            entity.Property(e => e.MaPhieuNhap).HasColumnName("ma_phieu_nhap");
            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.SoLuongKeHoach).HasColumnName("so_luong_ke_hoach");
            entity.Property(e => e.SoLuongThucNhan).HasColumnName("so_luong_thuc_nhan");
            entity.Property(e => e.ThanhTien)
                .HasComputedColumnSql("([so_luong_thuc_nhan]*[don_gia_nhap])", true)
                .HasColumnType("decimal(26, 2)")
                .HasColumnName("thanh_tien");

            entity.HasOne(d => d.MaPhieuNhapNavigation).WithMany(p => p.ChiTietPhieuNhapKhos)
                .HasForeignKey(d => d.MaPhieuNhap)
                .HasConstraintName("FK_ctpn_phieunhap");

            entity.HasOne(d => d.MaSkuNavigation).WithMany(p => p.ChiTietPhieuNhapKhos)
                .HasForeignKey(d => d.MaSku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ctpn_sku");
        });

        modelBuilder.Entity<DanhMuc>(entity =>
        {
            entity.HasKey(e => e.MaDanhMuc).HasName("PK__danh_muc__70624BDDE92110CB");

            entity.ToTable("danh_muc");

            entity.HasIndex(e => e.MaCodeDanhMuc, "UQ__danh_muc__08FC4E6D00C6A7E8").IsUnique();

            entity.HasIndex(e => e.TenDanhMuc, "UQ__danh_muc__B3D2892505FE4002").IsUnique();

            entity.Property(e => e.MaDanhMuc).HasColumnName("ma_danh_muc");
            entity.Property(e => e.MaCodeDanhMuc)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ma_code_danh_muc");
            entity.Property(e => e.TenDanhMuc)
                .HasMaxLength(100)
                .HasColumnName("ten_danh_muc");
        });

        modelBuilder.Entity<DonHang>(entity =>
        {
            entity.HasKey(e => e.MaDonHang).HasName("PK__don_hang__0246C5EABA92E9EC");

            entity.ToTable("don_hang", tb =>
            {
                tb.HasTrigger("trg_donhang_capnhat_thanhtien");
                tb.HasTrigger("trg_donhang_hoan_ton_khi_huy");
                tb.HasTrigger("trg_donhang_kiemtra_khuyenmai");
            });

            entity.HasIndex(e => e.MaKhachHang, "IX_donhang_makhachhang");

            entity.HasIndex(e => e.MaKhuyenMai, "IX_donhang_makhuyenmai");

            entity.HasIndex(e => e.MaNhanVien, "IX_donhang_manhanvien");

            entity.HasIndex(e => e.NgayDatHang, "IX_donhang_ngaydathang");

            entity.HasIndex(e => new { e.TrangThai, e.NgayDatHang }, "IX_donhang_trangthai_ngaydat");

            entity.HasIndex(e => e.MaCodeDonHang, "UQ__don_hang__E7107AC4B61E049B").IsUnique();

            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.DiaChiGiaoHang)
                .HasMaxLength(255)
                .HasColumnName("dia_chi_giao_hang");
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
            entity.Property(e => e.HinhThucNhanHang)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("TAI_SHOP")
                .HasColumnName("hinh_thuc_nhan_hang");
            entity.Property(e => e.LyDoHuy).HasColumnName("ly_do_huy");
            entity.Property(e => e.MaCodeDonHang)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValueSql("(concat('DH-',CONVERT([char](8),getdate(),(112)),'-',right('000000'+CONVERT([varchar](20),NEXT VALUE FOR [seq_ma_code_don_hang]),(6))))", "DF_donhang_macode")
                .HasColumnName("ma_code_don_hang");
            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.MaKhuyenMai).HasColumnName("ma_khuyen_mai");
            entity.Property(e => e.MaNhanVien).HasColumnName("ma_nhan_vien");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())", "DF_donhang_ngaycapnhat40")
                .HasColumnName("ngay_cap_nhat");
            entity.Property(e => e.NgayDatHang)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_dat_hang");
            entity.Property(e => e.NgayHuy).HasColumnName("ngay_huy");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())", "DF_donhang_ngaytao40")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.PhiVanChuyen)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("phi_van_chuyen");
            entity.Property(e => e.SdtKhachHangGiao)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("sdt_khach_hang_giao");
            entity.Property(e => e.TenKhachHangGiao)
                .HasMaxLength(150)
                .HasColumnName("ten_khach_hang_giao");
            entity.Property(e => e.ThanhTien)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("thanh_tien");
            entity.Property(e => e.TienGiamGia)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("tien_giam_gia");
            entity.Property(e => e.TongTienHang)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("tong_tien_hang");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("CONFIRMED")
                .HasColumnName("trang_thai");
            entity.Property(e => e.TrangThaiThanhToan)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("UNPAID")
                .HasColumnName("trang_thai_thanh_toan");

            entity.HasOne(d => d.MaKhachHangNavigation).WithMany(p => p.DonHangs)
                .HasForeignKey(d => d.MaKhachHang)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_donhang_khachhang");

            entity.HasOne(d => d.MaKhuyenMaiNavigation).WithMany(p => p.DonHangs)
                .HasForeignKey(d => d.MaKhuyenMai)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_donhang_khuyenmai");

            entity.HasOne(d => d.MaNhanVienNavigation).WithMany(p => p.DonHangs)
                .HasForeignKey(d => d.MaNhanVien)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_donhang_nguoidung");
        });

        modelBuilder.Entity<KeHang>(entity =>
        {
            entity.HasKey(e => e.MaKeHang).HasName("PK__ke_hang__037A746B5FFA97DE");

            entity.ToTable("ke_hang");

            entity.HasIndex(e => e.MaKhuVuc, "IX_kehang_khuvuc");

            entity.HasIndex(e => new { e.MaKhuVuc, e.TenKeHang }, "UK_kehang_khuvuc_ten").IsUnique();

            entity.Property(e => e.MaKeHang).HasColumnName("ma_ke_hang");
            entity.Property(e => e.MaKhuVuc).HasColumnName("ma_khu_vuc");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.TenKeHang)
                .HasMaxLength(50)
                .HasColumnName("ten_ke_hang");

            entity.HasOne(d => d.MaKhuVucNavigation).WithMany(p => p.KeHangs)
                .HasForeignKey(d => d.MaKhuVuc)
                .HasConstraintName("FK_kehang_khuvuc");
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.HasKey(e => e.MaKhachHang).HasName("PK__khach_ha__C9817AF6F07A7D40");

            entity.ToTable("khach_hang");

            entity.Property(e => e.MaKhachHang).HasColumnName("ma_khach_hang");
            entity.Property(e => e.DiaChi)
                .HasMaxLength(255)
                .HasColumnName("dia_chi");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("so_dien_thoai");
            entity.Property(e => e.TenKhachHang)
                .HasMaxLength(150)
                .HasColumnName("ten_khach_hang");
        });

        modelBuilder.Entity<KhuVucKho>(entity =>
        {
            entity.HasKey(e => e.MaKhuVuc).HasName("PK__khu_vuc___6A5E05CDEB2F543D");

            entity.ToTable("khu_vuc_kho");

            entity.HasIndex(e => e.TenKhuVuc, "UK_khuvuc_ten").IsUnique();

            entity.Property(e => e.MaKhuVuc).HasColumnName("ma_khu_vuc");
            entity.Property(e => e.DienTichM2)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("dien_tich_m2");
            entity.Property(e => e.LoaiKhuVuc)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("STORAGE")
                .HasColumnName("loai_khu_vuc");
            entity.Property(e => e.TenKhuVuc)
                .HasMaxLength(50)
                .HasColumnName("ten_khu_vuc");
            entity.Property(e => e.TrangThaiHoatDong)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai_hoat_dong");
        });

        modelBuilder.Entity<KhuyenMai>(entity =>
        {
            entity.HasKey(e => e.MaKhuyenMai).HasName("PK__khuyen_m__01A88CB37332F191");

            entity.ToTable("khuyen_mai");

            entity.HasIndex(e => e.MaCodeKhuyenMai, "UQ__khuyen_m__68F1394E39150511").IsUnique();

            entity.Property(e => e.MaKhuyenMai).HasColumnName("ma_khuyen_mai");
            entity.Property(e => e.GiaTriDonToiThieu)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("gia_tri_don_toi_thieu");
            entity.Property(e => e.GiaTriGiam)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("gia_tri_giam");
            entity.Property(e => e.LoaiGiamGia)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PERCENT")
                .HasColumnName("loai_giam_gia");
            entity.Property(e => e.MaCodeKhuyenMai)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("ma_code_khuyen_mai");
            entity.Property(e => e.NgayBatDau).HasColumnName("ngay_bat_dau");
            entity.Property(e => e.NgayKetThuc).HasColumnName("ngay_ket_thuc");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.SoLanDaDung).HasColumnName("so_lan_da_dung");
            entity.Property(e => e.SoLanSuDungToiDa).HasColumnName("so_lan_su_dung_toi_da");
            entity.Property(e => e.TenKhuyenMai)
                .HasMaxLength(150)
                .HasColumnName("ten_khuyen_mai");
            entity.Property(e => e.TrangThaiHoatDong)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai_hoat_dong");
        });

        modelBuilder.Entity<MatKhauDatLai>(entity =>
        {
            entity.HasKey(e => e.MaDatLai).HasName("PK__mat_khau__F5CAD957B6BA3B52");

            entity.ToTable("mat_khau_dat_lai");

            entity.HasIndex(e => new { e.MaNguoiDung, e.DaSuDung, e.NgayHetHan }, "IX_matkhaudatlai_nguoidung");

            entity.Property(e => e.MaDatLai).HasColumnName("ma_dat_lai");
            entity.Property(e => e.DaSuDung).HasColumnName("da_su_dung");
            entity.Property(e => e.MaNguoiDung).HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.MaXacNhanHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("ma_xac_nhan_hash");
            entity.Property(e => e.NgayHetHan).HasColumnName("ngay_het_han");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");

            entity.HasOne(d => d.MaNguoiDungNavigation).WithMany(p => p.MatKhauDatLais)
                .HasForeignKey(d => d.MaNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_matkhaudatlai_nguoidung");
        });

        modelBuilder.Entity<MauSanPham>(entity =>
        {
            entity.HasKey(e => e.MaMauSanPham).HasName("PK__mau_san___474DFE164D3D4A33");

            entity.ToTable("mau_san_pham");

            entity.HasIndex(e => e.MaDanhMuc, "IX_mausanpham_danhmuc");

            entity.Property(e => e.MaMauSanPham).HasColumnName("ma_mau_san_pham");
            entity.Property(e => e.ChatLieu)
                .HasMaxLength(100)
                .HasColumnName("chat_lieu");
            entity.Property(e => e.DuongDanHinhAnh)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("duong_dan_hinh_anh");
            entity.Property(e => e.LoaiSanPham)
                .HasMaxLength(100)
                .HasColumnName("loai_san_pham");
            entity.Property(e => e.MauSac)
                .HasMaxLength(100)
                .HasColumnName("mau_sac");
            entity.Property(e => e.MaCodeTenSp)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ma_code_ten_sp");
            entity.Property(e => e.MaDanhMuc).HasColumnName("ma_danh_muc");
            entity.Property(e => e.MoTa).HasColumnName("mo_ta");
            entity.Property(e => e.NhanHieu)
                .HasMaxLength(100)
                .HasColumnName("nhan_hieu");
            entity.Property(e => e.TenSanPham)
                .HasMaxLength(150)
                .HasColumnName("ten_san_pham");

            entity.HasOne(d => d.MaDanhMucNavigation).WithMany(p => p.MauSanPhams)
                .HasForeignKey(d => d.MaDanhMuc)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mausp_danhmuc");
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.HasKey(e => e.MaNguoiDung).HasName("PK__nguoi_du__19C32CF72B702042");

            entity.ToTable("nguoi_dung");

            entity.HasIndex(e => e.MaVaiTro, "IX_nguoidung_mavaitro");

            entity.HasIndex(e => e.TenDangNhap, "UQ__nguoi_du__363698B3AE9535FA").IsUnique();

            entity.HasIndex(e => e.Email, "UX_nguoidung_email")
                .IsUnique()
                .HasFilter("([email] IS NOT NULL)");

            entity.HasIndex(e => e.Cccd, "UX_nguoidung_cccd")
                .IsUnique()
                .HasFilter("([cccd] IS NOT NULL)");

            entity.Property(e => e.MaNguoiDung).HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.AnhThe)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("anh_the");
            entity.Property(e => e.Cccd)
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasColumnName("cccd");
            entity.Property(e => e.ChiNhanhNganHang)
                .HasMaxLength(100)
                .HasColumnName("chi_nhanh_ngan_hang");
            entity.Property(e => e.StkNganHang)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("stk_ngan_hang");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.GioiTinh)
                .HasMaxLength(10)
                .HasColumnName("gioi_tinh");
            entity.Property(e => e.HoTen)
                .HasMaxLength(100)
                .HasColumnName("ho_ten");
            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.MatKhauMaHoa)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("mat_khau_ma_hoa");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())", "DF_nguoidung_ngaycapnhat40")
                .HasColumnName("ngay_cap_nhat");
            entity.Property(e => e.NgaySinh).HasColumnName("ngay_sinh");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())", "DF_nguoidung_ngaytao40")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.PhaiDoiMatKhau).HasColumnName("phai_doi_mat_khau");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("so_dien_thoai");
            entity.Property(e => e.TenDangNhap)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ten_dang_nhap");
            entity.Property(e => e.ThuongTru)
                .HasMaxLength(255)
                .HasColumnName("thuong_tru");
            entity.Property(e => e.TrangThaiHoatDong)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai_hoat_dong");

            entity.HasOne(d => d.MaVaiTroNavigation).WithMany(p => p.NguoiDungs)
                .HasForeignKey(d => d.MaVaiTro)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_nguoidung_vaitro");
        });

        modelBuilder.Entity<NhaCungCap>(entity =>
        {
            entity.HasKey(e => e.MaNhaCungCap).HasName("PK__nha_cung__9B34E6DB46768B47");

            entity.ToTable("nha_cung_cap");

            entity.HasIndex(e => e.MaCodeNcc, "UQ__nha_cung__028FC07AAFEDB8C8").IsUnique();

            entity.Property(e => e.MaNhaCungCap).HasColumnName("ma_nha_cung_cap");
            entity.Property(e => e.DiaChi)
                .HasMaxLength(255)
                .HasColumnName("dia_chi");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.MaCodeNcc)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("ma_code_ncc");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.SoDienThoai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("so_dien_thoai");
            entity.Property(e => e.TenNhaCungCap)
                .HasMaxLength(150)
                .HasColumnName("ten_nha_cung_cap");
            entity.Property(e => e.TrangThaiHoatDong)
                .HasDefaultValue(true)
                .HasColumnName("trang_thai_hoat_dong");
        });

        modelBuilder.Entity<NhatKyThayDoiNguoiDung>(entity =>
        {
            entity.HasKey(e => e.MaNhatKy).HasName("PK__nhat_ky___DBCACAC1A1E48BE2");

            entity.ToTable("nhat_ky_thay_doi_nguoi_dung");

            entity.HasIndex(e => e.MaNguoiThucHien, "IX_nhatkynd_nguoithuchien");

            entity.HasIndex(e => new { e.MaNguoiDung, e.ThoiGian }, "IX_nhatkynguoidung_doituong").IsDescending(false, true);

            entity.Property(e => e.MaNhatKy).HasColumnName("ma_nhat_ky");
            entity.Property(e => e.GiaTriCu)
                .HasMaxLength(255)
                .HasColumnName("gia_tri_cu");
            entity.Property(e => e.GiaTriMoi)
                .HasMaxLength(255)
                .HasColumnName("gia_tri_moi");
            entity.Property(e => e.MaNguoiDung).HasColumnName("ma_nguoi_dung");
            entity.Property(e => e.MaNguoiThucHien).HasColumnName("ma_nguoi_thuc_hien");
            entity.Property(e => e.TenTruong)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ten_truong");
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())", "DF_nknd_thoigian40")
                .HasColumnName("thoi_gian");

            entity.HasOne(d => d.MaNguoiDungNavigation).WithMany(p => p.NhatKyThayDoiNguoiDungMaNguoiDungNavigations)
                .HasForeignKey(d => d.MaNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_nhatkynguoidung_doituong");

            entity.HasOne(d => d.MaNguoiThucHienNavigation).WithMany(p => p.NhatKyThayDoiNguoiDungMaNguoiThucHienNavigations)
                .HasForeignKey(d => d.MaNguoiThucHien)
                .HasConstraintName("FK_nhatkynguoidung_nguoithuchien");
        });

        modelBuilder.Entity<NhatKyTonKho>(entity =>
        {
            entity.HasKey(e => e.MaNhatKy).HasName("PK__nhat_ky___DBCACAC128B317CF");

            entity.ToTable("nhat_ky_ton_kho");

            entity.HasIndex(e => e.MaNguoiThucHien, "IX_nhatky_nguoithuchien");

            entity.HasIndex(e => new { e.MaSku, e.ThoiGian }, "IX_nhatky_sku_thoigian");

            entity.Property(e => e.MaNhatKy).HasColumnName("ma_nhat_ky");
            entity.Property(e => e.LoaiBienDong)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("loai_bien_dong");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaKeHang).HasColumnName("ma_ke_hang");
            entity.Property(e => e.MaNguoiThucHien).HasColumnName("ma_nguoi_thuc_hien");
            entity.Property(e => e.MaPhieuNhap).HasColumnName("ma_phieu_nhap");
            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.SoLuongThayDoi).HasColumnName("so_luong_thay_doi");
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("thoi_gian");

            entity.HasOne(d => d.MaNguoiThucHienNavigation).WithMany(p => p.NhatKyTonKhos)
                .HasForeignKey(d => d.MaNguoiThucHien)
                .HasConstraintName("FK_nhatky_nguoidung");

            entity.HasOne(d => d.MaSkuNavigation).WithMany(p => p.NhatKyTonKhos)
                .HasForeignKey(d => d.MaSku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_nhatky_sku");
        });

        modelBuilder.Entity<PhanBoXuatKhoTheoKe>(entity =>
        {
            entity.HasKey(e => e.MaPhanBo).HasName("PK__phan_bo___18E169AF104BC223");

            entity.ToTable("phan_bo_xuat_kho_theo_ke");

            entity.HasIndex(e => e.MaDonHang, "IX_phanbo_donhang");

            entity.HasIndex(e => new { e.MaSku, e.MaKeHang }, "IX_phanbo_sku_kehang");

            entity.Property(e => e.MaPhanBo).HasColumnName("ma_phan_bo");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaKeHang).HasColumnName("ma_ke_hang");
            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.SoLuong).HasColumnName("so_luong");

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.PhanBoXuatKhoTheoKes)
                .HasForeignKey(d => d.MaDonHang)
                .HasConstraintName("FK_phanbo_donhang");

            entity.HasOne(d => d.MaKeHangNavigation).WithMany(p => p.PhanBoXuatKhoTheoKes)
                .HasForeignKey(d => d.MaKeHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_phanbo_kehang");

            entity.HasOne(d => d.MaSkuNavigation).WithMany(p => p.PhanBoXuatKhoTheoKes)
                .HasForeignKey(d => d.MaSku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_phanbo_sku");
        });

        modelBuilder.Entity<PhieuNhapKho>(entity =>
        {
            entity.HasKey(e => e.MaPhieuNhap).HasName("PK__phieu_nh__F1DDD5C334A42B04");

            entity.ToTable("phieu_nhap_kho");

            entity.HasIndex(e => e.MaNhaCungCap, "IX_phieunhap_ncc");

            entity.HasIndex(e => e.NguoiTao, "IX_phieunhap_nguoitao");

            entity.HasIndex(e => e.MaCodePhieuNhap, "UQ__phieu_nh__269114F877DED7E8").IsUnique();

            entity.Property(e => e.MaPhieuNhap).HasColumnName("ma_phieu_nhap");
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
            entity.Property(e => e.MaCodePhieuNhap)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("ma_code_phieu_nhap");
            entity.Property(e => e.MaNhaCungCap).HasColumnName("ma_nha_cung_cap");
            entity.Property(e => e.NgayNhap)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_nhap");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.NguoiTao).HasColumnName("nguoi_tao");
            entity.Property(e => e.TongTienHang)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("tong_tien_hang");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaNhaCungCapNavigation).WithMany(p => p.PhieuNhapKhos)
                .HasForeignKey(d => d.MaNhaCungCap)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_phieunhap_ncc");

            entity.HasOne(d => d.NguoiTaoNavigation).WithMany(p => p.PhieuNhapKhos)
                .HasForeignKey(d => d.NguoiTao)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_phieunhap_nguoidung");
        });

        modelBuilder.Entity<LoHang>(entity =>
        {
            entity.HasKey(e => e.MaLoHang);

            entity.ToTable("lo_hang");

            entity.HasIndex(e => e.MaCodeLoHang).IsUnique();

            entity.Property(e => e.MaLoHang).HasColumnName("ma_lo_hang");
            entity.Property(e => e.MaCodeLoHang)
                .HasMaxLength(50)
                .HasColumnName("ma_code_lo_hang");
            entity.Property(e => e.NgayLoHang).HasColumnName("ngay_lo_hang");
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
        });

        modelBuilder.Entity<SanPhamSku>(entity =>
        {
            entity.HasKey(e => e.MaSku).HasName("PK__san_pham__085E78859F938883");

            entity.ToTable("san_pham_sku");

            entity.HasIndex(e => e.MaMauSanPham, "IX_sku_mausanpham");

            entity.HasIndex(e => e.TrangThaiVongDoi, "IX_sku_trangthai");

            entity.HasIndex(e => e.MaCodeSku, "UQ__san_pham__024DD2CC0A9B7AE9").IsUnique();

            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.GiaNiemYet)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("gia_niem_yet");
            entity.Property(e => e.MaCodeSku)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ma_code_sku");
            entity.Property(e => e.MaMauSanPham).HasColumnName("ma_mau_san_pham");
            entity.Property(e => e.MaLoHang).HasColumnName("ma_lo_hang");
            entity.Property(e => e.SoLuong).HasColumnName("so_luong");
            entity.Property(e => e.TrangThaiVongDoi)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("READY")
                .HasColumnName("trang_thai_vong_doi");

            entity.HasOne(d => d.MaMauSanPhamNavigation).WithMany(p => p.SanPhamSkus)
                .HasForeignKey(d => d.MaMauSanPham)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_sku_mausp");

            entity.HasOne(d => d.MaLoHangNavigation).WithMany(p => p.SanPhamSkus)
                .HasForeignKey(d => d.MaLoHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_sku_lohang");
        });

        modelBuilder.Entity<TonKhoKeHang>(entity =>
        {
            entity.HasKey(e => new { e.MaSku, e.MaKeHang }).HasName("PK_tonkhokehang");

            entity.ToTable("ton_kho_ke_hang");

            entity.HasIndex(e => e.MaKeHang, "IX_tonkhokehang_kehang");

            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.MaKeHang).HasColumnName("ma_ke_hang");
            entity.Property(e => e.SoLuong).HasColumnName("so_luong");

            entity.HasOne(d => d.MaKeHangNavigation).WithMany(p => p.TonKhoKeHangs)
                .HasForeignKey(d => d.MaKeHang)
                .HasConstraintName("FK_tonkhokehang_kehang");

            entity.HasOne(d => d.MaSkuNavigation).WithMany(p => p.TonKhoKeHangs)
                .HasForeignKey(d => d.MaSku)
                .HasConstraintName("FK_tonkhokehang_sku");
        });

        modelBuilder.Entity<TonKhoKhu>(entity =>
        {
            entity.HasKey(e => new { e.MaSku, e.MaKhuVuc }).HasName("PK_tonkhokhu");

            entity.ToTable("ton_kho_khu");

            entity.HasIndex(e => e.MaKhuVuc, "IX_tonkhokhu_khuvuc");

            entity.Property(e => e.MaSku).HasColumnName("ma_sku");
            entity.Property(e => e.MaKhuVuc).HasColumnName("ma_khu_vuc");
            entity.Property(e => e.SoLuong).HasColumnName("so_luong");

            entity.HasOne(d => d.MaKhuVucNavigation).WithMany(p => p.TonKhoKhus)
                .HasForeignKey(d => d.MaKhuVuc)
                .HasConstraintName("FK_tonkhokhu_khuvuc");

            entity.HasOne(d => d.MaSkuNavigation).WithMany(p => p.TonKhoKhus)
                .HasForeignKey(d => d.MaSku)
                .HasConstraintName("FK_tonkhokhu_sku");
        });

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.HasKey(e => e.MaVaiTro).HasName("PK__vai_tro__4AE1754DB0412C42");

            entity.ToTable("vai_tro");

            entity.HasIndex(e => e.TenVaiTro, "UQ__vai_tro__E59C937CA9610303").IsUnique();

            entity.Property(e => e.MaVaiTro).HasColumnName("ma_vai_tro");
            entity.Property(e => e.CoQuyenHuyDon).HasColumnName("co_quyen_huy_don");
            entity.Property(e => e.DuocNhanVanDon).HasColumnName("duoc_nhan_van_don");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.TenVaiTro)
                .HasMaxLength(100)
                .HasColumnName("ten_vai_tro");
        });

        modelBuilder.Entity<VanDon>(entity =>
        {
            entity.HasKey(e => e.MaVanDon).HasName("PK__van_don__3B08393BF73CCB6D");

            entity.ToTable("van_don", tb =>
            {
                tb.HasTrigger("trg_vandon_dongbo_don_hang");
                tb.HasTrigger("trg_vandon_kiemtra_don_hang");
                tb.HasTrigger("trg_vandon_kiemtra_nhanvien_giao_hang");
            });

            entity.HasIndex(e => e.NgayTao, "IX_vandon_ngaytao");

            entity.HasIndex(e => e.MaNhanVienGiaoHang, "IX_vandon_nhanvien_giao_hang");

            entity.HasIndex(e => e.TrangThai, "IX_vandon_trangthai");

            entity.HasIndex(e => e.MaDonHang, "UQ__van_don__0246C5EB9E5C2FA7").IsUnique();

            entity.HasIndex(e => e.MaCodeVanDon, "UQ__van_don__5D7F6D00BDD15E91").IsUnique();

            entity.Property(e => e.MaVanDon).HasColumnName("ma_van_don");
            entity.Property(e => e.GhiChu).HasColumnName("ghi_chu");
            entity.Property(e => e.MaCodeVanDon)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValueSql("(concat('VD-',CONVERT([char](8),getdate(),(112)),'-',right('000000'+CONVERT([varchar](20),NEXT VALUE FOR [seq_ma_code_van_don]),(6))))", "DF_vandon_macode")
                .HasColumnName("ma_code_van_don");
            entity.Property(e => e.MaDonHang).HasColumnName("ma_don_hang");
            entity.Property(e => e.MaNhanVienGiaoHang).HasColumnName("ma_nhan_vien_giao_hang");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_cap_nhat");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("ngay_tao");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("CHUA_HOAN_THANH")
                .HasColumnName("trang_thai");

            entity.HasOne(d => d.MaDonHangNavigation).WithOne(p => p.VanDon)
                .HasForeignKey<VanDon>(d => d.MaDonHang)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_vandon_donhang");

            entity.HasOne(d => d.MaNhanVienGiaoHangNavigation).WithMany(p => p.VanDons)
                .HasForeignKey(d => d.MaNhanVienGiaoHang)
                .HasConstraintName("FK_vandon_nhanviengiaohang");
        });

        modelBuilder.HasSequence("seq_ma_code_don_hang");
        modelBuilder.HasSequence("seq_ma_code_van_don");

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}