// Lưới ảnh sản phẩm: thêm nhiều ảnh, xoá, đổi thứ tự (ảnh đầu tiên là ảnh đại diện).
// Dùng chung cho trang Tạo và Sửa sản phẩm.
// - Ô viền nét đứt có dấu "+" để chọn thêm ảnh (chọn nhiều file một lần).
// - Mỗi ảnh có nút xoá và 2 nút mũi tên đổi vị trí; mũi tên trái ẩn ở ảnh đầu,
//   mũi tên phải ẩn ở ảnh cuối.
// - Thứ tự cuối cùng gửi lên server qua input ẩn #anhThuTu, dạng:
//   "moi:0|/images/products/a.jpg|moi:1" — "moi:N" là file thứ N trong ô chọn file,
//   còn lại là đường dẫn ảnh đã có trên server.
(function () {
  var luoi = document.getElementById('anhLuoi');
  if (!luoi) return;

  var nutThem = document.getElementById('anhThem');
  var inputFile = document.getElementById('fileHinh');
  var inputThuTu = document.getElementById('anhThuTu');
  var toiDa = parseInt(luoi.getAttribute('data-toi-da') || '8', 10);

  // Ảnh đã có trên server (chỉ trang Sửa mới có)
  var anhCu = [];
  try { anhCu = JSON.parse(luoi.getAttribute('data-anh-cu') || '[]'); } catch (e) { anhCu = []; }

  // File người dùng vừa chọn: chiSo trong mảng này chính là số trong token "moi:N".
  // Không xoá phần tử để chỉ số không lệch; file bị bỏ chỉ gỡ khỏi danh sách hiển thị.
  var fileMoi = [];

  // Danh sách hiển thị: { cu: true, src } hoặc { moi: true, chiSo, src }
  var ds = anhCu.map(function (src) { return { cu: true, src: src }; });

  function capNhatThuTu() {
    inputThuTu.value = ds.map(function (it) {
      return it.cu ? it.src : 'moi:' + it.chiSo;
    }).join('|');
  }

  function taoNut(act, icon, nhan) {
    var b = document.createElement('button');
    b.type = 'button';
    b.className = 'anh-o__nut';
    b.setAttribute('data-act', act);
    b.setAttribute('aria-label', nhan);
    b.title = nhan;
    var i = document.createElement('i');
    i.className = 'bi ' + icon;
    b.appendChild(i);
    return b;
  }

  function veLai() {
    // Bỏ các ô ảnh đang hiển thị, giữ lại ô "+"
    luoi.querySelectorAll('.anh-o:not(.anh-o--them)').forEach(function (o) { o.remove(); });
    var oThem = luoi.querySelector('.anh-o--them');

    ds.forEach(function (it, i) {
      var o = document.createElement('div');
      o.className = 'anh-o';
      o.setAttribute('data-vi-tri', String(i));

      var img = document.createElement('img');
      img.src = it.src;
      img.alt = 'Ảnh sản phẩm ' + (i + 1);
      o.appendChild(img);

      var so = document.createElement('span');
      so.className = 'anh-o__so' + (i === 0 ? ' is-dai-dien' : '');
      so.textContent = i === 0 ? 'Đại diện' : String(i + 1);
      o.appendChild(so);

      var thaoTac = document.createElement('div');
      thaoTac.className = 'anh-o__thao-tac';
      if (i > 0) thaoTac.appendChild(taoNut('trai', 'bi-chevron-left', 'Chuyển ảnh sang trái'));
      thaoTac.appendChild(taoNut('xoa', 'bi-x-lg', 'Xoá ảnh'));
      if (i < ds.length - 1) thaoTac.appendChild(taoNut('phai', 'bi-chevron-right', 'Chuyển ảnh sang phải'));
      o.appendChild(thaoTac);

      luoi.insertBefore(o, oThem);
    });

    if (oThem) oThem.style.display = ds.length >= toiDa ? 'none' : '';
    capNhatThuTu();
  }

  function themFile(dsFile) {
    var hopLe = [];
    Array.prototype.forEach.call(dsFile, function (f) {
      if (!f) return;
      if (!f.type || f.type.indexOf('image/') !== 0) { alert('File "' + f.name + '" không phải là hình ảnh.'); return; }
      if (f.size > 5 * 1024 * 1024) { alert('Ảnh "' + f.name + '" vượt quá 5MB, vui lòng chọn ảnh nhỏ hơn.'); return; }
      hopLe.push(f);
    });
    if (!hopLe.length) { inputFile.value = ''; return; }

    if (ds.length + hopLe.length > toiDa) {
      alert('Mỗi sản phẩm chỉ được tối đa ' + toiDa + ' ảnh.');
      hopLe = hopLe.slice(0, Math.max(0, toiDa - ds.length));
    }

    hopLe.forEach(function (f) {
      var chiSo = fileMoi.length;
      fileMoi.push(f);
      ds.push({ moi: true, chiSo: chiSo, src: URL.createObjectURL(f) });
    });

    // Gắn toàn bộ file vào ô input đúng thứ tự đã thêm (kể cả file đã bỏ trước đó
    // không còn trong danh sách hiển thị — chúng vẫn được gửi nhưng server bỏ qua vì
    // không xuất hiện trong #anhThuTu).
    try {
      var dt = new DataTransfer();
      fileMoi.forEach(function (f) { dt.items.add(f); });
      inputFile.files = dt.files;
    } catch (e) { /* trình duyệt cũ không hỗ trợ: giữ file người dùng vừa chọn */ }

    veLai();
  }

  if (nutThem && inputFile) {
    nutThem.addEventListener('click', function () { inputFile.click(); });
    inputFile.addEventListener('change', function () {
      var chon = Array.prototype.slice.call(inputFile.files || []);
      // Xoá giá trị ô chọn file để chọn lại đúng file vừa bỏ vẫn kích hoạt sự kiện change.
      // Danh sách file thật do fileMoi + DataTransfer quản lý bên dưới.
      inputFile.value = '';
      themFile(chon);
    });
  }

  luoi.addEventListener('click', function (e) {
    var nut = e.target.closest('.anh-o__nut');
    if (!nut) return;
    var viTri = parseInt(nut.closest('.anh-o').getAttribute('data-vi-tri'), 10);
    var act = nut.getAttribute('data-act');

    if (act === 'xoa') {
      ds.splice(viTri, 1);
      veLai();
      return;
    }
    if (act === 'trai' && viTri > 0) {
      var t = ds[viTri - 1]; ds[viTri - 1] = ds[viTri]; ds[viTri] = t;
      veLai();
    } else if (act === 'phai' && viTri < ds.length - 1) {
      var p = ds[viTri + 1]; ds[viTri + 1] = ds[viTri]; ds[viTri] = p;
      veLai();
    }
  });

  // Trước khi gửi form: gắn lại file theo đúng thứ tự fileMoi để "moi:N" khớp vị trí.
  var form = luoi.closest('form');
  if (form && inputFile) {
    form.addEventListener('submit', function () {
      try {
        var dt = new DataTransfer();
        fileMoi.forEach(function (f) { dt.items.add(f); });
        inputFile.files = dt.files;
      } catch (e) { /* bỏ qua */ }
      capNhatThuTu();
    });
  }

  veLai();
})();

