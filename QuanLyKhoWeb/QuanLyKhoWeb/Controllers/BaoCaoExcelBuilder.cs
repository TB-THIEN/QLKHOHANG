using System.Globalization;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services;

namespace QuanLyKhoWeb.Controllers
{
    /// <summary>
    /// Sinh file Excel .xlsx gộp cho trang Báo cáo:
    ///  - Sheet 1 "Tổng hợp 12 tháng": mỗi tháng gồm số đơn, tổng số hàng,
    ///    doanh thu, tỷ trọng, lũy kế và doanh thu bình quân/đơn.
    ///  - Xuất theo quý: sheet "Tổng hợp quý X" gồm 3 tháng của quý và 1 dòng
    ///    tổng hợp quý ở cuối.
    ///  - Sheet 2 "Chi tiết tháng X": danh sách đơn (và phiếu nhập) của tháng
    ///    người dùng đang chọn. Chỉ có khi trang Báo cáo đang lọc theo tháng.
    /// Không còn phần "Nhận xét nhanh" trong file.
    /// </summary>
    public static class BaoCaoExcelBuilder
    {
        private static readonly CultureInfo Vi = ExcelReportBuilder.Vi;

        public static string TenFile(BaoCaoVm vm)
        {
            var ten = vm.Quy is int q
                ? $"BaoCaoTongHop_Quy{q}_{vm.Nam}"
                : vm.Thang is int t
                    ? $"BaoCaoTongHop_Thang{t}_{vm.Nam}"
                    : $"BaoCaoTongHop_Nam{vm.Nam}";
            return ten + ".xlsx";
        }

        public static byte[] XayDung(BaoCaoVm vm)
        {
            var dsSheet = new List<OSheet>();
            if (vm.Quy is int quy)
                dsSheet.Add(DungTongHopQuy(vm, quy));
            else
                dsSheet.Add(DungTongHop(vm));

            if (vm.Thang is int thang)
                dsSheet.Add(DungChiTietThang(vm, thang));

            return ExcelWriter.Ghi(dsSheet);
        }

        // =====================================================================
        //  SHEET 1 — TỔNG HỢP 12 THÁNG
        // =====================================================================
        private static OSheet DungTongHop(BaoCaoVm vm)
        {
            var b = new ExcelReportBuilder(new double[] { 15, 12, 17, 15, 13, 17, 14 }, "VNĐ")
            {
                TenSheet = $"Tổng hợp 12 tháng {vm.Nam}"
            };

            var tongDon = vm.DoanhThuThang.Sum(x => x.SoDon);
            var tongHang = vm.DoanhThuThang.Sum(x => x.TongSoHang);
            var tbDon = tongDon > 0 ? vm.DoanhThuCaNam / tongDon : 0m;

            b.TieuDeHanhChinh(
                $"BÁO CÁO TỔNG HỢP KINH DOANH NĂM {vm.Nam}",
                "PHÒNG KINH DOANH - KHO VẬN",
                ("Kỳ báo cáo", $"12 tháng năm {vm.Nam}"),
                ("Tổng doanh thu", vm.DoanhThuCaNam.ToString("N0", Vi) + " đ"),
                ("Tổng số đơn", tongDon.ToString("N0", Vi) + " đơn"),
                ("Tổng số hàng", tongHang.ToString("N0", Vi) + " sản phẩm"),
                ("Doanh thu bình quân/đơn", tbDon.ToString("N0", Vi) + " đ"));

            b.AddRow(new() { [0] = ($"Bảng tổng hợp 12 tháng", OStyle.BoldLeft) });
            b.Merge($"A{b.DongHienTai}:{b.Col(b.SoCot - 1)}{b.DongHienTai}");
            b.Empty();
            b.Header(new[] { "Tháng", "Số đơn", "Tổng số hàng", "Doanh thu", "Tỷ trọng", "Lũy kế", "TB/đơn" });

            decimal luyKe = 0;
            foreach (var t in vm.DoanhThuThang)
            {
                luyKe += t.DoanhThu;
                var tyTrong = vm.DoanhThuCaNam > 0 ? Math.Round(t.DoanhThu / vm.DoanhThuCaNam * 100, 1) : 0m;
                b.AddRow(new()
                {
                    [0] = ($"Tháng {t.Thang}", OStyle.TextCenter),
                    [1] = (t.SoDon, OStyle.Integer),
                    [2] = (t.TongSoHang, OStyle.Integer),
                    [3] = ((double)t.DoanhThu, OStyle.Money),
                    [4] = ((double)(tyTrong / 100m), OStyle.Percent),
                    [5] = ((double)luyKe, OStyle.Money),
                    [6] = (t.SoDon > 0 ? (double)(t.DoanhThu / t.SoDon) : 0d, OStyle.Money)
                });
            }

            b.AddRow(new()
            {
                [0] = ("TỔNG CỘNG", OStyle.TotalCenter),
                [1] = (tongDon, OStyle.IntegerTotal),
                [2] = (tongHang, OStyle.IntegerTotal),
                [3] = ((double)vm.DoanhThuCaNam, OStyle.MoneyTotal),
                [4] = (1d, OStyle.PercentTotal),
                [5] = ((double)vm.DoanhThuCaNam, OStyle.MoneyTotal),
                [6] = ((double)tbDon, OStyle.MoneyTotal)
            });

            b.GhiChuNguon("Cơ sở dữ liệu bán hàng nội bộ, số liệu chốt đến thời điểm xuất file.");
            b.ChuKy("NGƯỜI LẬP BÁO CÁO", "TRƯỞNG PHÒNG KINH DOANH");
            return b.ThanhSheet();
        }

