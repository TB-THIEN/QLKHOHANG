/* ==========================================================================
   DO_AN_QLK - Schema quản lý kho (bản gọn, trạng thái cuối)
   - san_pham_sku chỉ có 1 cột giá: gia_tien (không còn gia_von / gia_niem_yet)
   - Không còn bảng/cột/SP đã bỏ: bao_cao, kiểm kê, ma_vach, gia_thi_truong,
     khuyen_mai_san_pham, các view cũ
   - Chạy tuần tự từ trên xuống; mỗi mục đánh số 1..28
   ========================================================================== */

USE master;
GO
IF DB_ID(N'DO_AN_QLK') IS NOT NULL
BEGIN
    ALTER DATABASE DO_AN_QLK SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE DO_AN_QLK;
END
GO
CREATE DATABASE DO_AN_QLK;
GO
USE DO_AN_QLK;
GO

-- Index lọc + cột PERSISTED bắt buộc 2 SET này bật.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- 1. DANH MỤC & SẢN PHẨM MASTER
CREATE TABLE danh_muc (
    ma_danh_muc INT IDENTITY(1,1) PRIMARY KEY,             -- Mã danh mục (khóa chính)
    ma_code_danh_muc VARCHAR(20) NOT NULL UNIQUE,          -- Mã code danh mục (vd: HOU, FUR...) - dùng để sinh mã SKU
    ten_danh_muc NVARCHAR(100) NOT NULL UNIQUE             -- Tên danh mục hiển thị
);
GO

-- Dữ liệu chuẩn: các nhóm hàng + 1 nhóm mặc định.
INSERT INTO danh_muc (ma_code_danh_muc, ten_danh_muc) VALUES
    ('HOU', N'Đồ dùng gia dụng'),
    ('ELE', N'Điện máy'),
    ('HAP', N'Điện gia dụng'),
    ('IND', N'Thiết bị công nghiệp'),
    ('FUR', N'Nội thất'),
    ('DEC', N'Trang trí'),
    ('SCR', N'Phế liệu'),
    ('SP',  N'Chưa phân loại');
GO

CREATE TABLE mau_san_pham (
    ma_mau_san_pham INT IDENTITY(1,1) PRIMARY KEY,         -- Mã sản phẩm gốc (khóa chính)
    ma_danh_muc INT NOT NULL,                              -- Thuộc danh mục nào
    ten_san_pham NVARCHAR(150) NOT NULL,                   -- Tên sản phẩm
    ma_code_ten_sp VARCHAR(50),                            -- Mã viết tắt tên sản phẩm (vd: DIA, GHE)
    mo_ta NVARCHAR(MAX),                                   -- Mô tả chi tiết sản phẩm
    duong_dan_hinh_anh VARCHAR(255),                       -- Đường dẫn ảnh sản phẩm
    chat_lieu NVARCHAR(100),                               -- Chất liệu (nhập tay, vd: Gỗ, Inox, Vải)
    mau_sac NVARCHAR(100),                                 -- Màu sắc (nhập tay, vd: Trắng, Đen, Nâu)
    nhan_hieu NVARCHAR(100),                               -- Nhãn hiệu (nhập tay)
    loai_san_pham NVARCHAR(100),                           -- Loại sản phẩm (nhập tay)
    CONSTRAINT FK_mausp_danhmuc FOREIGN KEY (ma_danh_muc) REFERENCES danh_muc(ma_danh_muc)
);
GO

-- 1b. NHIỀU ẢNH CHO MỘT SẢN PHẨM
-- Ảnh có thứ tự nhỏ nhất là ảnh đại diện; mau_san_pham.duong_dan_hinh_anh giữ bản sao
-- của ảnh đại diện để các màn hình cũ (danh sách, tồn kho, khu hàng) không phải sửa.
CREATE TABLE hinh_anh_san_pham (
    ma_hinh_anh INT IDENTITY(1,1) PRIMARY KEY,             -- Mã ảnh (khóa chính)
    ma_mau_san_pham INT NOT NULL,                          -- Thuộc sản phẩm gốc nào
    duong_dan VARCHAR(255) NOT NULL,                       -- Đường dẫn ảnh trong wwwroot
    thu_tu INT NOT NULL DEFAULT 0,                         -- Thứ tự hiển thị, 0 = ảnh đại diện
    CONSTRAINT FK_hinhanh_mausp FOREIGN KEY (ma_mau_san_pham)
        REFERENCES mau_san_pham(ma_mau_san_pham) ON DELETE CASCADE
);
GO
CREATE INDEX IX_hinhanh_mausp ON hinh_anh_san_pham(ma_mau_san_pham, thu_tu);
GO

-- 2. KHU VỰC KHO
CREATE TABLE khu_vuc_kho (
    ma_khu_vuc INT IDENTITY(1,1) PRIMARY KEY,              -- Mã khu vực (khóa chính)
    ten_khu_vuc NVARCHAR(50) NOT NULL,                     -- Tên khu vực (vd: Khu Sofa, Khu trưng bày)
    loai_khu_vuc VARCHAR(20) NOT NULL DEFAULT 'STORAGE'
        CHECK (loai_khu_vuc IN ('STORAGE', 'DISPLAY', 'SHIPPING')),  -- Loại khu: lưu trữ/trưng bày/xuất hàng
    dien_tich_m2 DECIMAL(10,2) DEFAULT 0,                  -- Diện tích (m2), phục vụ KPI hiệu suất sử dụng kho
    trang_thai_hoat_dong BIT DEFAULT 1                     -- 1 = đang dùng, 0 = ngừng dùng
);
GO

-- 3. SKU & TỒN KHO
CREATE TABLE san_pham_sku (
    ma_sku INT IDENTITY(1,1) PRIMARY KEY,                  -- Mã SKU (khóa chính)
    ma_code_sku VARCHAR(50) NOT NULL UNIQUE,               -- Mã code SKU đầy đủ (vd: HOU-DIA-CERWHITE-2603001)
    ma_mau_san_pham INT NOT NULL,                          -- Thuộc sản phẩm gốc nào
    gia_tien DECIMAL(15,2) NOT NULL DEFAULT 0,             -- Giá của SKU (dùng làm đơn giá bán)
    so_luong INT NOT NULL DEFAULT 0 CHECK (so_luong >= 0), -- Số lượng tồn hiện tại
    -- chặn giá bị ghi âm
    CONSTRAINT CK_sku_gia CHECK (gia_tien >= 0),
    trang_thai_vong_doi VARCHAR(20) DEFAULT 'READY'
        CHECK (trang_thai_vong_doi IN ('READY', 'DISPLAY', 'SOLD', 'DISPOSED')), -- Trạng thái vòng đời sản phẩm
    CONSTRAINT FK_sku_mausp FOREIGN KEY (ma_mau_san_pham) REFERENCES mau_san_pham(ma_mau_san_pham)
);
GO

-- 4. KHÁCH HÀNG
CREATE TABLE khach_hang (
    ma_khach_hang INT IDENTITY(1,1) PRIMARY KEY,           -- Mã khách hàng (khóa chính)
    ten_khach_hang NVARCHAR(150) NOT NULL,                 -- Tên khách hàng
    so_dien_thoai VARCHAR(20),                             -- Số điện thoại
    dia_chi NVARCHAR(255)                                  -- Địa chỉ
);
GO

-- 5. VAI TRÒ (PHÂN QUYỀN)
CREATE TABLE vai_tro (
    ma_vai_tro INT IDENTITY(1,1) PRIMARY KEY,              -- Mã vai trò (khóa chính)
    ten_vai_tro NVARCHAR(100) NOT NULL UNIQUE,             -- Tên vai trò (hiển thị + autocomplete)
    co_quyen_huy_don BIT NOT NULL DEFAULT 0,               -- 1 = được hủy đơn hàng
    duoc_nhan_van_don BIT NOT NULL DEFAULT 0,              -- 1 = được nhận vận đơn (giao hàng)
    ngay_tao DATETIME2 NOT NULL DEFAULT GETDATE()          -- Ngày tạo bản ghi
);
GO

-- Seed 4 vai trò mặc định.
INSERT INTO vai_tro (ten_vai_tro, co_quyen_huy_don, duoc_nhan_van_don) VALUES
    (N'Quản lý', 1, 0),
    (N'Nhân viên giao hàng', 0, 1),
    (N'Nhân viên kho', 0, 0),
    (N'Nhân viên bán hàng', 0, 0);
GO

-- 6. NHÂN VIÊN
CREATE TABLE nguoi_dung (
    ma_nguoi_dung INT IDENTITY(1,1) PRIMARY KEY,           -- Mã người dùng (khóa chính)
    ten_dang_nhap VARCHAR(50) NOT NULL UNIQUE,             -- Tên đăng nhập
    mat_khau_ma_hoa VARCHAR(255) NOT NULL,                 -- Mật khẩu đã mã hóa
    ho_ten NVARCHAR(100) NOT NULL,                         -- Họ tên nhân viên
    ma_vai_tro INT NOT NULL,                               -- Vai trò/phân quyền (khóa ngoại tới bảng vai_tro)
    trang_thai_hoat_dong BIT DEFAULT 1,                    -- 1 = đang làm việc
    thuong_tru NVARCHAR(255) NULL,                         -- Địa chỉ thường trú
    ngay_tao DATETIME DEFAULT GETDATE(),                   -- Ngày tạo tài khoản
    ngay_cap_nhat DATETIME DEFAULT GETDATE(),              -- Ngày cập nhật gần nhất
    CONSTRAINT FK_nguoidung_vaitro FOREIGN KEY (ma_vai_tro) REFERENCES vai_tro(ma_vai_tro)
);
GO

CREATE INDEX IX_nguoidung_mavaitro ON nguoi_dung(ma_vai_tro);
GO

-- 7. ĐƠN HÀNG
-- Đã bỏ nguon_don; thông tin khách hàng được lưu qua ma_khach_hang.
-- Mã đơn do CSDL tự sinh, người dùng không nhập/chỉnh sửa mã này.
CREATE SEQUENCE seq_ma_code_don_hang
    AS BIGINT
    START WITH 1
    INCREMENT BY 1;
GO

CREATE TABLE don_hang (
    ma_don_hang INT IDENTITY(1,1) PRIMARY KEY,             -- Mã đơn hàng (khóa chính)
    ma_code_don_hang VARCHAR(30) NOT NULL
        CONSTRAINT DF_donhang_macode DEFAULT (
            CONCAT('DH-', CONVERT(CHAR(8), GETDATE(), 112), '-',
                   RIGHT('000000' + CONVERT(VARCHAR(20), NEXT VALUE FOR seq_ma_code_don_hang), 6))
        ) UNIQUE,                                          -- Mã đơn tự sinh, dùng để hiển thị
    ma_khach_hang INT NULL,                                -- Khách hàng mua đơn (có thể để trống nếu khách vãng lai)
    ma_nhan_vien INT,                                      -- Nhân viên phụ trách đơn
    ma_khuyen_mai INT NULL,                                -- Chương trình khuyến mãi áp dụng (nếu có)
    ngay_dat_hang DATETIME2 DEFAULT GETDATE(),             -- Ngày giờ tạo đơn
    -- Bỏ trạng thái DRAFT (đơn luôn tạo thẳng CONFIRMED).
    trang_thai VARCHAR(20) DEFAULT 'CONFIRMED'
        CHECK (trang_thai IN ('CONFIRMED', 'COMPLETED', 'CANCELLED')), -- Trạng thái đơn hàng
    trang_thai_thanh_toan VARCHAR(20) DEFAULT 'UNPAID'
        CHECK (trang_thai_thanh_toan IN ('UNPAID', 'PAID')), -- Đã thanh toán hay chưa (chỉ hủy được khi UNPAID)
    hinh_thuc_nhan_hang VARCHAR(20) NOT NULL DEFAULT 'TAI_SHOP'
        CHECK (hinh_thuc_nhan_hang IN ('TAI_SHOP', 'GIAO_HANG')), -- Nhận tại kho/cửa hàng hoặc giao hàng
    tong_tien_hang DECIMAL(15,2) NOT NULL DEFAULT 0,       -- Tổng tiền hàng trước giảm giá
    tien_giam_gia DECIMAL(15,2) NOT NULL DEFAULT 0,        -- Số tiền được giảm giá
    phi_van_chuyen DECIMAL(15,2) NOT NULL DEFAULT 0,       -- Phí vận chuyển
    thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,           -- Tổng tiền thanh toán cuối cùng (trigger tự tính)
    ghi_chu NVARCHAR(MAX),                                 -- Ghi chú đơn hàng
    ngay_huy DATETIME NULL,                                -- Thời điểm hủy đơn (nếu có)
    ly_do_huy NVARCHAR(MAX) NULL,                          -- Lý do hủy đơn (nếu có)
    ngay_tao DATETIME DEFAULT GETDATE(),                   -- Ngày tạo bản ghi
    ngay_cap_nhat DATETIME DEFAULT GETDATE(),              -- Ngày cập nhật gần nhất
    CONSTRAINT FK_donhang_nguoidung FOREIGN KEY (ma_nhan_vien) REFERENCES nguoi_dung(ma_nguoi_dung) ON DELETE SET NULL,
    CONSTRAINT FK_donhang_khachhang FOREIGN KEY (ma_khach_hang) REFERENCES khach_hang(ma_khach_hang) ON DELETE SET NULL,
    CONSTRAINT CK_donhang_huy_chuathanhtoan
        CHECK (NOT (trang_thai = 'CANCELLED' AND trang_thai_thanh_toan = 'PAID')),
    -- chặn các cột tiền bị ghi âm
    CONSTRAINT CK_donhang_tien CHECK (
        tong_tien_hang >= 0 AND tien_giam_gia >= 0 AND
        phi_van_chuyen >= 0 AND thanh_tien >= 0
    ),
    -- Giảm giá không vượt tổng tiền hàng.
    CONSTRAINT CK_donhang_giamgia_hople CHECK (tien_giam_gia <= tong_tien_hang)
    -- FK ma_khuyen_mai thêm ở mục 11 (bảng khuyen_mai tạo sau).
);
GO

