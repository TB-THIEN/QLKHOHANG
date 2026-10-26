// UX chung toàn web: sidebar mobile, alert tự ẩn, chống submit 2 lần, nút clear search.
(function () {
  // Sidebar mobile
  var toggle = document.getElementById('sidebarToggle');
  var backdrop = document.getElementById('sidebarBackdrop');
  if (toggle) toggle.addEventListener('click', function () { document.body.classList.toggle('sidebar-open'); });
  if (backdrop) backdrop.addEventListener('click', function () { document.body.classList.remove('sidebar-open'); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') document.body.classList.remove('sidebar-open');
  });

  // Ô tìm kiếm tổng: gợi ý toàn hệ thống, chọn gợi ý để mở đúng trang chứa nội dung.
  var input = document.getElementById('globalSearch');
  var clear = document.getElementById('searchClear');
  var wrap = document.getElementById('globalSearchBox');
  var box = document.getElementById('globalSearchResults');
  var suggestUrl = wrap ? wrap.dataset.url : '';
  var timer = null, controller = null, items = [], activeIdx = -1;

  function syncSearch() { if (wrap) wrap.classList.toggle('has-value', !!(input && input.value.trim())); }
  function dongGoiY() {
    if (!box) return;
    box.hidden = true; box.innerHTML = ''; items = []; activeIdx = -1;
    if (input) input.setAttribute('aria-expanded', 'false');
  }
  function moGoiY() {
    if (!box) return;
    box.hidden = false;
    if (input) input.setAttribute('aria-expanded', 'true');
  }
  function veThongBao(text, icon) {
    box.innerHTML = '';
    var d = document.createElement('div');
    d.className = 'global-search-empty';
    d.innerHTML = '<i class="bi ' + (icon || 'bi-search') + '"></i><span></span>';
    d.querySelector('span').textContent = text;
    box.appendChild(d);
    items = []; activeIdx = -1;
    moGoiY();
  }
  function veGoiY(list) {
    box.innerHTML = ''; items = []; activeIdx = -1;
    var nhomTruoc = null;
    list.forEach(function (item) {
      if (item.nhom !== nhomTruoc) {
        var h = document.createElement('div');
        h.className = 'global-search-group';
        h.textContent = item.nhom || 'Kết quả';
        box.appendChild(h);
        nhomTruoc = item.nhom;
      }
      var b = document.createElement('button');
      b.type = 'button'; b.className = 'global-search-item'; b.setAttribute('role', 'option');
      var icon = document.createElement('span');
      icon.className = 'gs-icon';
      var i = document.createElement('i');
      i.className = 'bi ' + (item.icon || 'bi-search');
      icon.appendChild(i);
      var txt = document.createElement('span');
      txt.className = 'gs-text';
      var t = document.createElement('span');
      t.className = 'gs-title'; t.textContent = item.tieuDe || '';
      txt.appendChild(t);
      if (item.moTa) {
        var s = document.createElement('span');
        s.className = 'gs-sub'; s.textContent = item.moTa;
        txt.appendChild(s);
      }
      b.appendChild(icon); b.appendChild(txt);
      b.addEventListener('mousedown', function (e) { e.preventDefault(); });
      b.addEventListener('click', function () { if (item.url) window.location.href = item.url; });
      box.appendChild(b); items.push(b);
    });
    moGoiY();
  }
  async function taiGoiY(term) {
    if (!suggestUrl || !term) { dongGoiY(); return; }
    if (controller) controller.abort();
    controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
    box.innerHTML = '<div class="global-search-loading"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>Đang tìm...</span></div>';
    items = []; activeIdx = -1; moGoiY();
    try {
      var res = await fetch(suggestUrl + '?q=' + encodeURIComponent(term), { signal: controller ? controller.signal : undefined });
      if (!res.ok) throw new Error('http');
      var list = await res.json();
      if (!input || input.value.trim() !== term) return;
      if (!list.length) veThongBao('Không tìm thấy kết quả cho “' + term + '”.', 'bi-search');
      else veGoiY(list);
    } catch (e) {
      if (e && e.name === 'AbortError') return;
      if (input && input.value.trim() === term) veThongBao('Không tìm kiếm được, vui lòng thử lại.', 'bi-exclamation-triangle');
    }
  }
  function toMauGoiY() {
    items.forEach(function (b, i) { b.classList.toggle('active', i === activeIdx); });
    if (activeIdx >= 0) items[activeIdx].scrollIntoView({ block: 'nearest' });
  }
  if (input) {
    input.addEventListener('input', function () {
      syncSearch();
      var term = input.value.trim();
      clearTimeout(timer);
      if (!term) {
        if (controller) controller.abort();
        dongGoiY();
        return;
      }
      timer = setTimeout(function () { taiGoiY(term); }, 220);
    });
    input.addEventListener('focus', function () {
      if (input.value.trim()) taiGoiY(input.value.trim());
    });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        if (box.hidden && input.value.trim()) { e.preventDefault(); taiGoiY(input.value.trim()); return; }
        if (!items.length) return;
        e.preventDefault();
        activeIdx = e.key === 'ArrowDown'
          ? (activeIdx + 1) % items.length
          : (activeIdx - 1 + items.length) % items.length;
        toMauGoiY();
      } else if (e.key === 'Enter') {
        if (items.length) {
          e.preventDefault();
          (activeIdx >= 0 ? items[activeIdx] : items[0]).click();
        } else if (!box.hidden) {
          e.preventDefault();
        }
      } else if (e.key === 'Escape') {
        dongGoiY();
      }
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === '/' && document.activeElement !== input && !/INPUT|TEXTAREA|SELECT/.test(document.activeElement.tagName)) {
        e.preventDefault(); input.focus();
      }
    });
    syncSearch();
  }
  if (clear && input) clear.addEventListener('click', function () {
    input.value = ''; syncSearch(); dongGoiY(); input.focus();
  });
  document.addEventListener('click', function (e) {
    if (wrap && !wrap.contains(e.target)) dongGoiY();
  });

  // Alert tự ẩn sau 6s (trừ lỗi validation)
  document.querySelectorAll('.alert-dismissible').forEach(function (el) {
    setTimeout(function () {
      try {
        var a = bootstrap.Alert.getOrCreateInstance(el);
        a.close();
      } catch (e) { el.style.display = 'none'; }
    }, 6000);
  });

  // Chống submit 2 lần + spinner cho nút chính
  document.querySelectorAll('form').forEach(function (f) {
    f.addEventListener('submit', function () {
      var btn = f.querySelector('button[type="submit"].btn-primary');
      if (btn && !btn.disabled) {
        btn.disabled = true;
        var old = btn.innerHTML;
        btn.dataset.oldHtml = old;
        btn.innerHTML = '<span class="spinner-border" aria-hidden="true"></span> Đang xử lý...';
        setTimeout(function () {
          btn.disabled = false;
          if (btn.dataset.oldHtml) btn.innerHTML = btn.dataset.oldHtml;
        }, 8000);
      }
    });
  });
})();

