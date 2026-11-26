/* =====================================================================
   PATCH TỔNG HỢP — Sổ Kho (gộp toàn bộ thay đổi CSDL vào 1 file)
   ---------------------------------------------------------------------
   Gồm 3 nhóm việc:
     1. Đổi tên cờ tài xế: vai_tro.duoc_nhan_van_don -> vai_tro.giao_hang
        (kèm cập nhật sp_TaoNhanVien, sp_CapNhatNhanVien và
         trigger trg_vandon_kiemtra_nhanvien_giao_hang).
     2. Lỗi 2.1 — bán hàng phải trừ cả tồn KHU, không chỉ KỆ:
        thêm bảng phan_bo_xuat_kho_theo_khu + 3 trigger bán/hủy/xóa dòng.
     3. Dọn cờ giao_hang của các vai trò cũ trùng lặp (9, 13, 14)
        trên dữ liệu hiện có; vai trò chuẩn "Nhân viên giao hàng" giữ nguyên.

   Đặc điểm: chạy lại nhiều lần không lỗi (idempotent).
   Đã kiểm chứng trên DO_AN_QLK_TEST trước khi áp DO_AN_QLK.

   Cách chạy (file lưu UTF-8 có BOM để giữ tiếng Việt trong SP/trigger):
     sqlcmd -S '.\SQLEXPRESS' -E -C -d DO_AN_QLK -i patch_tong_hop.sql
   ===================================================================== */

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* =====================================================================
   1. ĐỔI TÊN CỜ TÀI XẾ: duoc_nhan_van_don -> giao_hang
   ===================================================================== */
IF COL_LENGTH(N'vai_tro', N'duoc_nhan_van_don') IS NOT NULL
   AND COL_LENGTH(N'vai_tro', N'giao_hang') IS NULL
BEGIN
    EXEC sp_rename N'dbo.vai_tro.duoc_nhan_van_don', N'giao_hang', N'COLUMN';
END
GO

-- Đổi tên ràng buộc mặc định cũ cho dễ đọc (không bắt buộc).
DECLARE @df SYSNAME, @sql NVARCHAR(MAX);
SELECT @df = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'vai_tro')
  AND c.name = N'giao_hang'
  AND dc.name LIKE N'DF__vai_tro__duoc_nh%';