CREATE TABLE chi_tiet_don_hang (
    ma_chi_tiet_don INT IDENTITY(1,1) PRIMARY KEY,         -- Mã chi tiết đơn (khóa chính)
    ma_don_hang INT NOT NULL,                              -- Thuộc đơn hàng nào
    ma_sku INT NOT NULL,                                   -- Bán SKU nào
    so_luong INT NOT NULL DEFAULT 1 CHECK (so_luong > 0),  -- Số lượng bán
    don_gia DECIMAL(15,2) NOT NULL DEFAULT 0,              -- Giá bán thực tế mỗi đơn vị
    thanh_tien_dong DECIMAL(15,2) NOT NULL DEFAULT 0,      -- Thành tiền của dòng hàng này
    CONSTRAINT FK_cthd_donhang FOREIGN KEY (ma_don_hang) REFERENCES don_hang(ma_don_hang) ON DELETE CASCADE,
    CONSTRAINT FK_cthd_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku),
    -- chặn các cột tiền bị ghi âm
    CONSTRAINT CK_cthd_tien CHECK (don_gia >= 0 AND thanh_tien_dong >= 0)
);
GO


-- 8. KHUYẾN MÃI
CREATE TABLE khuyen_mai (
    ma_khuyen_mai INT IDENTITY(1,1) PRIMARY KEY,           -- Mã khuyến mãi (khóa chính)
    ma_code_khuyen_mai VARCHAR(30) NOT NULL UNIQUE,        -- Mã code chương trình khuyến mãi
    ten_khuyen_mai NVARCHAR(150) NOT NULL,                 -- Tên đợt khuyến mãi
    loai_giam_gia VARCHAR(20) DEFAULT 'PERCENT'
        CHECK (loai_giam_gia IN ('PERCENT', 'FIXED_AMOUNT')), -- Giảm theo % hay số tiền cố định
    gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0,         -- Giá trị giảm (% hoặc VNĐ)
    gia_tri_don_toi_thieu DECIMAL(15,2) DEFAULT 0,         -- Giá trị đơn hàng tối thiểu để áp dụng
    so_lan_su_dung_toi_da INT NULL,                        -- NULL = không giới hạn; không giới hạn theo từng khách
    ngay_bat_dau DATETIME2 NOT NULL,                       -- Ngày giờ bắt đầu khuyến mãi
    ngay_ket_thuc DATETIME2 NOT NULL,                      -- Ngày giờ kết thúc khuyến mãi
    trang_thai_hoat_dong BIT DEFAULT 1,                    -- 1 = đang áp dụng
    ngay_tao DATETIME2 DEFAULT GETDATE(),                  -- Ngày tạo bản ghi
    -- chặn giá trị giảm giá / giá trị đơn tối thiểu bị âm
    CONSTRAINT CK_khuyenmai_giatri CHECK (gia_tri_giam >= 0 AND gia_tri_don_toi_thieu >= 0),
    -- nếu giảm theo %, giá trị phải trong khoảng 0-100
    CONSTRAINT CK_khuyenmai_percent CHECK (
        loai_giam_gia <> 'PERCENT' OR (gia_tri_giam >= 0 AND gia_tri_giam <= 100)
    ),
    CONSTRAINT CK_khuyenmai_gioihan CHECK (so_lan_su_dung_toi_da IS NULL OR so_lan_su_dung_toi_da > 0)
);
GO


-- 9. NHÀ CUNG CẤP
-- (Đã bỏ tag, website, mã số thuế, số fax theo yêu cầu giao diện)
CREATE TABLE nha_cung_cap (
    ma_nha_cung_cap INT IDENTITY(1,1) PRIMARY KEY,         -- Mã nhà cung cấp (khóa chính)
    ma_code_ncc VARCHAR(30) NOT NULL UNIQUE,               -- Mã code nhà cung cấp (tự sinh NCC-001...)
    ten_nha_cung_cap NVARCHAR(150) NOT NULL,               -- Tên nhà cung cấp
    so_dien_thoai VARCHAR(20),                             -- Số điện thoại liên hệ
    email VARCHAR(255),                                    -- Email liên hệ
    dia_chi NVARCHAR(255),                                 -- Địa chỉ nhà cung cấp
    trang_thai_hoat_dong BIT DEFAULT 1,                    -- 1 = đang hợp tác
    ngay_tao DATETIME2 DEFAULT GETDATE()                   -- Ngày tạo bản ghi
);
GO

-- 10. PHIẾU NHẬP KHO
CREATE TABLE phieu_nhap_kho (
    ma_phieu_nhap INT IDENTITY(1,1) PRIMARY KEY,           -- Mã phiếu nhập (khóa chính)
    ma_code_phieu_nhap VARCHAR(30) NOT NULL UNIQUE,        -- Mã code phiếu nhập / số ASN
    ma_nha_cung_cap INT NULL,                              -- Nhập từ nhà cung cấp nào
    nguoi_tao INT NULL,                                    -- Người lập phiếu nhập
    ngay_nhap DATETIME2 DEFAULT GETDATE(),                 -- Ngày giờ thực nhập kho
    trang_thai VARCHAR(20) DEFAULT 'PENDING'
        CHECK (trang_thai IN ('PENDING', 'RECEIVING', 'COMPLETED', 'CANCELLED')), -- Trạng thái phiếu nhập
    ghi_chu NVARCHAR(MAX) NULL,                            -- Ghi chú phiếu nhập
    ngay_tao DATETIME2 DEFAULT GETDATE(),                  -- Ngày tạo bản ghi
    tong_tien_hang DECIMAL(15,2) DEFAULT 0, -- Tổng giá trị tiền trị giá nhập kho (Tổng cộng từ các dòng chi tiết)


    CONSTRAINT FK_phieunhap_ncc FOREIGN KEY (ma_nha_cung_cap) REFERENCES nha_cung_cap(ma_nha_cung_cap) ON DELETE SET NULL,
    CONSTRAINT FK_phieunhap_nguoidung FOREIGN KEY (nguoi_tao) REFERENCES nguoi_dung(ma_nguoi_dung) ON DELETE SET NULL
);
GO

CREATE TABLE chi_tiet_phieu_nhap_kho (
    ma_chi_tiet_phieu_nhap INT IDENTITY(1,1) PRIMARY KEY,  -- Mã chi tiết phiếu nhập (khóa chính)
    ma_phieu_nhap INT NOT NULL,                            -- Thuộc phiếu nhập nào
    ma_sku INT NOT NULL,                                   -- Nhập SKU nào
    so_luong_ke_hoach INT NOT NULL DEFAULT 0,              -- Số lượng dự kiến nhập (theo ASN)
    so_luong_thuc_nhan INT NOT NULL DEFAULT 0,             -- Số lượng thực tế nhận được
    don_gia_nhap DECIMAL(15,2) DEFAULT 0,                  -- Đơn giá nhập
    thanh_tien AS (so_luong_thuc_nhan * don_gia_nhap) PERSISTED, -- Lưu thành tiền của phiếu đó (cột tính toán)
    CONSTRAINT FK_ctpn_phieunhap FOREIGN KEY (ma_phieu_nhap) REFERENCES phieu_nhap_kho(ma_phieu_nhap) ON DELETE CASCADE,
    CONSTRAINT FK_ctpn_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku),
    -- chặn số lượng/đơn giá bị ghi âm
    CONSTRAINT CK_ctpn_soluong CHECK (
        so_luong_ke_hoach >= 0 AND so_luong_thuc_nhan >= 0 AND don_gia_nhap >= 0
    )
);
GO

-- 11. LIÊN KẾT KHÓA NGOẠI KHUYẾN MÃI CHO ĐƠN HÀNG
-- Thêm sau vì bảng khuyen_mai được tạo sau bảng don_hang
ALTER TABLE don_hang
ADD CONSTRAINT FK_donhang_khuyenmai FOREIGN KEY (ma_khuyen_mai)
    REFERENCES khuyen_mai(ma_khuyen_mai) ON DELETE SET NULL;
GO

-- Ràng buộc bổ sung: ngày kết thúc khuyến mãi phải sau ngày bắt đầu
ALTER TABLE khuyen_mai
ADD CONSTRAINT CK_khuyenmai_thoigian CHECK (ngay_ket_thuc > ngay_bat_dau);
GO


-- Index cho cột FK / cột lọc-join (SQL Server không tự tạo).
CREATE INDEX IX_donhang_ngaydathang ON don_hang(ngay_dat_hang);              -- lọc báo cáo theo khoảng ngày
CREATE INDEX IX_donhang_makhachhang ON don_hang(ma_khach_hang);
CREATE INDEX IX_donhang_manhanvien ON don_hang(ma_nhan_vien);

CREATE INDEX IX_mausanpham_danhmuc ON mau_san_pham(ma_danh_muc);             -- lọc/join sản phẩm theo danh mục

CREATE INDEX IX_cthd_madonhang ON chi_tiet_don_hang(ma_don_hang);            -- join lấy chi tiết theo đơn
CREATE INDEX IX_cthd_masku ON chi_tiet_don_hang(ma_sku);                     -- top sản phẩm bán chạy

CREATE INDEX IX_sku_trangthai ON san_pham_sku(trang_thai_vong_doi);          -- lọc SKU hết hàng/sẵn sàng
CREATE INDEX IX_sku_mausanpham ON san_pham_sku(ma_mau_san_pham);

CREATE INDEX IX_ctpn_phieunhap ON chi_tiet_phieu_nhap_kho(ma_phieu_nhap);
GO


-- TRIGGER

-- TRG 1: Tự tính lại thanh_tien mỗi khi don_hang được update
-- (tong_tien_hang, tien_giam_gia, phi_van_chuyen thay đổi)
CREATE TRIGGER trg_donhang_capnhat_thanhtien
ON don_hang
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    -- Chống đệ quy trigger.
    IF TRIGGER_NESTLEVEL(OBJECT_ID('trg_donhang_capnhat_thanhtien')) > 1 RETURN;

    UPDATE dh
    SET dh.thanh_tien = i.tong_tien_hang - i.tien_giam_gia + i.phi_van_chuyen,
        dh.ngay_cap_nhat = GETDATE()
    FROM don_hang dh
    INNER JOIN inserted i ON dh.ma_don_hang = i.ma_don_hang;
END;
GO

-- TRG 7: Chi tiết phiếu nhập đổi -> tính lại tổng tiền phiếu.
CREATE TRIGGER trg_phieunhap_capnhat_tongtien
ON chi_tiet_phieu_nhap_kho
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE pn
    SET pn.tong_tien_hang = ISNULL((
            SELECT SUM(ct.thanh_tien)
            FROM chi_tiet_phieu_nhap_kho ct
            WHERE ct.ma_phieu_nhap = pn.ma_phieu_nhap
        ), 0)
    FROM phieu_nhap_kho pn
    WHERE pn.ma_phieu_nhap IN (
        SELECT ma_phieu_nhap FROM inserted
        UNION
        SELECT ma_phieu_nhap FROM deleted
    );
END;
GO


-- 12. VẬN CHUYỂN: mỗi đơn GIAO_HANG có đúng 1 vận đơn.

CREATE SEQUENCE seq_ma_code_van_don
    AS BIGINT
    START WITH 1
    INCREMENT BY 1;
GO

CREATE TABLE van_don (
    ma_van_don INT IDENTITY(1,1) PRIMARY KEY,
    ma_code_van_don VARCHAR(30) NOT NULL
        CONSTRAINT DF_vandon_macode DEFAULT (
            CONCAT('VD-', CONVERT(CHAR(8), GETDATE(), 112), '-',
                   RIGHT('000000' + CONVERT(VARCHAR(20), NEXT VALUE FOR seq_ma_code_van_don), 6))
        ) UNIQUE,                                         -- Mã vận đơn tự sinh
    ma_don_hang INT NOT NULL UNIQUE,                       -- Mỗi đơn chỉ có một vận đơn
    ma_nhan_vien_giao_hang INT NULL,                       -- Nhân viên giao hàng phụ trách (NULL = chưa gán, chờ nhân viên kho gán sau)
    trang_thai VARCHAR(20) NOT NULL DEFAULT 'CHUA_HOAN_THANH'
        CHECK (trang_thai IN ('CHUA_HOAN_THANH', 'CHUA_GIAO', 'DA_GIAO')), -- Chưa hoàn thành / Chưa giao / Đã giao
    ghi_chu NVARCHAR(MAX) NULL,
    ngay_tao DATETIME2 NOT NULL DEFAULT GETDATE(),
    ngay_cap_nhat DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_vandon_donhang FOREIGN KEY (ma_don_hang)
        REFERENCES don_hang(ma_don_hang),
    CONSTRAINT FK_vandon_nhanviengiaohang FOREIGN KEY (ma_nhan_vien_giao_hang)
        REFERENCES nguoi_dung(ma_nguoi_dung)
);
GO

-- Chỉ nhân viên giao hàng đang hoạt động mới được phụ trách vận đơn.
CREATE TRIGGER trg_vandon_kiemtra_nhanvien_giao_hang
ON van_don
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF TRIGGER_NESTLEVEL(OBJECT_ID('trg_vandon_kiemtra_nhanvien_giao_hang')) > 1 RETURN;

    -- Bỏ qua vận đơn chưa gán người giao (NULL).
    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN nguoi_dung nd ON nd.ma_nguoi_dung = i.ma_nhan_vien_giao_hang
        INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
        WHERE i.ma_nhan_vien_giao_hang IS NOT NULL
          AND (vt.duoc_nhan_van_don = 0 OR nd.trang_thai_hoat_dong <> 1)
    )
    BEGIN
        RAISERROR (N'Người phụ trách phải là nhân viên giao hàng đang hoạt động.', 16, 1);
        RETURN;
    END