        // =====================================================================
        //  SHEET TỔNG HỢP QUÝ — 3 tháng của quý + 1 dòng tổng hợp quý ở cuối
        // =====================================================================
        private static OSheet DungTongHopQuy(BaoCaoVm vm, int quy)
        {
            int thangDau = (quy - 1) * 3 + 1;
            int thangCuoi = thangDau + 2;
            var cacThang = vm.DoanhThuThang
                .Where(t => t.Thang >= thangDau && t.Thang <= thangCuoi)
                .OrderBy(t => t.Thang)
                .ToList();

            var b = new ExcelReportBuilder(new double[] { 15, 12, 17, 15, 13, 17, 14 }, "VNĐ")
            {
                TenSheet = $"Tổng hợp quý {quy} năm {vm.Nam}"
            };

            var tongDon = cacThang.Sum(x => x.SoDon);
            var tongHang = cacThang.Sum(x => x.TongSoHang);
            var tongDoanhThu = cacThang.Sum(x => x.DoanhThu);
            var tbDon = tongDon > 0 ? tongDoanhThu / tongDon : 0m;

            b.TieuDeHanhChinh(
                $"BÁO CÁO TỔNG HỢP KINH DOANH QUÝ {quy} NĂM {vm.Nam}",
                "PHÒNG KINH DOANH - KHO VẬN",
                ("Kỳ báo cáo", $"Quý {quy}/{vm.Nam} (tháng {thangDau} - {thangCuoi})"),
                ("Tổng doanh thu", tongDoanhThu.ToString("N0", Vi) + " đ"),
                ("Tổng số đơn", tongDon.ToString("N0", Vi) + " đơn"),
                ("Tổng số hàng", tongHang.ToString("N0", Vi) + " sản phẩm"),
                ("Doanh thu bình quân/đơn", tbDon.ToString("N0", Vi) + " đ"));

            b.AddRow(new() { [0] = ($"Bảng tổng hợp quý {quy} năm {vm.Nam}", OStyle.BoldLeft) });
            b.Merge($"A{b.DongHienTai}:{b.Col(b.SoCot - 1)}{b.DongHienTai}");
            b.Empty();
            b.Header(new[] { "Tháng", "Số đơn", "Tổng số hàng", "Doanh thu", "Tỷ trọng", "Lũy kế", "TB/đơn" });

            decimal luyKe = 0;
            foreach (var t in cacThang)
            {
                luyKe += t.DoanhThu;
                var tyTrong = tongDoanhThu > 0 ? Math.Round(t.DoanhThu / tongDoanhThu * 100, 1) : 0m;
                b.AddRow(new()
                {
                    [0] = ($"Tháng {t.Thang}", OStyle.TextCenter),
                    [1] = (t.SoDon, OStyle.Integer),
                    [2] = (t.TongSoHang, OStyle.Integer),
                    [3] = ((double)t.DoanhThu, OStyle.Money),
                    [4] = ((double)(tyTrong / 100m), OStyle.Percent),
                    [5] = ((double)luyKe, OStyle.Money),
                    [6] = (t.SoDon > 0 ? (double)(t.DoanhThu / t.SoDon) : 0d, OStyle.Money)
                });
            }

            // Dòng tổng hợp quý nằm ở cuối bảng, cộng lại đúng 3 tháng bên trên.
            b.AddRow(new()
            {
                [0] = ($"Quý {quy}", OStyle.TotalCenter),
                [1] = (tongDon, OStyle.IntegerTotal),
                [2] = (tongHang, OStyle.IntegerTotal),
                [3] = ((double)tongDoanhThu, OStyle.MoneyTotal),
                [4] = (1d, OStyle.PercentTotal),
                [5] = ((double)tongDoanhThu, OStyle.MoneyTotal),
                [6] = ((double)tbDon, OStyle.MoneyTotal)
            });

            b.GhiChuNguon("Cơ sở dữ liệu bán hàng nội bộ, số liệu chốt đến thời điểm xuất file.");
            b.ChuKy("NGƯỜI LẬP BÁO CÁO", "TRƯỞNG PHÒNG KINH DOANH");
            return b.ThanhSheet();
        }