// Ô tiền (.money): gõ 150000 -> hiện 150.000; submit tự gỡ chấm để server nhận số nguyên
function formatMoneyInput(el) {
  var d = (el.value || '').replace(/\D/g, '').slice(0, 15);
  el.value = d.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
}
document.addEventListener('input', function (e) {
  var t = e.target;
  if (t && t.classList && t.classList.contains('money')) formatMoneyInput(t);
});
document.addEventListener('submit', function (e) {
  var f = e.target;
  if (f && f.querySelectorAll) f.querySelectorAll('input.money').forEach(function (i) {
    i.value = i.value.replace(/\./g, '');
  });
}, true);
document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('input.money').forEach(formatMoneyInput);
});

// Hộp gợi ý dùng chung (1 list duy nhất do web tự vẽ, đã bỏ datalist mặc định):
// hỗ trợ chuột, phím lên/xuống + Enter, Esc, và dòng tạo mới (tùy chọn).
// cfg: { url(term), icon, allowCreate, createText(term), excludeExact[], showOnFocus }
window.setupAutocomplete = function (txtId, boxId, cfg) {
  cfg = cfg || {};
  var txt = document.getElementById(txtId), box = document.getElementById(boxId);
  if (!txt || !box || typeof cfg.url !== 'function') return;
  var icon = cfg.icon || 'bi-tag';
  var allowCreate = cfg.allowCreate !== false;
  var showOnFocus = cfg.showOnFocus === true; // true = click/focus vào ô trống vẫn xổ list có sẵn
  var hidden = (cfg.excludeExact || []).map(function (x) { return x.trim().toLowerCase(); });
  var items = [], activeIdx = -1;

  function close() { box.style.display = 'none'; box.innerHTML = ''; items = []; activeIdx = -1; }
  function paintActive() {
    items.forEach(function (b, i) { b.classList.toggle('active', i === activeIdx); });
  }
  // Vẽ list gợi ý từ term (dùng chung cho input + focus). isFocus = true khi xổ list lúc chưa gõ gì.
  async function render(term, isFocus) {
    term = (term || '').trim();
    // Ô trống + không bật showOnFocus -> đóng (giữ hành vi cũ cho các ô Sản phẩm)
    if (!term && !isFocus) { close(); return; }
    let list = [];
    try {
      const res = await fetch(cfg.url(term));
      list = await res.json();
    } catch (e) { close(); return; }
    // Nếu người dùng đã gõ tiếp trong lúc chờ fetch thì bỏ kết quả cũ
    if ((txt.value || '').trim() !== term && !isFocus) return;
    box.innerHTML = ''; items = []; activeIdx = -1;
    list.forEach(x => {
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'role-suggest-item';
      b.innerHTML = '<i class="bi ' + icon + '"></i><span></span>';
      b.querySelector('span').textContent = x;
      b.addEventListener('mousedown', (ev) => { ev.preventDefault(); txt.value = x; close(); });
      b.addEventListener('click', () => { txt.value = x; close(); txt.focus(); });
      box.appendChild(b); items.push(b);
    });
    // Dòng tạo mới khi tên gõ chưa có sẵn (trừ các giá trị bị ẩn). Khi focus ô trống thì không hiện dòng này.
    var biAn = hidden.indexOf(term.toLowerCase()) >= 0;
    if (term && allowCreate && !biAn && !list.some(x => x.toLowerCase() === term.toLowerCase())) {
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'role-suggest-item suggest-new';
      b.innerHTML = '<i class="bi bi-plus-circle"></i><span></span>';
      b.querySelector('span').textContent = typeof cfg.createText === 'function'
        ? cfg.createText(term) : 'Tạo mới "' + term + '"';
      b.addEventListener('mousedown', (ev) => { ev.preventDefault(); close(); });
      b.addEventListener('click', () => { close(); txt.focus(); });
      box.appendChild(b); items.push(b);
    }
    box.style.display = items.length ? 'block' : 'none';
  }
  txt.addEventListener('input', () => { render(txt.value, false); });
  // Click/focus vào ô: xổ toàn bộ danh sách có sẵn để chọn (yêu cầu combobox Vai trò)
  if (showOnFocus) {
    txt.addEventListener('focus', () => { render(txt.value, true); });
    txt.addEventListener('click', () => {
      if (box.style.display !== 'block') render(txt.value, true);
    });
  }
  txt.addEventListener('keydown', (e) => {
    if (box.style.display !== 'block' || !items.length) return;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      activeIdx = e.key === 'ArrowDown'
        ? (activeIdx + 1) % items.length
        : (activeIdx - 1 + items.length) % items.length;
      paintActive();
      items[activeIdx].scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Enter' && activeIdx >= 0) {
      e.preventDefault(); items[activeIdx].click();
    } else if (e.key === 'Escape') { close(); }
  });
  document.addEventListener('click', (e) => {
    if (!box.contains(e.target) && e.target !== txt) close();
  });
};

// Gợi ý vai trò (trang Thêm mới + Chi tiết nhân viên): combobox nhập + xổ list khi focus
// showOnFocus = true để click vào ô trống vẫn hiện tối đa 6 dòng + cuộn (đúng yêu cầu đồ án)
window.setupVaiTroAutocomplete = function (txtId, boxId, excludeExact) {
  window.setupAutocomplete(txtId, boxId, {
    url: term => '/NhanVien/GoiYVaiTro?term=' + encodeURIComponent(term),
    icon: 'bi-person-badge',
    allowCreate: true,
    showOnFocus: true,
    createText: term => 'Tạo vai trò mới "' + term + '"',
    excludeExact: excludeExact
  });
};