END;
GO

-- Chỉ lập vận đơn cho đơn GIAO_HANG chưa hủy.
CREATE TRIGGER trg_vandon_kiemtra_don_hang
ON van_don
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN don_hang dh ON dh.ma_don_hang = i.ma_don_hang
        WHERE dh.hinh_thuc_nhan_hang <> 'GIAO_HANG' OR dh.trang_thai = 'CANCELLED'
    )
    BEGIN
        RAISERROR (N'Chỉ có thể lập vận đơn cho đơn giao hàng chưa bị hủy.', 16, 1);
        RETURN;
    END
END;
GO

CREATE INDEX IX_vandon_nhanvien_giao_hang ON van_don(ma_nhan_vien_giao_hang);
CREATE INDEX IX_vandon_trangthai ON van_don(trang_thai);
CREATE INDEX IX_vandon_ngaytao ON van_don(ngay_tao);
GO

-- Đăng nhập: email định danh; OTP lưu băm, có hạn, dùng 1 lần.
ALTER TABLE nguoi_dung ADD email VARCHAR(255) NULL;
GO

CREATE UNIQUE INDEX UX_nguoidung_email ON nguoi_dung(email) WHERE email IS NOT NULL;
GO

CREATE TABLE mat_khau_dat_lai (
    ma_dat_lai INT IDENTITY(1,1) PRIMARY KEY,
    ma_nguoi_dung INT NOT NULL,                            -- Tài khoản yêu cầu đặt lại mật khẩu
    ma_xac_nhan_hash VARCHAR(255) NOT NULL,                -- Mã OTP đã băm (không lưu plaintext)
    ngay_tao DATETIME2 NOT NULL DEFAULT GETDATE(),
    ngay_het_han DATETIME2 NOT NULL,                       -- Mã chỉ dùng được trước thời điểm này
    da_su_dung BIT NOT NULL DEFAULT 0,                     -- 1 = mã đã được dùng, không dùng lại được
    CONSTRAINT FK_matkhaudatlai_nguoidung FOREIGN KEY (ma_nguoi_dung)
        REFERENCES nguoi_dung(ma_nguoi_dung)
);
GO

CREATE INDEX IX_matkhaudatlai_nguoidung ON mat_khau_dat_lai(ma_nguoi_dung, da_su_dung, ngay_het_han);
GO


CREATE PROCEDURE sp_TopSanPhamBanChay
    @bo_loc VARCHAR(10) = 'TAT_CA',   -- TAT_CA | NGAY | THANG | NAM | TUY_CHON
    @tu_ngay DATE = NULL,
    @den_ngay DATE = NULL,
    @top_n INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @bo_loc = 'NGAY' AND @tu_ngay IS NULL
        SELECT @tu_ngay = CAST(GETDATE() AS DATE), @den_ngay = CAST(GETDATE() AS DATE);
    ELSE IF @bo_loc = 'THANG' AND @tu_ngay IS NULL
        SELECT @tu_ngay = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1), @den_ngay = EOMONTH(GETDATE());
    ELSE IF @bo_loc = 'NAM' AND @tu_ngay IS NULL
        SELECT @tu_ngay = DATEFROMPARTS(YEAR(GETDATE()), 1, 1), @den_ngay = DATEFROMPARTS(YEAR(GETDATE()), 12, 31);
    ELSE IF @bo_loc = 'TAT_CA'
        SELECT @tu_ngay = NULL, @den_ngay = NULL;
    -- TUY_CHON: dùng nguyên @tu_ngay/@den_ngay truyền vào.
    -- Khoảng nửa-mở, không CAST cột ngày -> giữ index seek.
    DECLARE @den_ngay_ke_tiep DATETIME2 = DATEADD(DAY, 1, @den_ngay);

    SELECT TOP (@top_n)
        sp.ma_sku, sp.ma_code_sku, msp.ten_san_pham, msp.duong_dan_hinh_anh,
        SUM(ct.so_luong) AS tong_so_luong_ban, SUM(ct.thanh_tien_dong) AS tong_doanh_thu
    FROM chi_tiet_don_hang ct
    INNER JOIN don_hang dh ON dh.ma_don_hang = ct.ma_don_hang
    INNER JOIN san_pham_sku sp ON sp.ma_sku = ct.ma_sku
    INNER JOIN mau_san_pham msp ON msp.ma_mau_san_pham = sp.ma_mau_san_pham
    WHERE dh.trang_thai = 'COMPLETED'
      AND (@tu_ngay IS NULL OR dh.ngay_dat_hang >= @tu_ngay)
      AND (@den_ngay IS NULL OR dh.ngay_dat_hang < @den_ngay_ke_tiep)
    GROUP BY sp.ma_sku, sp.ma_code_sku, msp.ten_san_pham, msp.duong_dan_hinh_anh
    ORDER BY tong_so_luong_ban DESC;
END;
GO

-- 13. Cây Khu -> Kệ -> SKU: thêm bảng ke_hang.

-- Tên khu DUY NHẤT.
ALTER TABLE khu_vuc_kho
ADD CONSTRAINT UK_khuvuc_ten UNIQUE (ten_khu_vuc);
GO

-- Kệ hàng luôn thuộc về đúng 1 khu hàng.
CREATE TABLE ke_hang (
    ma_ke_hang INT IDENTITY(1,1) PRIMARY KEY,              -- Mã kệ hàng (khóa chính)
    ma_khu_vuc INT NOT NULL,                               -- Kệ này thuộc khu hàng nào
    ten_ke_hang NVARCHAR(50) NOT NULL,                     -- Tên kệ (vd: Kệ 1A, 1B...)
    ngay_tao DATETIME2 NOT NULL DEFAULT GETDATE(),         -- Ngày tạo bản ghi
    CONSTRAINT FK_kehang_khuvuc FOREIGN KEY (ma_khu_vuc)
        REFERENCES khu_vuc_kho(ma_khu_vuc) ON DELETE CASCADE,
    -- Không trùng tên kệ trong CÙNG 1 khu hàng (2 khu khác nhau vẫn được
    -- phép có kệ trùng tên, vì đó là 2 vị trí vật lý khác nhau).
    CONSTRAINT UK_kehang_khuvuc_ten UNIQUE (ma_khu_vuc, ten_ke_hang)
);
GO

CREATE INDEX IX_kehang_khuvuc ON ke_hang(ma_khu_vuc);      -- liệt kê danh sách kệ theo từng khu hàng
GO

-- 14. Đơn giá lấy từ gia_tien; trừ lượt KM nguyên tử.

-- Lưu lại thông tin khách hàng ngay trên đơn.
ALTER TABLE don_hang ADD ten_khach_hang_giao NVARCHAR(150) NULL;
ALTER TABLE don_hang ADD sdt_khach_hang_giao VARCHAR(20) NULL;
ALTER TABLE don_hang ADD dia_chi_giao_hang NVARCHAR(255) NULL;
GO

-- Bộ đếm lượt dùng khuyến mãi.
ALTER TABLE khuyen_mai ADD so_lan_da_dung INT NOT NULL DEFAULT 0;
GO
ALTER TABLE khuyen_mai ADD CONSTRAINT CK_khuyenmai_solandadung CHECK (so_lan_da_dung >= 0);
GO
-- Khởi tạo bộ đếm theo số đơn chưa hủy.
UPDATE km
SET so_lan_da_dung = (
    SELECT COUNT(*) FROM don_hang dh
    WHERE dh.ma_khuyen_mai = km.ma_khuyen_mai AND dh.trang_thai <> 'CANCELLED'
)
FROM khuyen_mai km;
GO

-- Kiểu bảng để truyền danh sách dòng hàng vào SP trong 1 lần.
CREATE TYPE ChiTietDonHang_Type AS TABLE
(
    ma_sku INT,
    so_luong INT
);
GO

CREATE PROCEDURE sp_TaoDonHang
    @ma_nhan_vien INT,
    @ma_khach_hang INT = NULL,
    @ten_khach_hang NVARCHAR(150),
    @sdt_khach_hang VARCHAR(20) = NULL,
    @dia_chi_giao_hang NVARCHAR(255) = NULL,
    @ma_khuyen_mai INT = NULL,
    @phi_van_chuyen DECIMAL(15,2) = 0,
    @hinh_thuc_nhan_hang VARCHAR(20) = 'TAI_SHOP',
    @ghi_chu NVARCHAR(MAX) = NULL,
    @chi_tiet ChiTietDonHang_Type READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Điểm 1: Đơn giá lấy từ gia_tien (giá của SKU), không nhận từ client.
    DECLARE @tong_tien_hang DECIMAL(15,2);
    SELECT @tong_tien_hang = SUM(ct.so_luong * sp.gia_tien)
    FROM @chi_tiet ct
    INNER JOIN san_pham_sku sp ON sp.ma_sku = ct.ma_sku;

    IF @tong_tien_hang IS NULL
    BEGIN
        RAISERROR (N'Danh sách sản phẩm không hợp lệ.', 16, 1);
        RETURN;
    END

    -- Kiểm tra khuyến mãi sơ bộ ngoài transaction; trừ lượt bên trong.
    DECLARE @loai_giam_gia VARCHAR(20), @gia_tri_giam DECIMAL(15,2), @gia_tri_toi_thieu DECIMAL(15,2);
    IF @ma_khuyen_mai IS NOT NULL
    BEGIN
        SELECT @loai_giam_gia = loai_giam_gia, @gia_tri_giam = gia_tri_giam,
               @gia_tri_toi_thieu = gia_tri_don_toi_thieu
        FROM khuyen_mai
        WHERE ma_khuyen_mai = @ma_khuyen_mai
          AND trang_thai_hoat_dong = 1
          AND GETDATE() BETWEEN ngay_bat_dau AND ngay_ket_thuc;

        IF @loai_giam_gia IS NULL
        BEGIN
            RAISERROR (N'Khuyến mãi không hợp lệ: đã hết hạn hoặc chưa được kích hoạt.', 16, 1);
            RETURN;
        END

        IF @tong_tien_hang < @gia_tri_toi_thieu
        BEGIN
            RAISERROR (N'Đơn hàng chưa đạt giá trị tối thiểu để áp dụng khuyến mãi này.', 16, 1);
            RETURN;
        END
    END

    DECLARE @tien_giam_gia DECIMAL(15,2) = 0;
    DECLARE @ma_don_hang INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @ma_khuyen_mai IS NOT NULL
        BEGIN
            -- Trừ lượt nguyên tử.
            UPDATE khuyen_mai
            SET so_lan_da_dung = so_lan_da_dung + 1
            WHERE ma_khuyen_mai = @ma_khuyen_mai
              AND (so_lan_su_dung_toi_da IS NULL OR so_lan_da_dung < so_lan_su_dung_toi_da);

            IF @@ROWCOUNT = 0
            BEGIN
                RAISERROR (N'Khuyến mãi đã hết lượt sử dụng.', 16, 1);
                RETURN;
            END

            SET @tien_giam_gia = CASE WHEN @loai_giam_gia = 'PERCENT'
                                       THEN @tong_tien_hang * @gia_tri_giam / 100.0
                                       ELSE @gia_tri_giam END;
            IF @tien_giam_gia > @tong_tien_hang SET @tien_giam_gia = @tong_tien_hang;
        END

        -- ma_code_don_hang tự sinh, không nhận từ giao diện.
        INSERT INTO don_hang (ma_nhan_vien, ma_khach_hang, ma_khuyen_mai,
                               tong_tien_hang, tien_giam_gia, phi_van_chuyen,
                               hinh_thuc_nhan_hang, ghi_chu, trang_thai, trang_thai_thanh_toan,
                               ten_khach_hang_giao, sdt_khach_hang_giao, dia_chi_giao_hang)
        VALUES (@ma_nhan_vien, @ma_khach_hang, @ma_khuyen_mai,
                @tong_tien_hang, @tien_giam_gia, @phi_van_chuyen,
                @hinh_thuc_nhan_hang, @ghi_chu, 'CONFIRMED', 'UNPAID',
                @ten_khach_hang, @sdt_khach_hang, @dia_chi_giao_hang);

        SET @ma_don_hang = SCOPE_IDENTITY();

        -- Trigger trg_cthd_tru_ton_khi_ban tự trừ kho khi chèn chi tiết
        -- (trigger yêu cầu đơn CONFIRMED nên đơn tạm ở CONFIRMED/UNPAID tới đây).
        INSERT INTO chi_tiet_don_hang (ma_don_hang, ma_sku, so_luong, don_gia, thanh_tien_dong)
        SELECT @ma_don_hang, ct.ma_sku, ct.so_luong, sp.gia_tien, ct.so_luong * sp.gia_tien
        FROM @chi_tiet ct
        INNER JOIN san_pham_sku sp ON sp.ma_sku = ct.ma_sku;

        IF @hinh_thuc_nhan_hang = 'GIAO_HANG'
        BEGIN
            INSERT INTO van_don (ma_don_hang, ma_nhan_vien_giao_hang)
            VALUES (@ma_don_hang, NULL);
        END

        -- Đơn tạo từ màn bán hàng đã chốt và nhận tiền ngay -> luôn là đơn thành công.
        UPDATE don_hang
        SET trang_thai = 'COMPLETED',
            trang_thai_thanh_toan = 'PAID'
        WHERE ma_don_hang = @ma_don_hang;

    COMMIT TRANSACTION;
    SELECT ma_don_hang, ma_code_don_hang FROM don_hang WHERE ma_don_hang = @ma_don_hang;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 15. 1 SKU nằm nhiều kệ: bảng ton_kho_ke_hang.

