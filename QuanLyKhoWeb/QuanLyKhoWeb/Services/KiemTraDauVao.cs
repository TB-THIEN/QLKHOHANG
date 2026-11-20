using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QuanLyKhoWeb.Services
{
    // Kiểm tra ô nhập tay dùng chung: tên không lẫn số, số nguyên đúng chuẩn
    public static class KiemTraDauVao
    {
        // Tên (sản phẩm, khách hàng, nhân viên, NCC, KM...) không được chứa số
        public static bool LaTenHopLe(string? ten)
            => !string.IsNullOrWhiteSpace(ten) && !ten.Any(char.IsDigit);

        // Gom lỗi binding kỹ thuật của các ô số nguyên (nhập chữ/số thập phân)
        // thành 1 cảnh báo tiếng Việt, gắn ngay dưới ô (fieldKey) để dễ thấy.
        // Chỉ báo khi key đó THỰC SỰ có lỗi binding/convert; nhập đúng (VD: 5) thì bỏ qua.
        // fieldKey rỗng thì gắn vào summary đầu trang.
        public static void BaoLoiSoNguyen(ModelStateDictionary modelState, string fieldKey,
            string thongBao = "Số lượng phải là số nguyên lớn hơn hoặc bằng 0 (không nhập chữ hay số thập phân).",
            params string[] keys)
        {
            var loi = keys
                .SelectMany(k => modelState.Keys.Where(x => x.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
                .Where(k => modelState[k] != null && modelState[k]!.Errors.Count > 0)
                .Distinct()
                .ToList();
            if (loi.Count == 0) return;
            foreach (var k in loi) modelState[k]?.Errors.Clear();
            modelState.AddModelError(fieldKey, thongBao);
        }
    }
}
