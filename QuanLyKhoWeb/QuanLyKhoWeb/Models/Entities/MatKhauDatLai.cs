using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class MatKhauDatLai
{
    public int MaDatLai { get; set; }

    public int MaNguoiDung { get; set; }

    public string MaXacNhanHash { get; set; } = null!;

    public DateTime NgayTao { get; set; }

    public DateTime NgayHetHan { get; set; }

    public bool DaSuDung { get; set; }

    public virtual NguoiDung MaNguoiDungNavigation { get; set; } = null!;
}