// Lightbox xem ảnh ở trang chi tiết: bấm ảnh để phóng to, có nút trái/phải chuyển ảnh.
// Nút trái ẩn ở ảnh đầu, nút phải ẩn ở ảnh cuối.
(function () {
  var khung = document.getElementById('xemAnh');
  if (!khung) return;

  var dsAnh = [];
  var viTri = 0;

  var img = document.getElementById('xemAnhImg');
  var dem = document.getElementById('xemAnhDem');
  var nutTrai = document.getElementById('xemAnhTrai');
  var nutPhai = document.getElementById('xemAnhPhai');

  function hienThi() {
    img.src = dsAnh[viTri];
    img.alt = 'Ảnh sản phẩm ' + (viTri + 1);
    dem.textContent = (viTri + 1) + ' / ' + dsAnh.length;
    nutTrai.hidden = viTri <= 0;
    nutPhai.hidden = viTri >= dsAnh.length - 1;
  }

  function mo(danhSach, batDau) {
    dsAnh = danhSach || [];
    if (!dsAnh.length) return;
    viTri = Math.max(0, Math.min(batDau || 0, dsAnh.length - 1));
    hienThi();
    khung.hidden = false;
    khung.classList.add('is-mo');
    document.body.classList.add('dang-xem-anh');
    nutPhai.focus();
  }

  function dong() {
    khung.classList.remove('is-mo');
    khung.hidden = true;
    document.body.classList.remove('dang-xem-anh');
  }

  document.querySelectorAll('[data-anh-xem]').forEach(function (o) {
    o.addEventListener('click', function () {
      var list = [];
      try { list = JSON.parse(o.getAttribute('data-ds-anh') || '[]'); } catch (e) { list = []; }
      mo(list, parseInt(o.getAttribute('data-anh-xem'), 10));
    });
    o.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); o.click(); }
    });
  });

  nutTrai.addEventListener('click', function () { if (viTri > 0) { viTri--; hienThi(); } });
  nutPhai.addEventListener('click', function () { if (viTri < dsAnh.length - 1) { viTri++; hienThi(); } });
  khung.querySelectorAll('[data-dong]').forEach(function (b) { b.addEventListener('click', dong); });

  document.addEventListener('keydown', function (e) {
    if (khung.hidden) return;
    if (e.key === 'Escape') { dong(); }
    else if (e.key === 'ArrowLeft' && viTri > 0) { viTri--; hienThi(); }
    else if (e.key === 'ArrowRight' && viTri < dsAnh.length - 1) { viTri++; hienThi(); }
  });
})();
