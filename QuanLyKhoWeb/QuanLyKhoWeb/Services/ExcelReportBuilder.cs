using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace QuanLyKhoWeb.Services
{
    public enum OStyle
    {
        Normal = 0,
        Title = 1,
        Agency = 2,
        Meta = 3,
        Header = 4,
        TextLeft = 5,
        Number = 6,
        NumberBold = 7,
        TextCenter = 8,
        CenterBold = 9,
        TotalLeft = 10,
        TotalNumber = 11,
        TotalCenter = 12,
        BoldLeft = 13,
        MetaRight = 14,
        AgencyRight = 15,

        // Kiểu SỐ THẬT: ô chứa <v> số, định dạng bằng numFmt để Excel tính toán được.
        Money = 16,          // #,##0 ; âm trong ngoặc ; 0 thành "-"
        MoneyBold = 17,
        MoneyTotal = 18,
        Percent = 19,        // 0.0% (giá trị lưu dạng phân số)
        PercentBold = 20,
        PercentTotal = 21,
        Integer = 22,
        IntegerBold = 23,
        IntegerTotal = 24,
        MetaLeft = 25,       // ghi chú in nghiêng canh trái (dưới ô chữ ký)
        BoldRight = 26,      // nhãn đậm canh phải (khối thông tin nửa phải)
        TextPlain = 27,      // chữ thường không khung (giá trị trong khối thông tin, tên người ký)
        CenterBoldPlain = 28 // tiêu đề ô chữ ký: đậm, canh giữa, không khung
    }

    public sealed class OCell
    {
        /// <summary>Giá trị dạng chuỗi (dùng cho ô chữ).</summary>
        public string Value { get; }

        /// <summary>Giá trị số thật (dùng cho ô số/tiền/phần trăm). Null nếu là ô chữ.</summary>
        public double? Number { get; }

        public OStyle Style { get; }

        public OCell(string value, OStyle style)
        {
            Value = value ?? "";
            Style = style;
        }

        public OCell(double number, OStyle style)
        {
            Value = "";
            Number = number;
            Style = style;
        }
    }

    public sealed class ORow
    {
        public int Index { get; }
        public int? Height { get; set; }
        public Dictionary<int, OCell> Cells { get; }

        public ORow(int index, Dictionary<int, OCell> cells)
        {
            Index = index;
            Cells = cells;
        }

        public static ORow Empty(int index) => new(index, new Dictionary<int, OCell>());
    }

    /// <summary>Một trang tính đã dựng xong, chờ ghi vào workbook.</summary>
    public sealed class OSheet
    {
        public string Ten { get; set; } = "BaoCao";
        public List<ORow> Rows { get; set; } = new();
        public List<double> Widths { get; set; } = new();
        public List<string> Merges { get; set; } = new();
    }

    /// <summary>
    /// Bộ dựng file Excel .xlsx (OOXML) dùng chung cho các trang báo cáo:
    /// tiêu đề hành chính, bảng kẻ khung, dòng tổng cộng và hai ô chữ ký.
    /// Không dùng thư viện ngoài, không dùng HTML giả .xls.
    /// </summary>
    public class ExcelReportBuilder
    {
        public static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly List<ORow> _rows = new();
        private readonly List<string> _merges = new();
        private readonly List<double> _widths;
        private readonly int _soCot;
        private readonly int _kyTrai;
        private readonly string _donVi;
        private int _r;

        public string TenSheet { get; set; } = "BaoCao";

        public ExcelReportBuilder(IEnumerable<double> widths, string donVi = "VNĐ")
        {
            _widths = widths.ToList();
            _soCot = Math.Max(1, _widths.Count);
            _kyTrai = Math.Max(1, (int)Math.Floor(_soCot * 0.42));
            _donVi = donVi;
        }

        public int SoCot => _soCot;
        public int DongHienTai => _r;

        // ---------------------------------------------------------------- ghi ô

        /// <summary>Ghi một dòng với các ô dạng (chữ, kiểu) hoặc (số, kiểu).</summary>
        public void AddRow(Dictionary<int, (object? v, OStyle s)> cells, int? height = null)
        {
            var d = new Dictionary<int, OCell>();
            foreach (var kv in cells)
            {
                var (v, style) = kv.Value;
                d[kv.Key] = v switch
                {
                    null => new OCell("", style),
                    string str => new OCell(str, style),
                    int i => new OCell((double)i, style),
                    long l => new OCell((double)l, style),
                    decimal m => new OCell((double)m, style),
                    double db => new OCell(db, style),
                    float f => new OCell((double)f, style),
                    _ => new OCell(v.ToString() ?? "", style)
                };
            }
            _rows.Add(new ORow(_r, d) { Height = height });
            _r++;
        }

        public void AddCells(Dictionary<int, OCell> cells, int? height = null)
        {
            _rows.Add(new ORow(_r, cells) { Height = height });
            _r++;
        }

        public void Empty(int soDong = 1)
        {
            for (int i = 0; i < soDong; i++) _rows.Add(ORow.Empty(_r++));
        }

        public void Header(string[] ten)
        {
            var d = new Dictionary<int, OCell>();
            for (int i = 0; i < ten.Length; i++) d[i] = new OCell(ten[i], OStyle.Header);
            _rows.Add(new ORow(_r++, d) { Height = 34 });
        }

        public void Merge(string refRange) => _merges.Add(refRange);

        public string Col(int i)
        {
            var s = "";
            i++;
            while (i > 0)
            {
                var m = (i - 1) % 26;
                s = (char)('A' + m) + s;
                i = (i - 1) / 26;
            }
            return s;
        }

        // ---------------------------------------------------------------- phần đầu báo cáo
        /// <param name="tieuDe">Tiêu đề lớn</param>
        /// <param name="tenDonVi">Dòng đơn vị bên trái (vd: PHÒNG KINH DOANH - KHO VẬN)</param>
        /// <param name="thongTinPhamVi">Các cặp nhãn/giá trị, xếp 2 cột: lẻ bên trái, chẵn bên phải</param>
        public void TieuDeHanhChinh(string tieuDe, string tenDonVi, params (string nhan, string giaTri)[] thongTinPhamVi)
        {
            int cotPhai = CotKhoiPhai;

            // Khối đầu: đơn vị (trái) — quốc hiệu (phải), mỗi bên merge để chữ không bị cắt
            AddRow(new()
            {
                [0] = ("CỬA HÀNG QUẢN LÝ KHO", OStyle.Agency),
                [cotPhai] = ("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", OStyle.AgencyRight)
            });
            Merge($"A{_r}:{Col(cotPhai - 1)}{_r}");
            Merge($"{Col(cotPhai)}{_r}:{Col(_soCot - 1)}{_r}");

            AddRow(new()
            {
                [0] = (tenDonVi, OStyle.BoldLeft),
                [cotPhai] = ("Độc lập - Tự do - Hạnh phúc", OStyle.MetaRight)
            });
            Merge($"A{_r}:{Col(cotPhai - 1)}{_r}");
            Merge($"{Col(cotPhai)}{_r}:{Col(_soCot - 1)}{_r}");

            Empty();

            // Tiêu đề báo cáo canh giữa toàn bảng
            AddRow(new() { [0] = (tieuDe, OStyle.Title) }, 30);
            Merge($"A{_r}:{Col(_soCot - 1)}{_r}");

            var ngayXuat = DateTime.Now.ToString("HH:mm 'ngày' dd/MM/yyyy", Vi);
            AddRow(new() { [0] = ($"(Thời điểm xuất file: {ngayXuat}  •  Đơn vị tính: {_donVi})", OStyle.Meta) });
            Merge($"A{_r}:{Col(_soCot - 1)}{_r}");

            Empty();

            // Khối thông tin: mỗi dòng 2 cặp "nhãn: giá trị" song song (nửa trái / nửa phải).
            // Bề rộng nhãn lấy theo nhãn dài nhất của từng cột để không nhãn nào bị xuống dòng.
            int nhanDaiNhatTrai = 0, nhanDaiNhatPhai = 0;
            for (int i = 0; i < thongTinPhamVi.Length; i++)
            {
                if (i % 2 == 0) nhanDaiNhatTrai = Math.Max(nhanDaiNhatTrai, thongTinPhamVi[i].nhan.Length);
                else nhanDaiNhatPhai = Math.Max(nhanDaiNhatPhai, thongTinPhamVi[i].nhan.Length);
            }

            var trai = ChiaO(0, cotPhai - 1, nhanDaiNhatTrai);
            var phai = ChiaO(cotPhai, _soCot - 1, nhanDaiNhatPhai);
            bool coNuaTrai = trai.HasValue;
            bool coNuaPhai = phai.HasValue;

            void Noi(int tu, int den)
            {
                if (den > tu) Merge($"{Col(tu)}{_r}:{Col(den)}{_r}");
            }

            for (int i = 0; i < thongTinPhamVi.Length; i += 2)
            {
                var o = new Dictionary<int, (object?, OStyle)>();
                if (coNuaTrai)
                {
                    o[trai!.Value.nhanTu] = (thongTinPhamVi[i].nhan + ":", OStyle.BoldLeft);
                    o[trai.Value.giaTriTu] = (thongTinPhamVi[i].giaTri, OStyle.TextPlain);
                }
                else
                {
                    o[0] = (thongTinPhamVi[i].nhan + ": " + thongTinPhamVi[i].giaTri, OStyle.BoldLeft);
                }

                bool coCapPhai = coNuaPhai && i + 1 < thongTinPhamVi.Length;
                if (coCapPhai)
                {
                    o[phai!.Value.nhanTu] = (thongTinPhamVi[i + 1].nhan + ":", OStyle.BoldRight);
                    o[phai.Value.giaTriTu] = (thongTinPhamVi[i + 1].giaTri, OStyle.TextPlain);
                }
                AddRow(o);

                if (coNuaTrai)
                {
                    Noi(trai!.Value.nhanTu, trai.Value.nhanDen);
                    Noi(trai.Value.giaTriTu, coCapPhai ? cotPhai - 1 : _soCot - 1);
                }
                else if (_soCot > 1)
                {
                    Noi(0, _soCot - 1);
                }
                if (coCapPhai)
                {
                    Noi(phai!.Value.nhanTu, phai.Value.nhanDen);
                    Noi(phai.Value.giaTriTu, _soCot - 1);
                }
            }
            Empty(2);
        }

        // Tách một dải cột thành ô nhãn và ô giá trị: ưu tiên đủ chỗ cho nhãn (theo số ký tự),
        // nhưng vẫn giữ ô giá trị tối thiểu để số tiền không bị cắt.
        private (int nhanTu, int nhanDen, int giaTriTu, int giaTriDen)? ChiaO(int tu, int den, int soKyTuNhan)
        {
            if (den - tu < 1) return null;

            double tong = 0;
            for (int i = tu; i <= den; i++) tong += _widths[i];

            // 1 ký tự Times New Roman 10pt ~ 0.85 đơn vị bề rộng cột
            double canNhan = Math.Max(8, soKyTuNhan * 0.85 + 2);
            double gioiHan = tong * 0.55;
            double mucTieu = Math.Min(canNhan, gioiHan);
            mucTieu = Math.Max(mucTieu, tong * 0.3);

            double cong = 0;
            int cat = tu;
            for (int i = tu; i < den; i++)
            {
                cong += _widths[i];
                cat = i;
                if (cong >= mucTieu) break;
            }
            return (tu, cat, cat + 1, den);
        }

        // Cột bắt đầu khối phải (quốc hiệu, thông tin phụ) — khoảng 55% chiều rộng bảng
        private int CotKhoiPhai => Math.Max(1, Math.Min(_soCot - 2, (int)Math.Floor(_soCot * 0.55)));

        /// <summary>Ghi chú nguồn dữ liệu, in ngay dưới bảng số liệu.</summary>
        public void GhiChuNguon(string noiDung)
        {
            Empty();
            AddRow(new() { [0] = ("Nguồn: " + noiDung, OStyle.Meta) });
            Merge($"A{_r}:{Col(_soCot - 1)}{_r}");
        }

        // ---------------------------------------------------------------- phần cuối
        /// <summary>Hai ô chữ ký: người lập (trái) và người duyệt (phải), gộp ô để chữ không bị cắt.</summary>
        public void ChuKy(string benTraiNguoiLap = "NGƯỜI LẬP BÁO CÁO", string benPhai = "QUẢN LÝ KHO")
        {
            Empty(2);
            int cotPhai = Math.Min(_kyTrai, _soCot - 1);

            AddRow(new()
            {
                [0] = (benTraiNguoiLap, OStyle.CenterBoldPlain),
                [cotPhai] = (benPhai, OStyle.CenterBoldPlain)
            });
            if (_soCot > 1) Merge($"A{_r}:{Col(cotPhai - 1)}{_r}");
            Merge($"{Col(cotPhai)}{_r}:{Col(_soCot - 1)}{_r}");

            AddRow(new()
            {
                [0] = ("(Ký, ghi rõ họ tên)", OStyle.Meta),
                [cotPhai] = ("(Ký, đóng dấu)", OStyle.Meta)
            });
            if (_soCot > 1) Merge($"A{_r}:{Col(cotPhai - 1)}{_r}");
            Merge($"{Col(cotPhai)}{_r}:{Col(_soCot - 1)}{_r}");

            _rows.Add(new ORow(_r++, new Dictionary<int, OCell> { [0] = new("", OStyle.Normal) }) { Height = 70 });
        }

        public byte[] Build() => ExcelWriter.Ghi(new List<OSheet> { ThanhSheet() });

        /// <summary>Chuyển builder hiện tại thành một sheet để ghép vào workbook nhiều sheet.</summary>
        public OSheet ThanhSheet() => new()
        {
            Ten = string.IsNullOrWhiteSpace(TenSheet) ? "BaoCao" : TenSheet,
            Rows = _rows,
            Widths = _widths,
            Merges = _merges
        };
    }

    /// <summary>Ghi workbook OOXML tối giản: nhiều sheet + styles dùng chung.</summary>
    internal static class ExcelWriter
    {
        private const string Ns = "xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"";

        public static byte[] Ghi(IReadOnlyList<OSheet> dsSheet)
        {
            static string ColLetter(int i)
            {
                var s = "";
                i++;
                while (i > 0)
                {
                    var m = (i - 1) % 26;
                    s = (char)('A' + m) + s;
                    i = (i - 1) / 26;
                }
                return s;
            }

            if (dsSheet == null || dsSheet.Count == 0)
                dsSheet = new List<OSheet> { new() };

            var sheetXmls = new List<string>();
            foreach (var sheet in dsSheet)
            {
                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sb.Append($"<worksheet {Ns}>");
                int soCot = Math.Max(1, sheet.Widths.Count);
                sb.Append($"<dimension ref=\"A1:{ColLetter(soCot - 1)}{Math.Max(1, sheet.Rows.Count)}\"/>");
                sb.Append($"<sheetViews><sheetView showGridLines=\"1\"{(sheetXmls.Count == 0 ? " tabSelected=\"1\"" : "")} workbookViewId=\"0\"/></sheetViews>");
                sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
                sb.Append("<cols>");
                for (int i = 0; i < sheet.Widths.Count; i++)
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{sheet.Widths[i].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                sb.Append("</cols><sheetData>");

                foreach (var row in sheet.Rows)
                {
                    sb.Append($"<row r=\"{row.Index + 1}\"");
                    if (row.Height.HasValue) sb.Append($" ht=\"{row.Height}\" customHeight=\"1\"");
                    sb.Append('>');
                    foreach (var kv in row.Cells)
                    {
                        var reference = $"{ColLetter(kv.Key)}{row.Index + 1}";
                        var cell = kv.Value;

                        // Ô số: ghi <v> để Excel đọc được giá trị và tính toán.
                        if (cell.Number.HasValue)
                        {
                            var num = cell.Number.Value.ToString("0.##########", CultureInfo.InvariantCulture);
                            sb.Append($"<c r=\"{reference}\" s=\"{(int)cell.Style}\"><v>{num}</v></c>");
                            continue;
                        }

                        if (string.IsNullOrEmpty(cell.Value))
                        {
                            sb.Append($"<c r=\"{reference}\" s=\"{(int)cell.Style}\"/>");
                            continue;
                        }
                        sb.Append($"<c r=\"{reference}\" s=\"{(int)cell.Style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(cell.Value)}</t></is></c>");
                    }
                    sb.Append("</row>");
                }
                sb.Append("</sheetData>");

                if (sheet.Merges.Count > 0)
                {
                    sb.Append($"<mergeCells count=\"{sheet.Merges.Count}\">");
                    foreach (var m in sheet.Merges) sb.Append($"<mergeCell ref=\"{m}\"/>");
                    sb.Append("</mergeCells>");
                }

                sb.Append("<printOptions horizontalCentered=\"1\"/>");
                sb.Append("<pageMargins left=\"0.25\" right=\"0.25\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/>");
                sb.Append("<pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
                sb.Append("</worksheet>");

                sheetXmls.Add(sb.ToString());
            }

            var wb = new StringBuilder();
            wb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            wb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
            wb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            wb.Append("<sheets>");
            for (int i = 0; i < dsSheet.Count; i++)
            {
                var ten = string.IsNullOrWhiteSpace(dsSheet[i].Ten) ? $"Sheet{i + 1}" : dsSheet[i].Ten;
                if (ten.Length > 31) ten = ten.Substring(0, 31);
                wb.Append($"<sheet name=\"{SecurityElement.Escape(ten)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            wb.Append("</sheets></workbook>");
            var workbookXml = wb.ToString();

            var rel = new StringBuilder();
            rel.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            rel.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < dsSheet.Count; i++)
                rel.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            rel.Append($"<Relationship Id=\"rId{dsSheet.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            rel.Append("</Relationships>");
            var wbRel = rel.ToString();

            var ct = new StringBuilder();
            ct.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            ct.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            ct.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            ct.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            ct.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (int i = 0; i < dsSheet.Count; i++)
                ct.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            ct.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            ct.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
            ct.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            ct.Append("</Types>");
            var contentTypes = ct.ToString();

            const string rootRels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
                "</Relationships>";
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var core = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
                "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                $"<dc:title>{SecurityElement.Escape(dsSheet[0].Ten)}</dc:title><dc:creator>Sổ Kho</dc:creator>" +
                "<cp:lastModifiedBy>Sổ Kho</cp:lastModifiedBy>" +
                $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:created>" +
                $"<dcterms:modified xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:modified></cp:coreProperties>";
            const string app = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\">" +
                "<Application>Microsoft Excel</Application></Properties>";

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                void Them(string path, string noiDung)
                {
                    var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                    using var s = entry.Open();
                    using var w = new StreamWriter(s, new UTF8Encoding(false));
                    w.Write(noiDung);
                }
                Them("[Content_Types].xml", contentTypes);
                Them("_rels/.rels", rootRels);
                Them("docProps/core.xml", core);
                Them("docProps/app.xml", app);
                Them("xl/workbook.xml", workbookXml);
                Them("xl/_rels/workbook.xml.rels", wbRel);
                Them("xl/styles.xml", Styles);
                for (int i = 0; i < sheetXmls.Count; i++)
                    Them($"xl/worksheets/sheet{i + 1}.xml", sheetXmls[i]);
            }
            return ms.ToArray();
        }

        private const string Styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"4\">" +
            "<numFmt numFmtId=\"164\" formatCode=\"#,##0;-#,##0;&quot;-&quot;\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"0.0%\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"#,##0\"/>" +
            "<numFmt numFmtId=\"167\" formatCode=\"0\"/>" +
            "</numFmts>" +
            "<fonts count=\"7\">" +
            "<font><sz val=\"10\"/><name val=\"Times New Roman\"/></font>" +
            "<font><b/><sz val=\"10\"/><name val=\"Times New Roman\"/></font>" +
            "<font><b/><sz val=\"14\"/><name val=\"Times New Roman\"/></font>" +
            "<font><i/><sz val=\"9\"/><name val=\"Times New Roman\"/></font>" +
            "<font><b/><sz val=\"9.5\"/><name val=\"Times New Roman\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Times New Roman\"/></font>" +
            "<font><sz val=\"9\"/><name val=\"Times New Roman\"/></font>" +
            "</fonts>" +
            "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFFFFF\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"3\">" +
            "<border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left style=\"thin\"><color rgb=\"FF555555\"/></left><right style=\"thin\"><color rgb=\"FF555555\"/></right>" +
            "<top style=\"thin\"><color rgb=\"FF555555\"/></top><bottom style=\"thin\"><color rgb=\"FF555555\"/></bottom><diagonal/></border>" +
            "<border><left style=\"thin\"><color rgb=\"FF555555\"/></left><right style=\"thin\"><color rgb=\"FF555555\"/></right>" +
            "<top style=\"medium\"><color rgb=\"FF000000\"/></top><bottom style=\"double\"><color rgb=\"FF000000\"/></bottom><diagonal/></border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"29\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"6\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"3\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"6\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"3\" fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 16 Money
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 17 MoneyBold
            "<xf numFmtId=\"164\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 18 MoneyTotal
            "<xf numFmtId=\"164\" fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 19 Percent
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            // 20 PercentBold
            "<xf numFmtId=\"165\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            // 21 PercentTotal
            "<xf numFmtId=\"165\" fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            // 22 Integer
            "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 23 IntegerBold
            "<xf numFmtId=\"166\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 24 IntegerTotal
            "<xf numFmtId=\"166\" fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 25 MetaLeft — ghi chú in nghiêng canh trái, không khung
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
            // 26 BoldRight — nhãn đậm canh phải, không khung
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            // 27 TextPlain — chữ thường canh trái (giá trị trong khối thông tin, tên người ký)
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
            // 28 CenterBoldPlain — tiêu đề ô chữ ký: đậm, canh giữa, không khung
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }
}