CREATE TABLE ton_kho_ke_hang (
    ma_sku INT NOT NULL,                                   -- SKU nào
    ma_ke_hang INT NOT NULL,                               -- Nằm trên kệ nào
    so_luong INT NOT NULL DEFAULT 0,                       -- Số lượng của SKU này trên đúng kệ này
    CONSTRAINT PK_tonkhokehang PRIMARY KEY (ma_sku, ma_ke_hang),
    CONSTRAINT FK_tonkhokehang_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku) ON DELETE CASCADE,
    CONSTRAINT FK_tonkhokehang_kehang FOREIGN KEY (ma_ke_hang) REFERENCES ke_hang(ma_ke_hang) ON DELETE CASCADE,
    CONSTRAINT CK_tonkhokehang_soluong CHECK (so_luong >= 0)
);
GO
CREATE INDEX IX_tonkhokehang_kehang ON ton_kho_ke_hang(ma_ke_hang);
GO

-- 16. Nhật ký biến động tồn kho.

CREATE TABLE nhat_ky_ton_kho (
    ma_nhat_ky BIGINT IDENTITY(1,1) PRIMARY KEY,
    ma_sku INT NOT NULL,
    loai_bien_dong VARCHAR(20) NOT NULL
        CHECK (loai_bien_dong IN ('QUET_MA', 'BAN_HANG', 'HUY_DON', 'NHAP_KHO', 'CHUYEN_KE', 'DIEU_CHINH_TAY')),
    so_luong_thay_doi INT NOT NULL,            -- dương = tăng, âm = giảm
    ma_nguoi_thuc_hien INT NULL,                -- NULL = không xác định được người thao tác (vd xóa dòng đơn trực tiếp)
    ma_don_hang INT NULL,
    ma_phieu_nhap INT NULL,
    ma_ke_hang INT NULL,                       -- dùng cho CHUYEN_KE
    thoi_gian DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_nhatky_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku),
    CONSTRAINT FK_nhatky_nguoidung FOREIGN KEY (ma_nguoi_thuc_hien) REFERENCES nguoi_dung(ma_nguoi_dung),
    CONSTRAINT FK_nhatky_donhang FOREIGN KEY (ma_don_hang) REFERENCES don_hang(ma_don_hang) ON DELETE SET NULL,
    CONSTRAINT FK_nhatky_phieunhap FOREIGN KEY (ma_phieu_nhap) REFERENCES phieu_nhap_kho(ma_phieu_nhap) ON DELETE SET NULL,
    CONSTRAINT FK_nhatky_kehang FOREIGN KEY (ma_ke_hang) REFERENCES ke_hang(ma_ke_hang) ON DELETE SET NULL
);
GO
CREATE INDEX IX_nhatky_sku_thoigian ON nhat_ky_ton_kho(ma_sku, thoi_gian);
GO

-- sp_HuyDonHang: ghi nhật ký HUY_DON theo người bấm hủy.
CREATE PROCEDURE sp_HuyDonHang
    @ma_don_hang INT,
    @ma_nguoi_thuc_hien INT,
    @ly_do_huy NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @trang_thai_tt VARCHAR(20);
    SELECT @trang_thai_tt = trang_thai_thanh_toan FROM don_hang WHERE ma_don_hang = @ma_don_hang;

    IF @trang_thai_tt IS NULL
    BEGIN
        RAISERROR (N'Không tìm thấy đơn hàng.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1 FROM nguoi_dung nd
        INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
        WHERE nd.ma_nguoi_dung = @ma_nguoi_thuc_hien
          AND vt.ten_vai_tro IN (N'Quản lý', N'Nhân viên kho', N'Nhân viên bán hàng')
    )
    BEGIN
        RAISERROR (N'Chỉ quản lý, nhân viên kho và nhân viên bán hàng mới có quyền hủy đơn hàng.', 16, 1);
        RETURN;
    END

    IF @trang_thai_tt = 'PAID'
    BEGIN
        RAISERROR (N'Đơn hàng đã thanh toán, không thể hủy.', 16, 1);
        RETURN;
    END

    UPDATE don_hang
    SET trang_thai = 'CANCELLED',
        ngay_huy = GETDATE(),
        ly_do_huy = @ly_do_huy
    WHERE ma_don_hang = @ma_don_hang;

    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_don_hang)
    SELECT ct.ma_sku, 'HUY_DON', SUM(ct.so_luong), @ma_nguoi_thuc_hien, @ma_don_hang
    FROM chi_tiet_don_hang ct
    WHERE ct.ma_don_hang = @ma_don_hang
    GROUP BY ct.ma_sku;
END;
GO

-- sp_QuetSkuNhapKho: thêm @ma_nguoi_thuc_hien để log QUET_MA.
CREATE PROCEDURE sp_QuetSkuNhapKho
    @ma_code_sku VARCHAR(50),
    @ma_nguoi_thuc_hien INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM san_pham_sku WHERE ma_code_sku = @ma_code_sku)
    BEGIN
        RAISERROR (N'Không tìm thấy mã SKU này trong hệ thống.', 16, 1);
        RETURN;
    END

    UPDATE san_pham_sku
    SET so_luong = so_luong + 1,
        trang_thai_vong_doi = CASE WHEN trang_thai_vong_doi = 'SOLD' THEN 'READY' ELSE trang_thai_vong_doi END
    WHERE ma_code_sku = @ma_code_sku;

    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien)
    SELECT ma_sku, 'QUET_MA', 1, @ma_nguoi_thuc_hien FROM san_pham_sku WHERE ma_code_sku = @ma_code_sku;

    SELECT sp.ma_sku, sp.ma_code_sku, msp.ten_san_pham, msp.duong_dan_hinh_anh,
           sp.so_luong AS ton_kho_sau_khi_them
    FROM san_pham_sku sp
    INNER JOIN mau_san_pham msp ON msp.ma_mau_san_pham = sp.ma_mau_san_pham
    WHERE sp.ma_code_sku = @ma_code_sku;
END;
GO

-- sp_HoanTatNhapKho: log NHAP_KHO (người lập phiếu).
CREATE PROCEDURE sp_HoanTatNhapKho
    @ma_phieu_nhap INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM phieu_nhap_kho WHERE ma_phieu_nhap = @ma_phieu_nhap)
    BEGIN
        RAISERROR (N'Không tìm thấy phiếu nhập kho.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM phieu_nhap_kho WHERE ma_phieu_nhap = @ma_phieu_nhap AND trang_thai = 'COMPLETED')
    BEGIN
        RAISERROR (N'Phiếu nhập kho này đã được hoàn tất trước đó.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM phieu_nhap_kho WHERE ma_phieu_nhap = @ma_phieu_nhap AND trang_thai = 'CANCELLED')
    BEGIN
        RAISERROR (N'Phiếu nhập kho này đã bị hủy, không thể hoàn tất.', 16, 1);
        RETURN;
    END

    DECLARE @nguoi_tao INT;
    SELECT @nguoi_tao = nguoi_tao FROM phieu_nhap_kho WHERE ma_phieu_nhap = @ma_phieu_nhap;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE sp
        SET sp.so_luong = sp.so_luong + ct.so_luong_thuc_nhan,
            sp.trang_thai_vong_doi = CASE WHEN sp.trang_thai_vong_doi = 'SOLD'
                                           THEN 'READY' ELSE sp.trang_thai_vong_doi END
        FROM san_pham_sku sp
        INNER JOIN (
            SELECT ma_sku, SUM(so_luong_thuc_nhan) AS so_luong_thuc_nhan
            FROM chi_tiet_phieu_nhap_kho
            WHERE ma_phieu_nhap = @ma_phieu_nhap
            GROUP BY ma_sku
        ) ct ON ct.ma_sku = sp.ma_sku;

        INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_phieu_nhap)
        SELECT ma_sku, 'NHAP_KHO', SUM(so_luong_thuc_nhan), @nguoi_tao, @ma_phieu_nhap
        FROM chi_tiet_phieu_nhap_kho
        WHERE ma_phieu_nhap = @ma_phieu_nhap
        GROUP BY ma_sku;

        UPDATE phieu_nhap_kho
        SET trang_thai = 'COMPLETED'
        WHERE ma_phieu_nhap = @ma_phieu_nhap;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT sp.ma_sku, sp.ma_code_sku, ct.so_luong_thuc_nhan, sp.so_luong AS ton_kho_sau_khi_nhap
    FROM chi_tiet_phieu_nhap_kho ct
    INNER JOIN san_pham_sku sp ON sp.ma_sku = ct.ma_sku
    WHERE ct.ma_phieu_nhap = @ma_phieu_nhap;
END;
GO

-- sp_ThemSkuVaoKe: thêm @ma_nguoi_thuc_hien để log CHUYEN_KE.
CREATE PROCEDURE sp_ThemSkuVaoKe
    @ma_sku INT,
    @ma_ke_hang INT,
    @so_luong INT,
    @ma_nguoi_thuc_hien INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM san_pham_sku WHERE ma_sku = @ma_sku)
    BEGIN
        RAISERROR (N'Không tìm thấy sản phẩm này.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM ke_hang WHERE ma_ke_hang = @ma_ke_hang)
    BEGIN
        RAISERROR (N'Không tìm thấy kệ hàng này.', 16, 1);
        RETURN;
    END

    IF @so_luong IS NULL OR @so_luong <= 0
    BEGIN
        RAISERROR (N'Số lượng xếp lên kệ phải lớn hơn 0.', 16, 1);
        RETURN;
    END

    -- Upsert: UPDATE trước, chưa có mới INSERT.
    UPDATE ton_kho_ke_hang SET so_luong = so_luong + @so_luong
    WHERE ma_sku = @ma_sku AND ma_ke_hang = @ma_ke_hang;

    IF @@ROWCOUNT = 0
    BEGIN
        BEGIN TRY
            INSERT INTO ton_kho_ke_hang (ma_sku, ma_ke_hang, so_luong)
            VALUES (@ma_sku, @ma_ke_hang, @so_luong);
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() IN (2601, 2627)
                UPDATE ton_kho_ke_hang SET so_luong = so_luong + @so_luong
                WHERE ma_sku = @ma_sku AND ma_ke_hang = @ma_ke_hang;
            ELSE THROW;
        END CATCH
    END

    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_ke_hang)
    VALUES (@ma_sku, 'CHUYEN_KE', @so_luong, @ma_nguoi_thuc_hien, @ma_ke_hang);

    SELECT sp.ma_sku, sp.ma_code_sku, msp.ten_san_pham, msp.duong_dan_hinh_anh,
           sp.gia_tien, tk.so_luong AS so_luong_tren_ke
    FROM ton_kho_ke_hang tk
    INNER JOIN san_pham_sku sp ON sp.ma_sku = tk.ma_sku
    INNER JOIN mau_san_pham msp ON msp.ma_mau_san_pham = sp.ma_mau_san_pham
    WHERE tk.ma_ke_hang = @ma_ke_hang;
END;
GO

-- 17. Lọc ngày giữ index seek (khoảng nửa-mở) + index gộp.

CREATE INDEX IX_donhang_trangthai_ngaydat ON don_hang(trang_thai, ngay_dat_hang);
GO

CREATE PROCEDURE sp_BaoCaoDoanhThuTheoThoiGian
    @tu_ngay DATE,
    @den_ngay DATE,
    @nhom_theo VARCHAR(10) = 'NGAY'   -- NGAY | THANG | NAM
AS
BEGIN
    SET NOCOUNT ON;

    -- Khoảng nửa-mở, không CAST cột ngày -> giữ index seek.
    DECLARE @den_ngay_ke_tiep DATETIME2 = DATEADD(DAY, 1, @den_ngay);

    IF @nhom_theo = 'NGAY'
        SELECT CAST(ngay_dat_hang AS DATE) AS moc_thoi_gian,
               COUNT(DISTINCT ma_don_hang) AS so_luong_don, SUM(thanh_tien) AS doanh_thu
        FROM don_hang
        WHERE trang_thai = 'COMPLETED' AND ngay_dat_hang >= @tu_ngay AND ngay_dat_hang < @den_ngay_ke_tiep
        GROUP BY CAST(ngay_dat_hang AS DATE)
        ORDER BY moc_thoi_gian;

    ELSE IF @nhom_theo = 'THANG'
        SELECT DATEPART(YEAR, ngay_dat_hang) AS nam, DATEPART(MONTH, ngay_dat_hang) AS thang,
               COUNT(DISTINCT ma_don_hang) AS so_luong_don, SUM(thanh_tien) AS doanh_thu
        FROM don_hang
        WHERE trang_thai = 'COMPLETED' AND ngay_dat_hang >= @tu_ngay AND ngay_dat_hang < @den_ngay_ke_tiep
        GROUP BY DATEPART(YEAR, ngay_dat_hang), DATEPART(MONTH, ngay_dat_hang)
        ORDER BY nam, thang;

    ELSE IF @nhom_theo = 'NAM'
        SELECT DATEPART(YEAR, ngay_dat_hang) AS nam,
               COUNT(DISTINCT ma_don_hang) AS so_luong_don, SUM(thanh_tien) AS doanh_thu
        FROM don_hang
        WHERE trang_thai = 'COMPLETED' AND ngay_dat_hang >= @tu_ngay AND ngay_dat_hang < @den_ngay_ke_tiep
        GROUP BY DATEPART(YEAR, ngay_dat_hang)
        ORDER BY nam;
END;
GO

-- 18. Chỉ lưu hash mật khẩu; bật đổi ở lần đăng nhập kế tiếp.

ALTER TABLE nguoi_dung ADD phai_doi_mat_khau BIT NOT NULL DEFAULT 0;
GO


-- 19. Bật phai_doi_mat_khau cho admin gốc.
UPDATE nguoi_dung SET phai_doi_mat_khau = 1 WHERE ten_dang_nhap = 'admin';
GO
-- 20. Thêm SĐT/ngày sinh/giới tính cho nhân viên.
ALTER TABLE nguoi_dung ADD so_dien_thoai VARCHAR(20) NULL;
GO
ALTER TABLE nguoi_dung ADD ngay_sinh DATE NULL;
GO
ALTER TABLE nguoi_dung ADD gioi_tinh NVARCHAR(10) NULL
    CONSTRAINT CK_nguoidung_gioitinh CHECK (gioi_tinh IN (N'Nam', N'Nữ', N'Khác'));
