/* ==========================================================================
   Trợ lý AI - logic khung chat
   Nguyên tắc an toàn:
   - Mọi nội dung do máy chủ/AI trả về đều gán bằng textContent, không dùng innerHTML,
     nên không có đường chèn HTML/script từ dữ liệu nhân viên hay từ model.
   - Không giữ gì trong localStorage: đóng trang là hết hội thoại.
   ========================================================================== */
(function () {
  'use strict';

  var goc = document.getElementById('troLyAi');
  if (!goc) return;

  var nutMo = document.getElementById('troLyMo');
  var panel = document.getElementById('troLyPanel');
  var nutDong = document.getElementById('troLyDong');
  var than = document.getElementById('troLyThan');
  var form = document.getElementById('troLyForm');
  var oNhap = document.getElementById('troLyNhap');
  var nutGui = document.getElementById('troLyGui');
  var nutDung = document.getElementById('troLyDung');
  var ghiChuChan = document.getElementById('troLyGhiChu');
  var GHI_CHU_MAC_DINH = ghiChuChan ? ghiChuChan.textContent : '';

  // Chưa cấu hình khóa API: vẫn mở được khung nhưng báo rõ thay vì gọi mạng.
  var daCauHinh = goc.getAttribute('data-da-cau-hinh') === 'true';

  // Lịch sử gửi kèm mỗi lượt (chỉ văn bản, tối đa 10 lượt gần nhất).
  var lichSu = [];
  var dangBan = false;

  // Bộ huỷ của lượt đang chạy. Bấm "Tạm dừng" sẽ abort() để trình duyệt ngắt kết nối;
  // ASP.NET Core chuyển việc đó thành RequestAborted nên server dừng luôn lượt gọi Gemini.
  var boHuy = null;

  function layToken() {
    var input = form.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function cuonXuongCuoi() {
    than.scrollTop = than.scrollHeight;
  }

  function themBong(vaiTro, noiDung, lopThem) {
    var div = document.createElement('div');
    div.className = 'tro-ly-msg ' + (vaiTro === 'nguoi' ? 'tro-ly-msg--nguoi' : 'tro-ly-msg--tro-ly');
    if (lopThem) div.classList.add(lopThem);
    div.textContent = noiDung;
    than.appendChild(div);
    cuonXuongCuoi();
    return div;
  }

  function themLoi(noiDung) {
    themBong('tro-ly', noiDung, 'tro-ly-msg--loi');
  }

  function hienDangGo() {
    var div = document.createElement('div');
    div.className = 'tro-ly-typing';
    div.id = 'troLyDangGo';
    div.setAttribute('aria-label', 'Trợ lý đang trả lời');
    for (var i = 0; i < 3; i++) div.appendChild(document.createElement('span'));
    than.appendChild(div);
    cuonXuongCuoi();
  }

  function xoaDangGo() {
    var el = document.getElementById('troLyDangGo');
    if (el) el.remove();
  }

  // Khi trợ lý đang chạy: ô nhập bị khoá, nút Gửi đổi thành nút Tạm dừng.
  // Người dùng vẫn thấy được nút dừng nên không bị "treo" khi AI trả lời chậm.
  function khoaNhap(khoa) {
    dangBan = khoa;
    oNhap.disabled = khoa;

    nutGui.hidden = khoa;
    nutGui.disabled = khoa;
    nutDung.hidden = !khoa;
    nutDung.disabled = !khoa;

    if (ghiChuChan) {
      ghiChuChan.textContent = khoa
        ? 'Trợ lý đang xử lý… bấm nút tạm dừng nếu bạn không cần nữa.'
        : GHI_CHU_MAC_DINH;
    }

    // Không chuyển focus sang nút Tạm dừng: người dùng vừa bấm Enter để gửi,
    // nhấn thêm lần nữa sẽ vô tình dừng mất. Chỉ giành lại focus khi mở khoá.
    if (!khoa) oNhap.focus();
  }

  function goiApi(duongDan, duLieu, tinHieu) {
    return fetch(duongDan, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': layToken()
      },
      // Gửi token trong header để action [FromBody] vẫn qua được ValidateAntiForgeryToken
      body: JSON.stringify(duLieu),
      signal: tinHieu
    }).then(function (res) {
      if (res.status === 429) {
        return { thanhCong: false, loi: 'Bạn thao tác quá nhanh. Vui lòng chờ một lát rồi thử lại.' };
      }
      if (res.status === 401 || res.status === 403) {
        return { thanhCong: false, loi: 'Phiên đăng nhập đã hết hạn hoặc bạn không có quyền dùng trợ lý.' };
      }
      if (!res.ok) {
        return { thanhCong: false, loi: 'Máy chủ trả về lỗi (' + res.status + '). Vui lòng thử lại.' };
      }
      // Bị chuyển hướng tới trang từ chối truy cập: trả về HTML chứ không phải JSON.
      var kieu = res.headers.get('content-type') || '';
      if (kieu.indexOf('application/json') === -1) {
        return { thanhCong: false, loi: 'Phiên đăng nhập đã hết hạn hoặc bạn không có quyền dùng trợ lý.' };
      }
      return res.json();
    }).catch(function (loi) {
      // Người dùng bấm Tạm dừng: không phải sự cố, trả cờ riêng để nơi gọi xử lý.
      if (loi && loi.name === 'AbortError') {
        return { thanhCong: false, daHuy: true };
      }
      return { thanhCong: false, loi: 'Không kết nối được tới máy chủ. Kiểm tra mạng rồi thử lại.' };
    });
  }

  // Thẻ tóm tắt bản nháp + hai nút Xác nhận / Huỷ.
  function veTheHanhDong(hanhDong) {
    var the = document.createElement('div');
    the.className = 'tro-ly-the';

    var tieuDe = document.createElement('div');
    tieuDe.className = 'tro-ly-the__title';
    var icon = document.createElement('i');
    icon.className = 'bi bi-clipboard-check';
    tieuDe.appendChild(icon);
    var tieuDeChu = document.createElement('span');
    tieuDeChu.textContent = hanhDong.tieuDe || 'Bản nháp chờ xác nhận';
    tieuDe.appendChild(tieuDeChu);
    the.appendChild(tieuDe);

    var dl = document.createElement('dl');
    (hanhDong.tomTat || []).forEach(function (dong) {
      var dt = document.createElement('dt');
      dt.textContent = dong.nhan;
      var dd = document.createElement('dd');
      dd.textContent = dong.giaTri;
      dl.appendChild(dt);
      dl.appendChild(dd);
    });
    the.appendChild(dl);

    var nut = document.createElement('div');
    nut.className = 'tro-ly-the__actions';

    var nutXacNhan = document.createElement('button');
    nutXacNhan.type = 'button';
    nutXacNhan.className = 'btn btn-primary';
    nutXacNhan.textContent = 'Xác nhận';
    var nutHuy = document.createElement('button');
    nutHuy.type = 'button';
    nutHuy.className = 'btn btn-outline-secondary';
    nutHuy.textContent = 'Huỷ';

    nut.appendChild(nutXacNhan);
    nut.appendChild(nutHuy);
    the.appendChild(nut);

    var ghiChu = document.createElement('div');
    ghiChu.className = 'tro-ly-the__hint';
    ghiChu.textContent = 'Chưa có gì được ghi vào hệ thống. Bản nháp hết hạn lúc '
      + (hanhDong.hetHanLuc || '--:--') + '.';
    the.appendChild(ghiChu);

    than.appendChild(the);
    cuonXuongCuoi();
    nutXacNhan.focus();

    function khoaThe() {
      nutXacNhan.disabled = true;
      nutHuy.disabled = true;
    }

    nutHuy.addEventListener('click', function () {
      khoaThe();
      ghiChu.textContent = 'Đang huỷ…';
      goiApi('/TroLy/Huy', { id: hanhDong.id }).then(function (kq) {
        ghiChu.textContent = 'Đã huỷ bản nháp.';
        if (kq && kq.traLoi) themBong('tro-ly', kq.traLoi);
      });
    });

    nutXacNhan.addEventListener('click', function () {
      khoaThe();
      ghiChu.textContent = 'Đang tạo nhân viên…';
      goiApi('/TroLy/XacNhan', { id: hanhDong.id }).then(function (kq) {
        if (!kq || !kq.thanhCong) {
          ghiChu.textContent = 'Không hoàn tất được.';
          themLoi((kq && kq.loi) || 'Không tạo được nhân viên.');
          return;
        }

        ghiChu.textContent = 'Đã hoàn tất.';
        themBong('tro-ly', kq.traLoi || 'Đã tạo nhân viên.');

        // Mật khẩu do quản lý đặt chỉ có trong đúng phản hồi này nên phải hiện ngay.
        if (kq.matKhau) {
          var hop = document.createElement('div');
          hop.className = 'tro-ly-mk';
          var nhan = document.createElement('span');
          nhan.textContent = 'Mật khẩu bạn đã đặt cho ' + (kq.tenDangNhapMoi || '') + ':';
          var ma = document.createElement('code');
          ma.textContent = kq.matKhau;
          var nutChep = document.createElement('button');
          nutChep.type = 'button';
          nutChep.className = 'btn btn-sm btn-outline-secondary';
          nutChep.textContent = 'Sao chép';
          nutChep.addEventListener('click', function () {
            if (navigator.clipboard && navigator.clipboard.writeText) {
              navigator.clipboard.writeText(kq.matKhau).then(function () {
                nutChep.textContent = 'Đã chép';
              }, function () {
                nutChep.textContent = 'Không chép được';
              });
            } else {
              nutChep.textContent = 'Trình duyệt không hỗ trợ';
            }
          });
          hop.appendChild(nhan);
          hop.appendChild(ma);
          hop.appendChild(nutChep);
          than.appendChild(hop);
          cuonXuongCuoi();
        }
      });
    });
  }

  function guiTinNhan() {
    if (dangBan) return;
    var noiDung = oNhap.value.trim();
    if (!noiDung) return;
    if (!daCauHinh) {
      themLoi('Trợ lý chưa được cấu hình khóa API nên chưa dùng được.');
      return;
    }

    themBong('nguoi', noiDung);
    oNhap.value = '';
    oNhap.style.height = 'auto';
    khoaNhap(true);
    hienDangGo();

    var lichSuGui = lichSu.slice(-10);
    boHuy = typeof AbortController !== 'undefined' ? new AbortController() : null;

    goiApi('/TroLy/Hoi', { tinNhan: noiDung, lichSu: lichSuGui },
      boHuy ? boHuy.signal : undefined).then(function (kq) {
      xoaDangGo();
      boHuy = null;
      khoaNhap(false);

      if (kq && kq.daHuy) {
        themBong('tro-ly', 'Đã tạm dừng theo yêu cầu của bạn. Bạn có thể sửa lại yêu cầu rồi gửi tiếp.');
        // Trả lại nội dung vừa gõ để người dùng chỉnh sửa thay vì phải nhập lại từ đầu.
        if (!oNhap.value) {
          oNhap.value = noiDung;
          oNhap.style.height = 'auto';
          oNhap.style.height = Math.min(oNhap.scrollHeight, 110) + 'px';
        }
        return;
      }

      if (!kq || !kq.thanhCong) {
        themLoi((kq && kq.loi) || 'Trợ lý không phản hồi. Vui lòng thử lại.');
        return;
      }

      if (kq.traLoi) themBong('tro-ly', kq.traLoi);
      if (kq.hanhDong) veTheHanhDong(kq.hanhDong);

      // Chỉ đưa vào lịch sử khi thành công, tránh làm nhiễu ngữ cảnh sau này.
      lichSu.push({ vaiTro: 'nguoi_dung', noiDung: noiDung });
      if (kq.traLoi) lichSu.push({ vaiTro: 'tro_ly', noiDung: kq.traLoi });
      if (lichSu.length > 20) lichSu = lichSu.slice(-20);
    });
  }

  function moPanel() {
    panel.hidden = false;
    nutMo.hidden = true;
    oNhap.focus();
    if (!than.dataset.khoiTao) {
      than.dataset.khoiTao = '1';
      if (!daCauHinh) {
        themLoi('Trợ lý chưa được cấu hình khóa API. Quản trị viên cần chạy lệnh '
          + 'dotnet user-secrets set "Ai:ApiKey" "<khóa>" rồi khởi động lại ứng dụng.');
      } else {
        themBong('tro-ly', 'Chào bạn, mình là trợ lý của Sổ Kho. '
          + 'Bạn có thể nhờ mình thêm nhân viên mới hoặc tra cứu nhân viên.');
        veGoiY();
      }
    }
  }

  function dongPanel() {
    panel.hidden = true;
    nutMo.hidden = false;
    nutMo.focus();
  }

  function veGoiY() {
    var hop = document.createElement('div');
    hop.className = 'tro-ly-goiy';
    var goiY = [
      'Thêm nhân viên Nguyễn Văn A, đăng nhập nva, mật khẩu Abc@1234, vai trò Nhân viên kho',
      'Tìm nhân viên tên Hùng',
      'Có những vai trò nào?'
    ];
    goiY.forEach(function (chu) {
      var nut = document.createElement('button');
      nut.type = 'button';
      nut.textContent = chu;
      nut.addEventListener('click', function () {
        oNhap.value = chu;
        hop.remove();
        guiTinNhan();
      });
      hop.appendChild(nut);
    });
    than.appendChild(hop);
    cuonXuongCuoi();
  }

  // --- Gắn sự kiện ---
  nutMo.addEventListener('click', moPanel);
  nutDong.addEventListener('click', dongPanel);

  panel.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') dongPanel();
  });

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    guiTinNhan();
  });

  // Bấm "Tạm dừng": ngắt request đang chạy. Việc dọn dẹp (xoá "đang gõ", mở khoá ô nhập)
  // do callback trong guiTinNhan lo, nên không làm hai lần ở đây.
  nutDung.addEventListener('click', function () {
    if (!dangBan) return;
    nutDung.disabled = true;
    if (boHuy) {
      boHuy.abort();
    } else {
      // Trình duyệt quá cũ (không có AbortController): chỉ mở khoá giao diện.
      xoaDangGo();
      boHuy = null;
      khoaNhap(false);
      themBong('tro-ly', 'Đã tạm dừng theo yêu cầu của bạn. Bạn có thể sửa lại yêu cầu rồi gửi tiếp.');
    }
  });

  // Enter gửi, Shift+Enter xuống dòng (giống các ứng dụng chat quen thuộc).
  oNhap.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      guiTinNhan();
    }
  });

  // Ô nhập tự giãn theo nội dung, tối đa 110px (CSS quy định).
  oNhap.addEventListener('input', function () {
    oNhap.style.height = 'auto';
    oNhap.style.height = Math.min(oNhap.scrollHeight, 110) + 'px';
  });
})();