IF @df IS NOT NULL
BEGIN
    SET @sql = N'EXEC sp_rename N''dbo.' + @df + N''', N''DF_vai_tro_giao_hang'', N''OBJECT'';';
    EXEC sp_executesql @sql;
END
GO

/* =====================================================================
   2. SP NHÂN VIÊN — dùng tham số @giao_hang
   ===================================================================== */
CREATE OR ALTER PROCEDURE sp_CapNhatNhanVien
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
    @giao_hang BIT = 0
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
            INSERT INTO vai_tro (ten_vai_tro, co_quyen_huy_don, giao_hang)
            VALUES (@ten_chuan, @co_quyen_huy_don, @giao_hang);
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
           nd.so_dien_thoai, nd.ngay_sinh, nd.gioi_tinh, vt.giao_hang
    FROM nguoi_dung nd INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
    WHERE nd.ma_nguoi_dung = @ma_nguoi_dung;
END;
GO

CREATE OR ALTER PROCEDURE sp_TaoNhanVien
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
    @giao_hang BIT = 0
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
            INSERT INTO vai_tro (ten_vai_tro, co_quyen_huy_don, giao_hang)
            VALUES (@ten_chuan, @co_quyen_huy_don, @giao_hang);
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
           nd.so_dien_thoai, nd.ngay_sinh, nd.gioi_tinh, vt.giao_hang
    FROM nguoi_dung nd INNER JOIN vai_tro vt ON vt.ma_vai_tro = nd.ma_vai_tro
    WHERE nd.ma_nguoi_dung = SCOPE_IDENTITY();
END;
GO


/* =====================================================================
   3. TRIGGER VẬN ĐƠN — kiểm tra cờ giao_hang
   ===================================================================== */
CREATE OR ALTER TRIGGER trg_vandon_kiemtra_nhanvien_giao_hang
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
          AND (vt.giao_hang = 0 OR nd.trang_thai_hoat_dong <> 1)
    )
    BEGIN
        RAISERROR (N'Người phụ trách phải là nhân viên giao hàng đang hoạt động.', 16, 1);
        RETURN;
    END
END;
GO

/* =====================================================================
   4. LỖI 2.1 — TỒN KHO THEO KHU
   ===================================================================== */

/* 4.1. Bảng phân bổ xuất kho theo khu (idempotent) */
IF OBJECT_ID(N'phan_bo_xuat_kho_theo_khu', N'U') IS NULL
BEGIN
    CREATE TABLE phan_bo_xuat_kho_theo_khu (
        ma_phan_bo INT IDENTITY(1,1) PRIMARY KEY,
        ma_don_hang INT NOT NULL,
        ma_sku INT NOT NULL,
        ma_khu_vuc INT NOT NULL,
        so_luong INT NOT NULL CHECK (so_luong > 0),
        CONSTRAINT FK_phanbo_khu_donhang FOREIGN KEY (ma_don_hang) REFERENCES don_hang(ma_don_hang) ON DELETE CASCADE,
        CONSTRAINT FK_phanbo_khu_sku FOREIGN KEY (ma_sku) REFERENCES san_pham_sku(ma_sku),
        CONSTRAINT FK_phanbo_khu_khuvuc FOREIGN KEY (ma_khu_vuc) REFERENCES khu_vuc_kho(ma_khu_vuc)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_phanbo_khu_sku_khuvuc'
               AND object_id = OBJECT_ID(N'phan_bo_xuat_kho_theo_khu'))
    CREATE INDEX IX_phanbo_khu_sku_khuvuc ON phan_bo_xuat_kho_theo_khu(ma_sku, ma_khu_vuc);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_phanbo_khu_khuvuc'
               AND object_id = OBJECT_ID(N'phan_bo_xuat_kho_theo_khu'))
    CREATE INDEX IX_phanbo_khu_khuvuc ON phan_bo_xuat_kho_theo_khu(ma_khu_vuc);
GO

/* 4.2. Bán hàng: trừ kệ trước, thiếu bao nhiêu trừ tiếp từ khu */
CREATE OR ALTER TRIGGER trg_cthd_tru_ton_khi_ban
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

    -- Phần kệ không đủ -> trừ tiếp tồn khu (hàng chưa xếp kệ).
    ;WITH g AS (
        SELECT ma_don_hang, ma_sku, SUM(so_luong) AS can_tru
        FROM inserted GROUP BY ma_don_hang, ma_sku
    ),
    da_tru AS (
        SELECT ma_don_hang, ma_sku, SUM(tru_duoc) AS tru_ke
        FROM #pb_tmp GROUP BY ma_don_hang, ma_sku
    ),
    can_khu AS (
        SELECT g.ma_don_hang, g.ma_sku, g.can_tru - ISNULL(t.tru_ke, 0) AS con_lai
        FROM g LEFT JOIN da_tru t ON t.ma_don_hang = g.ma_don_hang AND t.ma_sku = g.ma_sku
        WHERE g.can_tru - ISNULL(t.tru_ke, 0) > 0
    ),
    k AS (
        SELECT tk.ma_sku, tk.ma_khu_vuc, tk.so_luong,
               ROW_NUMBER() OVER (PARTITION BY tk.ma_sku ORDER BY tk.so_luong DESC, tk.ma_khu_vuc) AS rn
        FROM ton_kho_khu tk
        WHERE tk.so_luong > 0
          AND EXISTS (SELECT 1 FROM can_khu c WHERE c.ma_sku = tk.ma_sku)
    ),
    c AS (
        SELECT k.ma_sku, k.ma_khu_vuc, k.so_luong, c.ma_don_hang, c.con_lai,
               SUM(k.so_luong) OVER (PARTITION BY c.ma_don_hang, k.ma_sku ORDER BY k.rn
                                     ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS da_chia_truoc
        FROM k INNER JOIN can_khu c ON c.ma_sku = k.ma_sku
    )
    SELECT ma_don_hang, ma_sku, ma_khu_vuc,
           CASE WHEN con_lai - ISNULL(da_chia_truoc, 0) <= 0 THEN 0
                WHEN so_luong >= con_lai - ISNULL(da_chia_truoc, 0) THEN con_lai - ISNULL(da_chia_truoc, 0)
                ELSE so_luong END AS tru_duoc
    INTO #pb_khu_tmp FROM c;

    DELETE FROM #pb_khu_tmp WHERE tru_duoc <= 0;

    UPDATE tk SET tk.so_luong = tk.so_luong - a.tru_duoc
    FROM ton_kho_khu tk
    INNER JOIN (SELECT ma_sku, ma_khu_vuc, SUM(tru_duoc) AS tru_duoc
                FROM #pb_khu_tmp GROUP BY ma_sku, ma_khu_vuc) a
        ON a.ma_sku = tk.ma_sku AND a.ma_khu_vuc = tk.ma_khu_vuc;

    INSERT INTO phan_bo_xuat_kho_theo_khu (ma_don_hang, ma_sku, ma_khu_vuc, so_luong)
    SELECT ma_don_hang, ma_sku, ma_khu_vuc, tru_duoc FROM #pb_khu_tmp;

    DROP TABLE #pb_tmp;
    DROP TABLE #pb_khu_tmp;

    INSERT INTO nhat_ky_ton_kho (ma_sku, loai_bien_dong, so_luong_thay_doi, ma_nguoi_thuc_hien, ma_don_hang)
    SELECT i.ma_sku, 'BAN_HANG', -SUM(i.so_luong), dh.ma_nhan_vien, i.ma_don_hang
    FROM inserted i
    INNER JOIN don_hang dh ON dh.ma_don_hang = i.ma_don_hang
    GROUP BY i.ma_sku, i.ma_don_hang, dh.ma_nhan_vien;
END;
GO

CREATE OR ALTER TRIGGER trg_donhang_hoan_ton_khi_huy
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

        -- Hoàn tồn khu đã trừ khi bán.
        UPDATE tk
        SET tk.so_luong = tk.so_luong + pb.so_luong
        FROM ton_kho_khu tk
        INNER JOIN phan_bo_xuat_kho_theo_khu pb ON pb.ma_sku = tk.ma_sku AND pb.ma_khu_vuc = tk.ma_khu_vuc
        INNER JOIN inserted i ON i.ma_don_hang = pb.ma_don_hang
        INNER JOIN deleted d ON d.ma_don_hang = i.ma_don_hang
        WHERE i.trang_thai = 'CANCELLED' AND d.trang_thai <> 'CANCELLED';

        DELETE pb
        FROM phan_bo_xuat_kho_theo_khu pb
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

CREATE OR ALTER TRIGGER trg_cthd_hoan_ton_khi_xoa_dong
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

    -- Hoàn tồn khu đã trừ khi bán.
    UPDATE tk
    SET tk.so_luong = tk.so_luong + pb.so_luong
    FROM ton_kho_khu tk
    INNER JOIN phan_bo_xuat_kho_theo_khu pb ON pb.ma_sku = tk.ma_sku AND pb.ma_khu_vuc = tk.ma_khu_vuc
    INNER JOIN (
        SELECT DISTINCT d.ma_don_hang, d.ma_sku
        FROM deleted d
        LEFT JOIN don_hang dh ON dh.ma_don_hang = d.ma_don_hang
        WHERE dh.ma_don_hang IS NULL OR dh.trang_thai <> 'CANCELLED'
    ) x ON x.ma_don_hang = pb.ma_don_hang AND x.ma_sku = pb.ma_sku;

    DELETE pb
    FROM phan_bo_xuat_kho_theo_khu pb
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

/* =====================================================================
   5. DỌN DỮ LIỆU CŨ — tắt cờ giao_hang của các vai trò trùng lặp cũ.
      Chỉ áp cho các vai trò có sẵn trên DB hiện tại; DB mới không bị ảnh hưởng.
   ===================================================================== */
UPDATE vai_tro
SET giao_hang = 0
WHERE giao_hang = 1
  AND ten_vai_tro IN (N'nhân viên bảo vệ', N'Nhan vien ban hang', N'Nhan vien kho');
GO

/* =====================================================================
   6. KIỂM TRA NHANH SAU KHI CHẠY
   ===================================================================== */
SELECT ma_vai_tro, ten_vai_tro, co_quyen_huy_don, giao_hang
FROM vai_tro ORDER BY ma_vai_tro;

SELECT name, is_disabled
FROM sys.triggers
WHERE name IN (N'trg_vandon_kiemtra_nhanvien_giao_hang',
               N'trg_cthd_tru_ton_khi_ban',
               N'trg_donhang_hoan_ton_khi_huy',
               N'trg_cthd_hoan_ton_khi_xoa_dong')
ORDER BY name;

SELECT COUNT(*) AS so_cot_bang_khu
FROM sys.columns WHERE object_id = OBJECT_ID(N'phan_bo_xuat_kho_theo_khu');
GO