GO

-- 21. Nhật ký đổi thông tin người dùng.

CREATE TABLE nhat_ky_thay_doi_nguoi_dung (
    ma_nhat_ky INT IDENTITY(1,1) PRIMARY KEY,
    ma_nguoi_dung INT NOT NULL,                -- người bị thay đổi thông tin
    ma_nguoi_thuc_hien INT NULL,               -- người bấm Lưu (NULL nếu qua liên kết quên mật khẩu, chưa đăng nhập)
    ten_truong VARCHAR(50) NOT NULL,           -- tên cột nguoi_dung bị đổi
    gia_tri_cu NVARCHAR(255) NULL,
    gia_tri_moi NVARCHAR(255) NULL,
    thoi_gian DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_nhatkynguoidung_doituong FOREIGN KEY (ma_nguoi_dung) REFERENCES nguoi_dung(ma_nguoi_dung),
    CONSTRAINT FK_nhatkynguoidung_nguoithuchien FOREIGN KEY (ma_nguoi_thuc_hien) REFERENCES nguoi_dung(ma_nguoi_dung)
);
GO

CREATE INDEX IX_nhatkynguoidung_doituong ON nhat_ky_thay_doi_nguoi_dung(ma_nguoi_dung, thoi_gian DESC);
GO

-- ============================================================
-- 22. HÀNG XẾP TRỰC TIẾP VÀO KHU (khu không bắt buộc phải có kệ).
-- 1 SKU có thể nằm ở nhiều khu (chưa xếp kệ) và nhiều kệ.
-- ============================================================

CREATE TABLE ton_kho_khu (
    ma_sku INT NOT NULL,
    ma_khu_vuc INT NOT NULL,
    so_luong INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_tonkhokhu PRIMARY KEY (ma_sku, ma_khu_vuc),
    CONSTRAINT FK_tonkhokhu_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku) ON DELETE CASCADE,
    CONSTRAINT FK_tonkhokhu_khuvuc FOREIGN KEY (ma_khu_vuc) REFERENCES khu_vuc_kho(ma_khu_vuc) ON DELETE CASCADE,
    CONSTRAINT CK_tonkhokhu_soluong CHECK (so_luong >= 0)
);
GO
CREATE INDEX IX_tonkhokhu_khuvuc ON ton_kho_khu(ma_khu_vuc);
GO

-- Xếp thêm hàng vào khu (chưa chọn kệ): upsert + ghi nhật ký CHUYEN_KE (ma_ke_hang NULL).
CREATE PROCEDURE sp_ThemSkuVaoKhu
    @ma_sku INT,
    @ma_khu_vuc INT,
    @so_luong INT,
    @ma_nguoi_thuc_hien INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM san_pham_sku WHERE ma_sku = @ma_sku)
    BEGIN
        RAISERROR (N'Không tìm thấy sản phẩm này.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM khu_vuc_kho WHERE ma_khu_vuc = @ma_khu_vuc)
    BEGIN
        RAISERROR (N'Không tìm thấy khu hàng này.', 16, 1);
        RETURN;
    END

    IF @so_luong IS NULL OR @so_luong <= 0
    BEGIN
        RAISERROR (N'Số lượng xếp vào khu phải lớn hơn 0.', 16, 1);
        RETURN;
    END

    UPDATE ton_kho_khu SET so_luong = so_luong + @so_luong
    WHERE ma_sku = @ma_sku AND ma_khu_vuc = @ma_khu_vuc;

    IF @@ROWCOUNT = 0
    BEGIN
        BEGIN TRY
            INSERT INTO ton_kho_khu (ma_sku, ma_khu_vuc, so_luong)
            VALUES (@ma_sku, @ma_khu_vuc, @so_luong);
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() IN (2601, 2627)
                UPDATE ton_kho_khu SET so_luong = so_luong + @so_luong
                WHERE ma_sku = @ma_sku AND ma_khu_vuc = @ma_khu_vuc;
            ELSE THROW;
        END CATCH
    END

    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_ke_hang)
    VALUES (@ma_sku, 'CHUYEN_KE', @so_luong, @ma_nguoi_thuc_hien, NULL);

    SELECT sp.ma_sku, sp.ma_code_sku, msp.ten_san_pham, msp.duong_dan_hinh_anh,
           sp.gia_tien, tk.so_luong AS so_luong_trong_khu
    FROM ton_kho_khu tk
    INNER JOIN san_pham_sku sp ON sp.ma_sku = tk.ma_sku
    INNER JOIN mau_san_pham msp ON msp.ma_mau_san_pham = sp.ma_mau_san_pham
    WHERE tk.ma_khu_vuc = @ma_khu_vuc;
END;
GO

-- sp_DatLaiMatKhauNhanVien: quản lý đặt lại mật khẩu.
CREATE PROCEDURE sp_DatLaiMatKhauNhanVien
    @ma_nguoi_dung INT,
    @ma_nguoi_thuc_hien INT,
    @mat_khau_ma_hoa_moi VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM nguoi_dung WHERE ma_nguoi_dung = @ma_nguoi_dung)
    BEGIN
        RAISERROR (N'Không tìm thấy nhân viên này.', 16, 1);
        RETURN;
    END

    UPDATE nguoi_dung
    SET mat_khau_ma_hoa = @mat_khau_ma_hoa_moi,
        phai_doi_mat_khau = 1,
        ngay_cap_nhat = GETDATE()
    WHERE ma_nguoi_dung = @ma_nguoi_dung;

    INSERT INTO nhat_ky_thay_doi_nguoi_dung (ma_nguoi_dung, ma_nguoi_thuc_hien, ten_truong, gia_tri_cu, gia_tri_moi)
    VALUES (@ma_nguoi_dung, @ma_nguoi_thuc_hien, 'mat_khau_ma_hoa', NULL, N'(Quản lý đặt lại mật khẩu)');
END;
GO

-- SP xem lịch sử đổi thông tin 1 nhân viên.
CREATE PROCEDURE sp_LichSuThayDoiNhanVien
    @ma_nguoi_dung INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT nk.thoi_gian, nk.ten_truong, nk.gia_tri_cu, nk.gia_tri_moi,
           nd.ho_ten AS nguoi_thuc_hien
    FROM nhat_ky_thay_doi_nguoi_dung nk
    LEFT JOIN nguoi_dung nd ON nd.ma_nguoi_dung = nk.ma_nguoi_thuc_hien
    WHERE nk.ma_nguoi_dung = @ma_nguoi_dung
    ORDER BY nk.thoi_gian DESC;
END;
GO

-- 23. Hàm bỏ dấu + bảng đếm mã SKU + sp_TaoSanPham.


-- Hàm bỏ dấu tiếng Việt để sinh mã viết tắt.
CREATE FUNCTION fn_BoDauTiengViet (@chuoi NVARCHAR(255))
RETURNS NVARCHAR(255)
AS
BEGIN
    IF @chuoi IS NULL RETURN NULL;
    DECLARE @tra_ve NVARCHAR(255) = N'';
    DECLARE @i INT = 1, @ch NCHAR(1), @goc NCHAR(1);
    WHILE @i <= LEN(@chuoi)
    BEGIN
        SET @ch = SUBSTRING(@chuoi, @i, 1);
        SET @goc = CASE
            WHEN @ch LIKE N'a' COLLATE Latin1_General_CI_AI THEN N'a'
            WHEN @ch LIKE N'e' COLLATE Latin1_General_CI_AI THEN N'e'
            WHEN @ch LIKE N'i' COLLATE Latin1_General_CI_AI THEN N'i'
            WHEN @ch LIKE N'o' COLLATE Latin1_General_CI_AI THEN N'o'
            WHEN @ch LIKE N'u' COLLATE Latin1_General_CI_AI THEN N'u'
            WHEN @ch LIKE N'y' COLLATE Latin1_General_CI_AI THEN N'y'
            WHEN @ch = N'đ' THEN N'd'
            WHEN @ch = N'Đ' THEN N'D'
            ELSE NULL END;
        IF @goc IS NULL
            SET @goc = @ch;
        ELSE IF @ch = UPPER(@ch) COLLATE Latin1_General_BIN
            SET @goc = UPPER(@goc);
        SET @tra_ve += @goc;
        SET @i += 1;
    END
    RETURN @tra_ve;
END;
GO

-- Bộ đếm số thứ tự mã SKU theo tháng.
CREATE TABLE bo_dem_ma_sku_theo_thang (
    thang_nam CHAR(4) PRIMARY KEY,          -- định dạng YYMM
    so_luong_da_dung INT NOT NULL DEFAULT 0
);
GO

-- 24. Tồn kệ tự trừ/hoàn khi bán/hủy.

-- Bảng phan_bo_xuat_kho_theo_ke ghi vết đã trừ kệ nào bao nhiêu.
CREATE TABLE phan_bo_xuat_kho_theo_ke (
    ma_phan_bo INT IDENTITY(1,1) PRIMARY KEY,
    ma_don_hang INT NOT NULL,
    ma_sku INT NOT NULL,
    ma_ke_hang INT NOT NULL,
    so_luong INT NOT NULL CHECK (so_luong > 0),
    CONSTRAINT FK_phanbo_donhang FOREIGN KEY (ma_don_hang) REFERENCES don_hang(ma_don_hang) ON DELETE CASCADE,
    CONSTRAINT FK_phanbo_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku),
    CONSTRAINT FK_phanbo_kehang FOREIGN KEY (ma_ke_hang) REFERENCES ke_hang(ma_ke_hang)
);
GO
CREATE INDEX IX_phanbo_sku_kehang ON phan_bo_xuat_kho_theo_ke(ma_sku, ma_ke_hang);
GO
CREATE INDEX IX_phanbo_kehang ON phan_bo_xuat_kho_theo_ke(ma_ke_hang);
GO

-- Xóa dòng trực tiếp -> hoàn đúng kệ đã trừ (Điểm 1).
CREATE TRIGGER trg_cthd_hoan_ton_khi_xoa_dong
ON chi_tiet_don_hang
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE sp
    SET sp.so_luong = sp.so_luong + g.tong_so_luong,
        sp.trang_thai_vong_doi = CASE WHEN sp.trang_thai_vong_doi = 'SOLD'
                                       THEN 'READY' ELSE sp.trang_thai_vong_doi END
    FROM san_pham_sku sp
    INNER JOIN (
        SELECT d.ma_sku, SUM(d.so_luong) AS tong_so_luong
        FROM deleted d
        LEFT JOIN don_hang dh ON dh.ma_don_hang = d.ma_don_hang
        WHERE dh.ma_don_hang IS NULL OR dh.trang_thai <> 'CANCELLED'
        GROUP BY d.ma_sku
    ) g ON sp.ma_sku = g.ma_sku;

    UPDATE tk
    SET tk.so_luong = tk.so_luong + pb.so_luong
    FROM ton_kho_ke_hang tk
    INNER JOIN phan_bo_xuat_kho_theo_ke pb ON pb.ma_sku = tk.ma_sku AND pb.ma_ke_hang = tk.ma_ke_hang
    INNER JOIN (
        SELECT DISTINCT d.ma_don_hang, d.ma_sku
        FROM deleted d
        LEFT JOIN don_hang dh ON dh.ma_don_hang = d.ma_don_hang
        WHERE dh.ma_don_hang IS NULL OR dh.trang_thai <> 'CANCELLED'
    ) x ON x.ma_don_hang = pb.ma_don_hang AND x.ma_sku = pb.ma_sku;

    DELETE pb
    FROM phan_bo_xuat_kho_theo_ke pb
    INNER JOIN (
        SELECT DISTINCT d.ma_don_hang, d.ma_sku
        FROM deleted d
        LEFT JOIN don_hang dh ON dh.ma_don_hang = d.ma_don_hang
        WHERE dh.ma_don_hang IS NULL OR dh.trang_thai <> 'CANCELLED'
    ) x ON x.ma_don_hang = pb.ma_don_hang AND x.ma_sku = pb.ma_sku;

    -- Xóa dòng trực tiếp vẫn phải để lại vết trong sổ nhật ký tồn kho.
    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_don_hang)
    SELECT d.ma_sku, 'HUY_DON', SUM(d.so_luong), dh.ma_nhan_vien, d.ma_don_hang
    FROM deleted d
    LEFT JOIN don_hang dh ON dh.ma_don_hang = d.ma_don_hang
    WHERE dh.ma_don_hang IS NULL OR dh.trang_thai <> 'CANCELLED'
    GROUP BY d.ma_sku, d.ma_don_hang, dh.ma_nhan_vien;
END;
GO

