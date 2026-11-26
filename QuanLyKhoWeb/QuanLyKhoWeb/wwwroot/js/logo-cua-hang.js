// Chọn logo cửa hàng: 1 ảnh duy nhất, chọn từ máy như ảnh sản phẩm.
// - Ô viền nét đứt có dấu "+" để chọn ảnh (chỉ 1 file).
// - Ảnh đang dùng hiện ngay trong ô, có nút × để bỏ logo.
// - Chọn ảnh mới thì thay ảnh cũ; ô input file luôn chứa đúng file sẽ gửi lên.
// - Bấm × đặt #logoXoa = "true" để server xóa đường dẫn đã lưu.
(function () {
  var luoi = document.getElementById('logoLuoi');
  if (!luoi) return;

  var nutThem = document.getElementById('logoThem');
  var inputFile = document.getElementById('fileLogo');
  var inputXoa = document.getElementById('logoXoa');
  var form = luoi.closest('form');

  // Ảnh đã lưu trên server (trang Edit luôn có)
  var anhCu = (luoi.getAttribute('data-anh-cu') || '').trim();
  var anhMoi = null;   // File người dùng vừa chọn
  var urlMoi = null;   // Object URL để xem trước

  function datXoa(b) {
    if (inputXoa) inputXoa.value = b ? 'true' : 'false';
  }

  function taoNutXoa() {
    var b = document.createElement('button');
    b.type = 'button';
    b.className = 'anh-o__nut';
    b.setAttribute('data-act', 'xoa');
    b.setAttribute('aria-label', 'Bỏ logo');
    b.title = 'Bỏ logo';
    var i = document.createElement('i');
    i.className = 'bi bi-x-lg';
    b.appendChild(i);
    return b;
  }

  function hienThi() {
    // Bỏ ô ảnh đang hiển thị, giữ lại ô "+"
    luoi.querySelectorAll('.anh-o:not(.anh-o--them)').forEach(function (o) { o.remove(); });
    var oThem = luoi.querySelector('.anh-o--them');

    var src = urlMoi || anhCu;
    if (src) {
      var o = document.createElement('div');
      o.className = 'anh-o';

      var img = document.createElement('img');
      img.src = src;
      img.alt = 'Logo cửa hàng';
      o.appendChild(img);

      var thaoTac = document.createElement('div');
      thaoTac.className = 'anh-o__thao-tac';
      thaoTac.appendChild(taoNutXoa());
      o.appendChild(thaoTac);

      luoi.insertBefore(o, oThem);
    }

    // Đã có ảnh thì ẩn ô "+" (logo chỉ 1 ảnh)
    if (oThem) oThem.style.display = src ? 'none' : '';
  }

  function boChonFile() {
    if (urlMoi) { URL.revokeObjectURL(urlMoi); urlMoi = null; }
    anhMoi = null;
    if (inputFile) inputFile.value = '';
  }

  if (nutThem && inputFile) {
    nutThem.addEventListener('click', function () { inputFile.click(); });

    inputFile.addEventListener('change', function () {
      var f = (inputFile.files || [])[0];
      if (!f) return;

      if (!f.type || f.type.indexOf('image/') !== 0) {
        alert('File "' + f.name + '" không phải là hình ảnh.');
        boChonFile();
        return;
      }
      if (f.size > 5 * 1024 * 1024) {
        alert('Ảnh "' + f.name + '" vượt quá 5MB, vui lòng chọn ảnh nhỏ hơn.');
        boChonFile();
        return;
      }

      if (urlMoi) URL.revokeObjectURL(urlMoi);
      anhMoi = f;
      urlMoi = URL.createObjectURL(f);

      // Có ảnh mới thì không xóa logo cũ nữa (server sẽ thay thế)
      datXoa(false);
      hienThi();
    });
  }

  // Bấm ×: nếu đang xem ảnh vừa chọn thì hủy ảnh đó và quay lại logo đang dùng;
  // nếu đang xem logo đã lưu thì bỏ hẳn logo (server xóa đường dẫn).
  luoi.addEventListener('click', function (e) {
    var nut = e.target.closest('.anh-o__nut');
    if (!nut) return;
    if (nut.getAttribute('data-act') !== 'xoa') return;

    if (urlMoi) {
      boChonFile();
      datXoa(false);
    } else {
      boChonFile();
      anhCu = '';
      datXoa(true);
    }
    hienThi();
  });

  // Trước khi gửi: bảo đảm input file chứa đúng file đã chọn
  if (form && inputFile) {
    form.addEventListener('submit', function () {
      try {
        if (anhMoi) {
          var dt = new DataTransfer();
          dt.items.add(anhMoi);
          inputFile.files = dt.files;
        } else {
          inputFile.value = '';
        }
      } catch (e) { /* trình duyệt cũ: giữ nguyên input */ }
    });
  }

  hienThi();
})();