        // =====================================================================
        //  SHEET 2 — CHI TIẾT THÁNG ĐANG CHỌN
        // =====================================================================
        private static OSheet DungChiTietThang(BaoCaoVm vm, int thang)
        {
            var b = new ExcelReportBuilder(new double[] { 5, 16, 30, 14, 17, 16, 20, 9, 15, 14 }, "VNĐ")
            {
                TenSheet = $"Chi tiết tháng {thang}"
            };

            var dsDon = vm.DonHangChiTiet;
            var donHoanTat = dsDon.Where(x => x.TrangThai == "Hoàn tất").ToList();
            var tongHang = dsDon.Sum(x => x.SoLuong);
            var tongTien = dsDon.Sum(x => x.ThanhTien);
            var hangHoanTat = donHoanTat.Sum(x => x.SoLuong);
            var tienHoanTat = donHoanTat.Sum(x => x.ThanhTien);

            b.TieuDeHanhChinh(
                $"CHI TIẾT ĐƠN HÀNG THÁNG {thang}/{vm.Nam}",
                "PHÒNG KINH DOANH - KHO VẬN",
                ("Kỳ báo cáo", $"Tháng {thang}/{vm.Nam}"),
                ("Số đơn phát sinh", dsDon.Count.ToString("N0", Vi) + " đơn"),
                ("Số đơn hoàn tất", donHoanTat.Count.ToString("N0", Vi) + " đơn"),
                ("Tổng số hàng (đơn hoàn tất)", hangHoanTat.ToString("N0", Vi) + " sản phẩm"),
                ("Tổng thanh toán (đơn hoàn tất)", tienHoanTat.ToString("N0", Vi) + " đ"));

            b.AddRow(new() { [0] = ($"Danh sách đơn hàng tháng {thang}/{vm.Nam}", OStyle.BoldLeft) });
            b.Merge($"A{b.DongHienTai}:{b.Col(b.SoCot - 1)}{b.DongHienTai}");
            b.Empty();
            b.Header(new[] { "STT", "Mã đơn", "Khách hàng", "Điện thoại", "Ngày đặt", "Hình thức", "Nhân viên", "SL", "Thanh tiền", "Trạng thái" });

            int stt = 1;
            foreach (var d in dsDon)
            {
                b.AddRow(new()
                {
                    [0] = ((stt++).ToString(), OStyle.TextCenter),
                    [1] = (d.MaCodeDonHang, OStyle.TextCenter),
                    [2] = (d.KhachHang, OStyle.TextLeft),
                    [3] = (d.DienThoai, OStyle.TextCenter),
                    [4] = (d.NgayDat.ToString("dd/MM/yyyy HH:mm"), OStyle.TextCenter),
                    [5] = (d.HinhThuc, OStyle.TextCenter),
                    [6] = (d.NhanVien, OStyle.TextLeft),
                    [7] = (d.SoLuong, OStyle.Integer),
                    [8] = ((double)d.ThanhTien, OStyle.MoneyBold),
                    [9] = (d.TrangThai, OStyle.TextCenter)
                });
            }

            if (dsDon.Count == 0)
            {
                b.AddRow(new() { [0] = ($"Không có đơn hàng nào trong tháng {thang}/{vm.Nam}.", OStyle.TextCenter) });
            }
            else
            {
                b.AddRow(new()
                {
                    [0] = ("TỔNG CỘNG", OStyle.TotalCenter),
                    [2] = ($"{dsDon.Count} đơn · {donHoanTat.Count} đơn hoàn tất", OStyle.TotalLeft),
                    [3] = ("", OStyle.TotalCenter),
                    [4] = ("", OStyle.TotalCenter),
                    [5] = ("", OStyle.TotalCenter),
                    [6] = ("", OStyle.TotalLeft),
                    [7] = (tongHang, OStyle.IntegerTotal),
                    [8] = ((double)tongTien, OStyle.MoneyTotal),
                    [9] = ("", OStyle.TotalCenter)
                });
                b.Merge($"A{b.DongHienTai}:B{b.DongHienTai}");

                b.AddRow(new()
                {
                    [0] = ($"Ghi chú: sheet Tổng hợp 12 tháng chỉ tính đơn Hoàn tất — tháng {thang}: "
                           + $"{donHoanTat.Count} đơn / {hangHoanTat} SP / {tienHoanTat.ToString("N0", Vi)} đ.", OStyle.Meta)
                });
                b.Merge($"A{b.DongHienTai}:{b.Col(b.SoCot - 1)}{b.DongHienTai}");
            }

            if (vm.PhieuNhapChiTiet.Count > 0)
            {
                b.Empty(2);
                b.AddRow(new() { [0] = ($"Phiếu nhập kho tháng {thang}/{vm.Nam}", OStyle.BoldLeft) });
                b.Merge($"A{b.DongHienTai}:{b.Col(b.SoCot - 1)}{b.DongHienTai}");
                b.Empty();
                b.Header(new[] { "STT", "Mã phiếu", "Nhà cung cấp", "Số dòng", "Ngày nhập", "Người tạo", "SL", "Giá trị nhập", "Trạng thái" });

                int sttP = 1;
                foreach (var p in vm.PhieuNhapChiTiet)
                {
                    b.AddRow(new()
                    {
                        [0] = ((sttP++).ToString(), OStyle.TextCenter),
                        [1] = (p.MaCodePhieuNhap, OStyle.TextCenter),
                        [2] = (p.NhaCungCap, OStyle.TextLeft),
                        [3] = (p.SoDong, OStyle.Integer),
                        [4] = (p.NgayNhap.ToString("dd/MM/yyyy HH:mm"), OStyle.TextCenter),
                        [5] = (p.NguoiTao, OStyle.TextLeft),
                        [6] = (p.SoLuong, OStyle.Integer),
                        [7] = ((double)p.TongTien, OStyle.MoneyBold),
                        [8] = (p.TrangThai, OStyle.TextCenter)
                    });
                }

                b.AddRow(new()
                {
                    [0] = ("TỔNG CỘNG", OStyle.TotalCenter),
                    [1] = ("", OStyle.TotalLeft),
                    [2] = ($"{vm.PhieuNhapChiTiet.Count} phiếu", OStyle.TotalLeft),
                    [3] = (vm.PhieuNhapChiTiet.Sum(x => x.SoDong), OStyle.IntegerTotal),
                    [4] = ("", OStyle.TotalCenter),
                    [5] = ("", OStyle.TotalLeft),
                    [6] = (vm.PhieuNhapChiTiet.Sum(x => x.SoLuong), OStyle.IntegerTotal),
                    [7] = ((double)vm.PhieuNhapChiTiet.Sum(x => x.TongTien), OStyle.MoneyTotal),
                    [8] = ("", OStyle.TotalCenter)
                });
                b.Merge($"A{b.DongHienTai}:B{b.DongHienTai}");
            }

            b.GhiChuNguon("Cơ sở dữ liệu bán hàng nội bộ, số liệu chốt đến thời điểm xuất file.");
            b.ChuKy("NGƯỜI LẬP BÁO CÁO", "TRƯỞNG PHÒNG KINH DOANH");
            return b.ThanhSheet();
        }
    }
}