-- Hủy cả đơn -> hoàn đúng kệ đã trừ (Điểm 1).
CREATE TRIGGER trg_donhang_hoan_ton_khi_huy
ON don_hang
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(OBJECT_ID('trg_donhang_hoan_ton_khi_huy')) > 1 RETURN;

    IF EXISTS (
        SELECT 1 FROM inserted i
        INNER JOIN deleted d ON i.ma_don_hang = d.ma_don_hang
        WHERE i.trang_thai = 'CANCELLED' AND d.trang_thai <> 'CANCELLED'
    )
    BEGIN
        UPDATE sp
        SET sp.so_luong = sp.so_luong + ct.so_luong,
            sp.trang_thai_vong_doi = CASE WHEN sp.trang_thai_vong_doi = 'SOLD'
                                           THEN 'READY' ELSE sp.trang_thai_vong_doi END
        FROM san_pham_sku sp
        INNER JOIN chi_tiet_don_hang ct ON ct.ma_sku = sp.ma_sku
        INNER JOIN inserted i ON i.ma_don_hang = ct.ma_don_hang
        INNER JOIN deleted d ON d.ma_don_hang = i.ma_don_hang
        WHERE i.trang_thai = 'CANCELLED' AND d.trang_thai <> 'CANCELLED';

        UPDATE tk
        SET tk.so_luong = tk.so_luong + pb.so_luong
        FROM ton_kho_ke_hang tk
        INNER JOIN phan_bo_xuat_kho_theo_ke pb ON pb.ma_sku = tk.ma_sku AND pb.ma_ke_hang = tk.ma_ke_hang
        INNER JOIN inserted i ON i.ma_don_hang = pb.ma_don_hang
        INNER JOIN deleted d ON d.ma_don_hang = i.ma_don_hang
        WHERE i.trang_thai = 'CANCELLED' AND d.trang_thai <> 'CANCELLED';

        DELETE pb
        FROM phan_bo_xuat_kho_theo_ke pb
        INNER JOIN inserted i ON i.ma_don_hang = pb.ma_don_hang
        INNER JOIN deleted d ON d.ma_don_hang = i.ma_don_hang
        WHERE i.trang_thai = 'CANCELLED' AND d.trang_thai <> 'CANCELLED';

        UPDATE km
        SET km.so_lan_da_dung = CASE WHEN km.so_lan_da_dung > 0 THEN km.so_lan_da_dung - 1 ELSE 0 END
        FROM khuyen_mai km
        INNER JOIN inserted i ON i.ma_khuyen_mai = km.ma_khuyen_mai
        INNER JOIN deleted d ON d.ma_don_hang = i.ma_don_hang
        WHERE i.trang_thai = 'CANCELLED' AND d.trang_thai <> 'CANCELLED' AND i.ma_khuyen_mai IS NOT NULL;
    END
END;
GO


-- Email nhân viên chỉ nhận đuôi @gmail.com.
ALTER TABLE nguoi_dung
ADD CONSTRAINT CK_nguoidung_email_gmail CHECK (
    email IS NULL
    OR LTRIM(RTRIM(email)) = N''
    OR (
        email LIKE N'%_@gmail.com'
        AND email NOT LIKE N'% %'
        AND email NOT LIKE N'%@%@%'
    )
);
GO

-- 25. Thêm index cho cột FK + đổi 7 cột DATETIME sang DATETIME2.

CREATE INDEX IX_donhang_makhuyenmai ON don_hang(ma_khuyen_mai);
GO
CREATE INDEX IX_nhatky_nguoithuchien ON nhat_ky_ton_kho(ma_nguoi_thuc_hien);
GO
CREATE INDEX IX_nhatkynd_nguoithuchien ON nhat_ky_thay_doi_nguoi_dung(ma_nguoi_thuc_hien);
GO
CREATE INDEX IX_phanbo_donhang ON phan_bo_xuat_kho_theo_ke(ma_don_hang);
GO
CREATE INDEX IX_phieunhap_ncc ON phieu_nhap_kho(ma_nha_cung_cap);
GO
CREATE INDEX IX_phieunhap_nguoitao ON phieu_nhap_kho(nguoi_tao);
GO
CREATE INDEX IX_ctpn_sku ON chi_tiet_phieu_nhap_kho(ma_sku);
GO

-- Gỡ DEFAULT, đổi kiểu DATETIME2, gắn lại.
DECLARE @df40 NVARCHAR(200);
SELECT @df40 = dc.name FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'don_hang') AND c.name = N'ngay_tao';
IF @df40 IS NOT NULL EXEC(N'ALTER TABLE [don_hang] DROP CONSTRAINT [' + @df40 + N']');
SELECT @df40 = dc.name FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'don_hang') AND c.name = N'ngay_cap_nhat';
IF @df40 IS NOT NULL EXEC(N'ALTER TABLE [don_hang] DROP CONSTRAINT [' + @df40 + N']');
SELECT @df40 = dc.name FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'nguoi_dung') AND c.name = N'ngay_tao';
IF @df40 IS NOT NULL EXEC(N'ALTER TABLE [nguoi_dung] DROP CONSTRAINT [' + @df40 + N']');
SELECT @df40 = dc.name FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'nguoi_dung') AND c.name = N'ngay_cap_nhat';
IF @df40 IS NOT NULL EXEC(N'ALTER TABLE [nguoi_dung] DROP CONSTRAINT [' + @df40 + N']');
SELECT @df40 = dc.name FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'nhat_ky_thay_doi_nguoi_dung') AND c.name = N'thoi_gian';
IF @df40 IS NOT NULL EXEC(N'ALTER TABLE [nhat_ky_thay_doi_nguoi_dung] DROP CONSTRAINT [' + @df40 + N']');
GO

-- Index có key thoi_gian -> gỡ trước khi đổi kiểu, tạo lại sau.
DROP INDEX IX_nhatkynguoidung_doituong ON nhat_ky_thay_doi_nguoi_dung;
GO

ALTER TABLE don_hang ALTER COLUMN ngay_tao DATETIME2 NULL;
ALTER TABLE don_hang ALTER COLUMN ngay_cap_nhat DATETIME2 NULL;
ALTER TABLE don_hang ALTER COLUMN ngay_huy DATETIME2 NULL;
ALTER TABLE nguoi_dung ALTER COLUMN ngay_tao DATETIME2 NULL;
ALTER TABLE nguoi_dung ALTER COLUMN ngay_cap_nhat DATETIME2 NULL;
ALTER TABLE nhat_ky_thay_doi_nguoi_dung ALTER COLUMN thoi_gian DATETIME2 NOT NULL;
GO

ALTER TABLE don_hang ADD CONSTRAINT DF_donhang_ngaytao40 DEFAULT GETDATE() FOR ngay_tao;
ALTER TABLE don_hang ADD CONSTRAINT DF_donhang_ngaycapnhat40 DEFAULT GETDATE() FOR ngay_cap_nhat;
ALTER TABLE nguoi_dung ADD CONSTRAINT DF_nguoidung_ngaytao40 DEFAULT GETDATE() FOR ngay_tao;
ALTER TABLE nguoi_dung ADD CONSTRAINT DF_nguoidung_ngaycapnhat40 DEFAULT GETDATE() FOR ngay_cap_nhat;
ALTER TABLE nhat_ky_thay_doi_nguoi_dung ADD CONSTRAINT DF_nknd_thoigian40 DEFAULT GETDATE() FOR thoi_gian;
CREATE INDEX IX_nhatkynguoidung_doituong ON nhat_ky_thay_doi_nguoi_dung(ma_nguoi_dung, thoi_gian DESC);
GO

-- 26. Lô hàng theo NGÀY; SKU lấy tháng/năm từ ngày lô.
USE DO_AN_QLK;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- 1) Bảng lo_hang.
IF OBJECT_ID(N'lo_hang', N'U') IS NULL
BEGIN
    CREATE TABLE lo_hang (
        ma_lo_hang INT IDENTITY(1,1) PRIMARY KEY,
        ma_code_lo_hang NVARCHAR(50) NOT NULL UNIQUE,
        ngay_lo_hang DATE NOT NULL,                    -- Ngày lô hàng (quản lý chọn ngày/tháng/năm trên trang Thêm SP)
        ghi_chu NVARCHAR(MAX),
        ngay_tao DATETIME DEFAULT GETDATE()
    );
END
GO

-- 2) SKU thêm ma_lo_hang + FK + index.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'san_pham_sku') AND name = N'ma_lo_hang')
BEGIN
    ALTER TABLE san_pham_sku ADD ma_lo_hang INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_sku_lohang')
BEGIN
    ALTER TABLE san_pham_sku
    ADD CONSTRAINT FK_sku_lohang FOREIGN KEY (ma_lo_hang) REFERENCES lo_hang(ma_lo_hang);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_sku_lohang' AND object_id = OBJECT_ID(N'san_pham_sku'))
BEGIN
    CREATE INDEX IX_sku_lohang ON san_pham_sku(ma_lo_hang) WHERE ma_lo_hang IS NOT NULL;
END
GO

-- 3) sp_TaoSanPham: nhận ngày lô, sinh mã SKU.
CREATE PROCEDURE sp_TaoSanPham
    @ten_san_pham NVARCHAR(150),
    @gia_tien DECIMAL(15,2),
    @so_luong INT,
    @danh_muc NVARCHAR(100) = NULL,
    @nhan_hieu NVARCHAR(100) = NULL,
    @loai_san_pham NVARCHAR(100) = NULL,
    @chat_lieu NVARCHAR(100) = NULL,
    @mau_sac NVARCHAR(100) = NULL,
    @mo_ta NVARCHAR(MAX) = NULL,
    @duong_dan_hinh_anh VARCHAR(255) = NULL,
    @ma_code_ten_sp VARCHAR(50) = NULL,
    @ngay_lo_hang DATE = NULL,   -- Ngay lo hang (ngay/thang/nam) quan ly chon tren giao dien
    @ma_nguoi_thuc_hien INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ten_san_pham IS NULL OR LTRIM(RTRIM(@ten_san_pham)) = N''
    BEGIN
        RAISERROR (N'Tên sản phẩm không được để trống.', 16, 1);
        RETURN;
    END

    IF @gia_tien < 0 OR @so_luong < 0
    BEGIN
        RAISERROR (N'Giá tiền và số lượng phải lớn hơn hoặc bằng 0.', 16, 1);
        RETURN;
    END

    -- Ngày lô bắt buộc; mã lô L+yyMMdd; tháng/năm sinh mã SKU.
    IF @ngay_lo_hang IS NULL
    BEGIN
        RAISERROR (N'Vui lòng chọn ngày lô hàng.', 16, 1);
        RETURN;
    END
    DECLARE @ma_code_lo_hang NVARCHAR(50) = N'L' + FORMAT(@ngay_lo_hang, 'yyMMdd');

    -- Chất liệu + màu sắc bắt buộc; mỗi thuộc tính tối đa 3 ký tự.
    IF @chat_lieu IS NULL OR LTRIM(RTRIM(@chat_lieu)) = N''
    BEGIN
        RAISERROR (N'Chất liệu không được để trống.', 16, 1);
        RETURN;
    END
    IF @mau_sac IS NULL OR LTRIM(RTRIM(@mau_sac)) = N''
    BEGIN
        RAISERROR (N'Màu sắc không được để trống.', 16, 1);
        RETURN;
    END

    IF @ma_code_ten_sp IS NULL OR LTRIM(RTRIM(@ma_code_ten_sp)) = N''
        SET @ma_code_ten_sp = LEFT(UPPER(REPLACE(dbo.fn_BoDauTiengViet(@ten_san_pham), ' ', '')), 6);

    -- Đoạn thuộc tính: bỏ dấu, in hoa, bỏ khoảng trắng.
    DECLARE @ma_thuoctinh VARCHAR(6) =
        LEFT(UPPER(REPLACE(dbo.fn_BoDauTiengViet(@chat_lieu), ' ', '')), 3)
        + LEFT(UPPER(REPLACE(dbo.fn_BoDauTiengViet(@mau_sac), ' ', '')), 3);

    -- Danh mục: tra theo tên; chưa có thì tự tạo.
    DECLARE @ten_dm_chuan NVARCHAR(100) = ISNULL(NULLIF(LTRIM(RTRIM(@danh_muc)), N''), N'Chua phan loai');
    DECLARE @ma_danh_muc INT;
    DECLARE @ma_nhom_hang VARCHAR(10);

    SELECT @ma_danh_muc = ma_danh_muc, @ma_nhom_hang = ma_code_danh_muc
    FROM danh_muc WHERE ten_danh_muc = @ten_dm_chuan;

    IF @ma_danh_muc IS NULL
    BEGIN
        DECLARE @ma_goc VARCHAR(10) = LEFT(UPPER(REPLACE(dbo.fn_BoDauTiengViet(@ten_dm_chuan), ' ', '')), 3);
        DECLARE @ma_thu VARCHAR(10) = @ma_goc, @dem INT = 1;
        WHILE EXISTS (SELECT 1 FROM danh_muc WHERE ma_code_danh_muc = @ma_thu)
        BEGIN
            SET @dem += 1;
            SET @ma_thu = @ma_goc + CONVERT(VARCHAR(2), @dem);
        END

        BEGIN TRY
            INSERT INTO danh_muc (ma_code_danh_muc, ten_danh_muc) VALUES (@ma_thu, @ten_dm_chuan);
            SET @ma_danh_muc = SCOPE_IDENTITY();
            SET @ma_nhom_hang = @ma_thu;
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
            SELECT @ma_danh_muc = ma_danh_muc, @ma_nhom_hang = ma_code_danh_muc
            FROM danh_muc WHERE ten_danh_muc = @ten_dm_chuan;
        END CATCH
    END

    DECLARE @yymm CHAR(4) = FORMAT(@ngay_lo_hang, 'yyMM');
    DECLARE @dem_ra TABLE (so_thu_tu INT);
    DECLARE @so_thu_tu INT;
    DECLARE @ma_sku INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Tìm-hoặc-tạo lô theo ngày.
        DECLARE @ma_lo_hang INT;
        SELECT @ma_lo_hang = ma_lo_hang FROM lo_hang WHERE ma_code_lo_hang = @ma_code_lo_hang;
        IF @ma_lo_hang IS NULL
        BEGIN
            BEGIN TRY
                INSERT INTO lo_hang (ma_code_lo_hang, ngay_lo_hang)
                VALUES (@ma_code_lo_hang, @ngay_lo_hang);
                SET @ma_lo_hang = SCOPE_IDENTITY();
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() IN (2601, 2627)
                    SELECT @ma_lo_hang = ma_lo_hang FROM lo_hang WHERE ma_code_lo_hang = @ma_code_lo_hang;
                ELSE THROW;
            END CATCH
            IF @ma_lo_hang IS NULL
                THROW 50000, N'Khong xac dinh duoc lo hang.', 1;
        END

        -- Tăng bộ đếm nguyên tử.
        UPDATE bo_dem_ma_sku_theo_thang
        SET so_luong_da_dung = so_luong_da_dung + 1
        OUTPUT INSERTED.so_luong_da_dung INTO @dem_ra
        WHERE thang_nam = @yymm;

        IF @@ROWCOUNT = 0
        BEGIN
            BEGIN TRY
                INSERT INTO bo_dem_ma_sku_theo_thang (thang_nam, so_luong_da_dung)
                OUTPUT INSERTED.so_luong_da_dung INTO @dem_ra
                VALUES (@yymm, 1);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() IN (2601, 2627)
                    UPDATE bo_dem_ma_sku_theo_thang
                    SET so_luong_da_dung = so_luong_da_dung + 1
                    OUTPUT INSERTED.so_luong_da_dung INTO @dem_ra
                    WHERE thang_nam = @yymm;
                ELSE THROW;
            END CATCH
        END

        SELECT @so_thu_tu = so_thu_tu FROM @dem_ra;

        -- Mã SKU: NHÓM-TÊN-THUỘC TÍNH-YYMM###.
        DECLARE @ma_code_sku VARCHAR(50) =
            @ma_nhom_hang + '-' + @ma_code_ten_sp + '-' + @ma_thuoctinh + '-' + @yymm + RIGHT('000' + CONVERT(VARCHAR(10), @so_thu_tu), 3);

        INSERT INTO mau_san_pham (ten_san_pham, ma_code_ten_sp, mo_ta, duong_dan_hinh_anh, ma_danh_muc,
                                  nhan_hieu, loai_san_pham, chat_lieu, mau_sac)
        VALUES (@ten_san_pham, @ma_code_ten_sp, @mo_ta, @duong_dan_hinh_anh, @ma_danh_muc,
                @nhan_hieu, @loai_san_pham, @chat_lieu, @mau_sac);

        DECLARE @ma_mau_san_pham INT = SCOPE_IDENTITY();

        INSERT INTO san_pham_sku (ma_code_sku, ma_mau_san_pham, ma_lo_hang, gia_tien, so_luong)
        VALUES (@ma_code_sku, @ma_mau_san_pham, @ma_lo_hang, @gia_tien, @so_luong);

        SET @ma_sku = SCOPE_IDENTITY();

        IF @so_luong > 0
            INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien)
            VALUES (@ma_sku, 'NHAP_KHO', @so_luong, @ma_nguoi_thuc_hien);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT sp.ma_sku, sp.ma_code_sku, m.ten_san_pham, m.duong_dan_hinh_anh,
           sp.gia_tien, sp.so_luong, dm.ten_danh_muc AS danh_muc, m.nhan_hieu, m.loai_san_pham,
           m.chat_lieu, m.mau_sac,
           lh.ma_code_lo_hang AS lo_hang
    FROM san_pham_sku sp
    INNER JOIN mau_san_pham m ON m.ma_mau_san_pham = sp.ma_mau_san_pham
    INNER JOIN danh_muc dm ON dm.ma_danh_muc = m.ma_danh_muc
    LEFT JOIN lo_hang lh ON lh.ma_lo_hang = sp.ma_lo_hang
    WHERE sp.ma_sku = @ma_sku;
END;
GO

/* ==========================================================================
   DU LIEU MAU DUY NHAT: tai khoan admin (chay cuoi file, sau khi da co
   bang + vai tro + SP). Dang nhap lan dau: admin / Admin@123, he thong
   bat buoc doi mat khau ngay (phai_doi_mat_khau = 1).
   Luu y: day la hash mau cua mat khau "Admin@123". Khi deploy that,
   hay doi mat khau ngay sau lan dang nhap dau tien.
   ========================================================================== */
IF NOT EXISTS (SELECT 1 FROM nguoi_dung WHERE ten_dang_nhap = 'admin')
BEGIN
    INSERT INTO nguoi_dung (ten_dang_nhap, mat_khau_ma_hoa, ho_ten, ma_vai_tro,
                             email, so_dien_thoai, ngay_sinh, gioi_tinh, thuong_tru,
                             trang_thai_hoat_dong, phai_doi_mat_khau)
    SELECT 'admin', '$2b$11$9RQ9ZNHQU3tpU2816sFnAeZO7RWXugOdhaoS7JecRC4KYUQx2.mqW',
           N'Nguyễn Văn An', vt.ma_vai_tro,
           'quanly.kho@gmail.com', '0901234567', '1990-05-15', N'Nam',
           N'123 Đường Lê Lợi, Quận 1, TP. Hồ Chí Minh',
           1, 1
    FROM vai_tro vt WHERE vt.ten_vai_tro = N'Quản lý';
END
ELSE
BEGIN
    UPDATE nd
    SET nd.ho_ten = N'Nguyễn Văn An',
        nd.ma_vai_tro = vt.ma_vai_tro,
        nd.email = 'quanly.kho@gmail.com',
        nd.so_dien_thoai = '0901234567',
        nd.ngay_sinh = '1990-05-15',
        nd.gioi_tinh = N'Nam',
        nd.thuong_tru = N'123 Đường Lê Lợi, Quận 1, TP. Hồ Chí Minh',
        nd.trang_thai_hoat_dong = 1,
        nd.phai_doi_mat_khau = 1,
        nd.ngay_cap_nhat = GETDATE()
    FROM nguoi_dung nd
    INNER JOIN vai_tro vt ON vt.ten_vai_tro = N'Quản lý'
    WHERE nd.ten_dang_nhap = 'admin';
END
GO

SELECT nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten, vt.ten_vai_tro AS vai_tro, nd.email,
       nd.so_dien_thoai, nd.ngay_sinh, nd.gioi_tinh, nd.thuong_tru,
       vt.duoc_nhan_van_don, nd.trang_thai_hoat_dong, nd.phai_doi_mat_khau
FROM nguoi_dung nd INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
WHERE nd.ten_dang_nhap = 'admin';
GO


IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_nhacungcap_email' AND object_id = OBJECT_ID(N'nha_cung_cap'))
    CREATE UNIQUE INDEX UX_nhacungcap_email ON nha_cung_cap(email) WHERE email IS NOT NULL;
GO


CREATE TRIGGER trg_cthd_tru_ton_khi_ban
ON chi_tiet_don_hang
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN don_hang dh ON dh.ma_don_hang = i.ma_don_hang
        WHERE dh.trang_thai <> 'CONFIRMED'
    )
    BEGIN
        RAISERROR (N'Không thể thêm sản phẩm vào đơn hàng đã hủy hoặc đã hoàn tất.', 16, 1);
        RETURN;
    END

    IF EXISTS (
        SELECT 1
        FROM (SELECT ma_sku, SUM(so_luong) AS tong_so_luong FROM inserted GROUP BY ma_sku) g
        INNER JOIN san_pham_sku sp ON sp.ma_sku = g.ma_sku
        WHERE sp.so_luong < g.tong_so_luong
    )
    BEGIN
        RAISERROR (N'Số lượng bán vượt quá tồn kho hiện có của SKU.', 16, 1);
        RETURN;
    END

    UPDATE sp
    SET sp.so_luong = sp.so_luong - g.tong_so_luong,
        sp.trang_thai_vong_doi = CASE WHEN sp.so_luong - g.tong_so_luong = 0
                                       THEN 'SOLD' ELSE sp.trang_thai_vong_doi END
    FROM san_pham_sku sp
    INNER JOIN (SELECT ma_sku, SUM(so_luong) AS tong_so_luong FROM inserted GROUP BY ma_sku) g
        ON sp.ma_sku = g.ma_sku;

    -- Trừ tồn từng kệ (ưu tiên kệ nhiều hàng).
    WITH g AS (
        SELECT ma_don_hang, ma_sku, SUM(so_luong) AS can_tru
        FROM inserted GROUP BY ma_don_hang, ma_sku
    ),
    k AS (
        SELECT tk.ma_sku, tk.ma_ke_hang, tk.so_luong,
               ROW_NUMBER() OVER (PARTITION BY tk.ma_sku ORDER BY tk.so_luong DESC, tk.ma_ke_hang) AS rn
        FROM ton_kho_ke_hang tk
        WHERE tk.so_luong > 0
          AND EXISTS (SELECT 1 FROM g WHERE g.ma_sku = tk.ma_sku)
    ),
    c AS (
        SELECT k.ma_sku, k.ma_ke_hang, k.so_luong, g.ma_don_hang, g.can_tru,
               SUM(k.so_luong) OVER (PARTITION BY g.ma_don_hang, k.ma_sku ORDER BY k.rn
                                     ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS da_chia_truoc
        FROM k INNER JOIN g ON g.ma_sku = k.ma_sku
    )
    SELECT ma_don_hang, ma_sku, ma_ke_hang,
           CASE WHEN can_tru - ISNULL(da_chia_truoc, 0) <= 0 THEN 0
                WHEN so_luong >= can_tru - ISNULL(da_chia_truoc, 0) THEN can_tru - ISNULL(da_chia_truoc, 0)
                ELSE so_luong END AS tru_duoc
    INTO #pb_tmp FROM c;

    DELETE FROM #pb_tmp WHERE tru_duoc <= 0;

    UPDATE tk SET tk.so_luong = tk.so_luong - a.tru_duoc
    FROM ton_kho_ke_hang tk
    INNER JOIN (SELECT ma_sku, ma_ke_hang, SUM(tru_duoc) AS tru_duoc
                FROM #pb_tmp GROUP BY ma_sku, ma_ke_hang) a
        ON a.ma_sku = tk.ma_sku AND a.ma_ke_hang = tk.ma_ke_hang;

    INSERT INTO phan_bo_xuat_kho_theo_ke (ma_don_hang, ma_sku, ma_ke_hang, so_luong)
    SELECT ma_don_hang, ma_sku, ma_ke_hang, tru_duoc FROM #pb_tmp;

    DROP TABLE #pb_tmp;

    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_don_hang)
    SELECT i.ma_sku, 'BAN_HANG', -SUM(i.so_luong), dh.ma_nhan_vien, i.ma_don_hang
    FROM inserted i
    INNER JOIN don_hang dh ON dh.ma_don_hang = i.ma_don_hang
    GROUP BY i.ma_sku, i.ma_don_hang, dh.ma_nhan_vien;
END;
GO

CREATE TRIGGER trg_donhang_kiemtra_khuyenmai
ON don_hang
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF TRIGGER_NESTLEVEL(OBJECT_ID('trg_donhang_kiemtra_khuyenmai')) > 1 RETURN;
    IF NOT UPDATE(ma_khuyen_mai) RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        WHERE i.ma_khuyen_mai IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM khuyen_mai km
              WHERE km.ma_khuyen_mai = i.ma_khuyen_mai
                AND km.trang_thai_hoat_dong = 1
                AND i.ngay_dat_hang BETWEEN km.ngay_bat_dau AND km.ngay_ket_thuc
                AND i.tong_tien_hang >= km.gia_tri_don_toi_thieu
          )
    )
    BEGIN
        RAISERROR (N'Khuyến mãi không hợp lệ: đã hết hạn, chưa được kích hoạt hoặc chưa đạt giá trị tối thiểu.', 16, 1);
        RETURN;
    END
END;
GO


-- 27. Hoàn thành đơn = đã thanh toán: vận đơn DA_GIAO -> đơn COMPLETED + PAID.
-- sửa các đơn COMPLETED cũ đang UNPAID thành PAID.
USE DO_AN_QLK;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE TRIGGER trg_vandon_dongbo_don_hang
ON van_don
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF TRIGGER_NESTLEVEL(OBJECT_ID('trg_vandon_dongbo_don_hang')) > 1 RETURN;

    IF UPDATE(trang_thai)
    BEGIN
        UPDATE dh
        SET dh.trang_thai = 'COMPLETED',
            dh.trang_thai_thanh_toan = 'PAID'
        FROM don_hang dh
        INNER JOIN inserted i ON i.ma_don_hang = dh.ma_don_hang
        INNER JOIN deleted d ON d.ma_van_don = i.ma_van_don
        WHERE i.trang_thai = 'DA_GIAO'
          AND d.trang_thai <> 'DA_GIAO'
          AND dh.trang_thai = 'CONFIRMED';
    END
END;
GO

UPDATE don_hang SET trang_thai_thanh_toan = 'PAID'
WHERE trang_thai = 'COMPLETED' AND trang_thai_thanh_toan = 'UNPAID';
GO

-- 28. Hồ sơ nhân viên: CCCD (12 số, duy nhất), ảnh thẻ 3x4, STK + chi nhánh ngân hàng.
USE DO_AN_QLK;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'nguoi_dung') AND name = N'cccd')
    ALTER TABLE nguoi_dung ADD cccd VARCHAR(12) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'nguoi_dung') AND name = N'anh_the')
    ALTER TABLE nguoi_dung ADD anh_the VARCHAR(255) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'nguoi_dung') AND name = N'stk_ngan_hang')
    ALTER TABLE nguoi_dung ADD stk_ngan_hang VARCHAR(30) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'nguoi_dung') AND name = N'chi_nhanh_ngan_hang')
    ALTER TABLE nguoi_dung ADD chi_nhanh_ngan_hang NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_nguoidung_cccd' AND object_id = OBJECT_ID(N'nguoi_dung'))
    CREATE UNIQUE INDEX UX_nguoidung_cccd ON nguoi_dung(cccd) WHERE cccd IS NOT NULL;
GO

CREATE PROCEDURE sp_CapNhatNhanVien
    @ma_nguoi_dung INT,
    @ma_nguoi_thuc_hien INT,
    @ten_dang_nhap VARCHAR(50),
    @ho_ten NVARCHAR(100),
    @vai_tro NVARCHAR(100),
    @email VARCHAR(255) = NULL,
    @so_dien_thoai VARCHAR(20) = NULL,
    @ngay_sinh DATE = NULL,
    @gioi_tinh NVARCHAR(10) = NULL,
    @thuong_tru NVARCHAR(255) = NULL,
    @cccd VARCHAR(12) = NULL,
    @anh_the VARCHAR(255) = NULL,
    @stk_ngan_hang VARCHAR(30) = NULL,
    @chi_nhanh_ngan_hang NVARCHAR(100) = NULL,
    @trang_thai_hoat_dong BIT = 1,
    @co_quyen_huy_don BIT = 0,
    @duoc_nhan_van_don BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM nguoi_dung WHERE ma_nguoi_dung = @ma_nguoi_dung)
    BEGIN
        RAISERROR (N'Không tìm thấy nhân viên này.', 16, 1);
        RETURN;
    END

    IF EXISTS (
        SELECT 1 FROM nguoi_dung
        WHERE ten_dang_nhap = @ten_dang_nhap AND ma_nguoi_dung <> @ma_nguoi_dung
    )
    BEGIN
        RAISERROR (N'Tên đăng nhập này đã tồn tại.', 16, 1);
        RETURN;
    END

    SET @email = NULLIF(LTRIM(RTRIM(@email)), N'');
    IF @email IS NOT NULL
    BEGIN
        IF NOT (@email LIKE N'%_@gmail.com' AND @email NOT LIKE N'% %' AND @email NOT LIKE N'%@%@%')
        BEGIN
            RAISERROR (N'Email phải có đuôi @gmail.com.', 16, 1);
            RETURN;
        END
        IF EXISTS (SELECT 1 FROM nguoi_dung WHERE email = @email AND ma_nguoi_dung <> @ma_nguoi_dung)
        BEGIN
            RAISERROR (N'Email này đã được sử dụng.', 16, 1);
            RETURN;
        END
    END

    SET @cccd = NULLIF(LTRIM(RTRIM(@cccd)), '');
    IF @cccd IS NOT NULL
    BEGIN
        IF LEN(@cccd) <> 12 OR @cccd LIKE '%[^0-9]%'
        BEGIN
            RAISERROR (N'CCCD phải gồm đúng 12 chữ số.', 16, 1);
            RETURN;
        END
        IF EXISTS (SELECT 1 FROM nguoi_dung WHERE cccd = @cccd AND ma_nguoi_dung <> @ma_nguoi_dung)
        BEGIN
            RAISERROR (N'CCCD này đã được sử dụng.', 16, 1);
            RETURN;
        END
    END

    DECLARE @ten_chuan NVARCHAR(100) = LTRIM(RTRIM(@vai_tro));
    IF @ten_chuan IS NULL OR @ten_chuan = N''
    BEGIN
        RAISERROR (N'Vai trò không được để trống.', 16, 1);
        RETURN;
    END

    DECLARE @ma_vai_tro INT;
    SELECT @ma_vai_tro = ma_vai_tro FROM vai_tro WHERE ten_vai_tro = @ten_chuan;
    IF @ma_vai_tro IS NULL
    BEGIN
        BEGIN TRY
            INSERT INTO vai_tro (ten_vai_tro, co_quyen_huy_don, duoc_nhan_van_don)
            VALUES (@ten_chuan, @co_quyen_huy_don, @duoc_nhan_van_don);
            SET @ma_vai_tro = SCOPE_IDENTITY();
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() IN (2601, 2627)
                SELECT @ma_vai_tro = ma_vai_tro FROM vai_tro WHERE ten_vai_tro = @ten_chuan;
            ELSE THROW;
        END CATCH
        IF @ma_vai_tro IS NULL
        BEGIN
            RAISERROR (N'Không xác định được vai trò.', 16, 1);
            RETURN;
        END
    END

    IF EXISTS (SELECT 1 FROM vai_tro WHERE ma_vai_tro = @ma_vai_tro AND co_quyen_huy_don = 1)
    AND EXISTS (
        SELECT 1 FROM nguoi_dung nd
        INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
        WHERE vt.co_quyen_huy_don = 1 AND nd.trang_thai_hoat_dong = 1
          AND nd.ma_nguoi_dung <> @ma_nguoi_dung
    )
    BEGIN
        RAISERROR (N'Không được có nhiều hơn 1 quản lý.', 16, 1);
        RETURN;
    END

    DECLARE @ho_ten_cu NVARCHAR(100), @ma_vaitro_cu INT, @sdt_cu VARCHAR(20),
            @ngaysinh_cu DATE, @gioitinh_cu NVARCHAR(10), @diachi_cu NVARCHAR(255),
            @trangthai_cu BIT, @email_cu VARCHAR(255),
            @cccd_cu VARCHAR(12), @stk_cu VARCHAR(30), @chinhanh_cu NVARCHAR(100);

    SELECT @ho_ten_cu = ho_ten, @ma_vaitro_cu = ma_vai_tro, @sdt_cu = so_dien_thoai,
           @ngaysinh_cu = ngay_sinh, @gioitinh_cu = gioi_tinh, @diachi_cu = thuong_tru,
           @trangthai_cu = trang_thai_hoat_dong, @email_cu = email,
           @cccd_cu = cccd, @stk_cu = stk_ngan_hang, @chinhanh_cu = chi_nhanh_ngan_hang
    FROM nguoi_dung WHERE ma_nguoi_dung = @ma_nguoi_dung;

    DECLARE @ten_vaitro_cu NVARCHAR(100);
    SELECT @ten_vaitro_cu = ten_vai_tro FROM vai_tro WHERE ma_vai_tro = @ma_vaitro_cu;

    UPDATE nguoi_dung
    SET ten_dang_nhap = @ten_dang_nhap,
        ho_ten = @ho_ten,
        ma_vai_tro = @ma_vai_tro,
        email = @email,
        so_dien_thoai = @so_dien_thoai,
        ngay_sinh = @ngay_sinh,
        gioi_tinh = @gioi_tinh,
        thuong_tru = @thuong_tru,
        cccd = @cccd,
        anh_the = COALESCE(@anh_the, anh_the),
        stk_ngan_hang = @stk_ngan_hang,
        chi_nhanh_ngan_hang = @chi_nhanh_ngan_hang,
        trang_thai_hoat_dong = @trang_thai_hoat_dong,
        ngay_cap_nhat = GETDATE()
    WHERE ma_nguoi_dung = @ma_nguoi_dung;

    INSERT INTO nhat_ky_thay_doi_nguoi_dung (ma_nguoi_dung, ma_nguoi_thuc_hien, ten_truong, gia_tri_cu, gia_tri_moi)
    SELECT @ma_nguoi_dung, @ma_nguoi_thuc_hien, v.ten_truong, v.gia_tri_cu, v.gia_tri_moi
    FROM (VALUES
        ('ho_ten', CAST(@ho_ten_cu AS NVARCHAR(255)), CAST(@ho_ten AS NVARCHAR(255))),
        ('vai_tro', CAST(@ten_vaitro_cu AS NVARCHAR(255)), CAST(@ten_chuan AS NVARCHAR(255))),
        ('email', CAST(@email_cu AS NVARCHAR(255)), CAST(@email AS NVARCHAR(255))),
        ('so_dien_thoai', CAST(@sdt_cu AS NVARCHAR(255)), CAST(@so_dien_thoai AS NVARCHAR(255))),
        ('ngay_sinh', CONVERT(NVARCHAR(255), @ngaysinh_cu, 23), CONVERT(NVARCHAR(255), @ngay_sinh, 23)),
        ('gioi_tinh', CAST(@gioitinh_cu AS NVARCHAR(255)), CAST(@gioi_tinh AS NVARCHAR(255))),
        ('thuong_tru', CAST(@diachi_cu AS NVARCHAR(255)), CAST(@thuong_tru AS NVARCHAR(255))),
        ('cccd', CAST(@cccd_cu AS NVARCHAR(255)), CAST(@cccd AS NVARCHAR(255))),
        ('stk_ngan_hang', CAST(@stk_cu AS NVARCHAR(255)), CAST(@stk_ngan_hang AS NVARCHAR(255))),
        ('chi_nhanh_ngan_hang', CAST(@chinhanh_cu AS NVARCHAR(255)), CAST(@chi_nhanh_ngan_hang AS NVARCHAR(255))),
        ('trang_thai_hoat_dong', CONVERT(NVARCHAR(255), @trangthai_cu), CONVERT(NVARCHAR(255), @trang_thai_hoat_dong))
    ) AS v(ten_truong, gia_tri_cu, gia_tri_moi)
    WHERE ISNULL(v.gia_tri_cu, N'') <> ISNULL(v.gia_tri_moi, N'');

    SELECT nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten, vt.ten_vai_tro AS vai_tro, nd.email,
           nd.so_dien_thoai, nd.ngay_sinh, nd.gioi_tinh, vt.duoc_nhan_van_don
    FROM nguoi_dung nd INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
    WHERE nd.ma_nguoi_dung = @ma_nguoi_dung;
END;
GO

-- sp_TaoNhanVien + sp_CapNhatNhanVien: nhận thêm 4 trường hồ sơ.
CREATE PROCEDURE sp_TaoNhanVien
    @ten_dang_nhap VARCHAR(50),
    @mat_khau_ma_hoa VARCHAR(255),
    @ho_ten NVARCHAR(100),
    @vai_tro NVARCHAR(100),
    @email VARCHAR(255) = NULL,
    @so_dien_thoai VARCHAR(20) = NULL,
    @ngay_sinh DATE = NULL,
    @gioi_tinh NVARCHAR(10) = NULL,
    @thuong_tru NVARCHAR(255) = NULL,
    @cccd VARCHAR(12) = NULL,
    @anh_the VARCHAR(255) = NULL,
    @stk_ngan_hang VARCHAR(30) = NULL,
    @chi_nhanh_ngan_hang NVARCHAR(100) = NULL,
    @co_quyen_huy_don BIT = 0,
    @duoc_nhan_van_don BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM nguoi_dung WHERE ten_dang_nhap = @ten_dang_nhap)
    BEGIN
        RAISERROR (N'Tên đăng nhập này đã tồn tại.', 16, 1);
        RETURN;
    END

    SET @email = NULLIF(LTRIM(RTRIM(@email)), N'');
    IF @email IS NOT NULL
    BEGIN
        IF NOT (@email LIKE N'%_@gmail.com' AND @email NOT LIKE N'% %' AND @email NOT LIKE N'%@%@%')
        BEGIN
            RAISERROR (N'Email phải có đuôi @gmail.com.', 16, 1);
            RETURN;
        END
        IF EXISTS (SELECT 1 FROM nguoi_dung WHERE email = @email)
        BEGIN
            RAISERROR (N'Email này đã được sử dụng.', 16, 1);
            RETURN;
        END
    END

    SET @cccd = NULLIF(LTRIM(RTRIM(@cccd)), '');
    IF @cccd IS NOT NULL
    BEGIN
        IF LEN(@cccd) <> 12 OR @cccd LIKE '%[^0-9]%'
        BEGIN
            RAISERROR (N'CCCD phải gồm đúng 12 chữ số.', 16, 1);
            RETURN;
        END
        IF EXISTS (SELECT 1 FROM nguoi_dung WHERE cccd = @cccd)
        BEGIN
            RAISERROR (N'CCCD này đã được sử dụng.', 16, 1);
            RETURN;
        END
    END

    DECLARE @ten_chuan NVARCHAR(100) = LTRIM(RTRIM(@vai_tro));
    IF @ten_chuan IS NULL OR @ten_chuan = N''
    BEGIN
        RAISERROR (N'Vai trò không được để trống.', 16, 1);
        RETURN;
    END

    DECLARE @ma_vai_tro INT;
    SELECT @ma_vai_tro = ma_vai_tro FROM vai_tro WHERE ten_vai_tro = @ten_chuan;
    IF @ma_vai_tro IS NULL
    BEGIN
        BEGIN TRY
            INSERT INTO vai_tro (ten_vai_tro, co_quyen_huy_don, duoc_nhan_van_don)
            VALUES (@ten_chuan, @co_quyen_huy_don, @duoc_nhan_van_don);
            SET @ma_vai_tro = SCOPE_IDENTITY();
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() IN (2601, 2627)
                SELECT @ma_vai_tro = ma_vai_tro FROM vai_tro WHERE ten_vai_tro = @ten_chuan;
            ELSE THROW;
        END CATCH
        IF @ma_vai_tro IS NULL
        BEGIN
            RAISERROR (N'Không xác định được vai trò.', 16, 1);
            RETURN;
        END
    END

    IF EXISTS (SELECT 1 FROM vai_tro WHERE ma_vai_tro = @ma_vai_tro AND co_quyen_huy_don = 1)
    AND EXISTS (
        SELECT 1 FROM nguoi_dung nd
        INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
        WHERE vt.co_quyen_huy_don = 1 AND nd.trang_thai_hoat_dong = 1
    )
    BEGIN
        RAISERROR (N'Không được có nhiều hơn 1 quản lý.', 16, 1);
        RETURN;
    END

    INSERT INTO nguoi_dung (ten_dang_nhap, mat_khau_ma_hoa, ho_ten, ma_vai_tro, email,
                             so_dien_thoai, ngay_sinh, gioi_tinh, thuong_tru,
                             cccd, anh_the, stk_ngan_hang, chi_nhanh_ngan_hang,
                             phai_doi_mat_khau)
    VALUES (@ten_dang_nhap, @mat_khau_ma_hoa, @ho_ten, @ma_vai_tro, @email,
            @so_dien_thoai, @ngay_sinh, @gioi_tinh, @thuong_tru,
            @cccd, @anh_the, @stk_ngan_hang, @chi_nhanh_ngan_hang,
            1);

    SELECT nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten, vt.ten_vai_tro AS vai_tro, nd.email,
           nd.so_dien_thoai, nd.ngay_sinh, nd.gioi_tinh, vt.duoc_nhan_van_don
    FROM nguoi_dung nd INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
    WHERE nd.ma_nguoi_dung = SCOPE_IDENTITY();
END;
GO
