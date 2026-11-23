/* TechShop admin: React 18 UMD. Load local react, react-dom, then this file. */
(function (React, ReactDOM) {
  'use strict';
  var host = document.getElementById('admin-app');
  if (!host) return;
  if (!React || !ReactDOM || !ReactDOM.createRoot) {
    host.setAttribute('role', 'alert');
    host.textContent = 'Không tải được giao diện quản trị. Vui lòng kiểm tra các tệp React cục bộ và tải lại trang.';
    return;
  }
/* APP_START */
  var isAdmin = host.dataset.role !== "Staff";
  var h = React.createElement;
  var useState = React.useState;
  var useEffect = React.useEffect;
  var useRef = React.useRef;
  var NEXT_STATUS = {"PendingPayment":["Cancelled"],"New":["Packing","Cancelled"],"Packing":["Shipping"],"Shipping":["Completed"],"Completed":[],"Cancelled":[],"Returned":[]};
  var STATUS_LABELS = { PendingPayment: 'Chờ thanh toán', New: 'Đơn mới', Packing: 'Đang đóng gói', Shipping: 'Đang giao hàng', Completed: 'Hoàn tất', Cancelled: 'Đã hủy', Returned: 'Đã trả hàng' };
  var ACTION_LABELS = { Packing: 'Đóng gói', Shipping: 'Giao hàng', Completed: 'Hoàn tất', Cancelled: 'Hủy đơn' };
  var moneyFormat = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });
  var numberFormat = new Intl.NumberFormat('vi-VN');
  function money(value) { return moneyFormat.format(Number(value) || 0); }
  function localDate(date) {
    return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0');
  }
  function displayDate(value) {
    var date = new Date(value);
    return isNaN(date.getTime()) ? '—' : date.toLocaleDateString('vi-VN');
  }
  function initialPeriod() {
    var today = new Date();
    return { from: localDate(new Date(today.getFullYear(), today.getMonth(), 1)), to: localDate(today) };
  }
  function safeError(error) {
    return error && error.message ? error.message : 'Có lỗi không xác định. Vui lòng thử lại.';
  }
  function request(url, options) {
    return fetch(url, Object.assign({ credentials: 'same-origin', headers: { Accept: 'application/json' } }, options || {})).then(function (response) {
      if (response.redirected) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
      if (!response.ok) {
        var fallback = response.status === 401 ? 'Bạn cần đăng nhập để tiếp tục.' : response.status === 403 ? 'Bạn không có quyền thực hiện thao tác hoặc mã bảo vệ đã hết hạn. Hãy tải lại trang.' : response.status === 409 ? 'Đơn hàng đã được thay đổi bởi người khác. Hãy tải lại dữ liệu trước khi thử lại.' : 'Không thể xử lý yêu cầu (HTTP ' + response.status + '). Vui lòng thử lại.';
        return response.text().then(function (text) {
          var detail;
          try { detail = JSON.parse(text); } catch (_) { /* Never display an HTML error page. */ }
          var message = detail && (detail.message || detail.detail || detail.title);
          if (detail && detail.errors) {
            var validation = Object.keys(detail.errors).map(function (key) { return [].concat(detail.errors[key]).join(' '); }).join(' ');
            if (validation) message = validation;
          }
          throw new Error(typeof message === 'string' ? fallback + ' ' + message.slice(0, 500) : fallback);
        });
      }
      if (response.status === 204) return null;
      return response.text().then(function (text) {
        if (!text) return null;
        try { return JSON.parse(text); } catch (_) { throw new Error('Máy chủ trả về dữ liệu không hợp lệ. Vui lòng tải lại trang.'); }
      });
    }).catch(function (error) {
      if (error.name === 'AbortError') throw error;
      if (error instanceof TypeError) throw new Error('Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối và thử lại.');
      throw error;
    });
  }
  function validateArray(data) {
    if (!Array.isArray(data)) throw new Error('Dữ liệu danh sách từ máy chủ không đúng định dạng.');
    return data;
  }
  function validateRevenue(data) {
    if (!data || !Array.isArray(data.points) || !Number.isFinite(Number(data.total)) || !Number.isFinite(Number(data.orders))) throw new Error('Dữ liệu doanh thu từ máy chủ không đúng định dạng.');
    return data;
  }
  function useResource(url, revision, validator) {
    var state = useState({ data: null, loading: true, error: '' });
    var resource = state[0], setResource = state[1];
    var sequence = useRef(0);
    useEffect(function () {
      if (!url) { setResource({ data: null, loading: false, error: "" }); return; }
      var controller = new AbortController();
      var id = ++sequence.current;
      setResource({ data: null, loading: true, error: '' });
      request(url, { signal: controller.signal }).then(validator).then(function (data) {
        if (sequence.current === id && !controller.signal.aborted) setResource({ data: data, loading: false, error: '' });
      }).catch(function (error) {
        if (error.name !== 'AbortError' && sequence.current === id && !controller.signal.aborted) setResource({ data: null, loading: false, error: safeError(error) });
      });
      return function () { controller.abort(); };
    }, [url, revision]);
    return [resource, setResource];
  }
  function Loading(props) { return h('div', { className: 'admin-loading', role: 'status' }, props.text || 'Đang tải dữ liệu…'); }
  function Failure(props) {
    return h('div', { className: 'error admin-error', role: 'alert' }, h('p', null, props.message), props.retry ? h('button', { type: 'button', className: 'btn btn-outline btn-sm', onClick: props.retry }, 'Thử lại') : null);
  }
  function Empty(props) { return h('div', { className: 'empty' }, props.children || 'Chưa có dữ liệu trong khoảng thời gian này.'); }
  function ResourceBody(props) {
    if (props.resource.loading) return h(Loading, null);
    if (props.resource.error) return h(Failure, { message: props.resource.error, retry: props.retry });
    return props.children;
  }
  function RevenueChart(props) {
    var points = props.points.slice().sort(function (a, b) { return String(a.date).localeCompare(String(b.date)); });
    if (!points.length) return h(Empty, null, 'Chưa phát sinh doanh thu trong khoảng thời gian đã chọn.');
    var width = Math.max(640, points.length * 18 + 85), height = 240;
    var left = 68, bottom = 204, plotWidth = width - left - 15, max = Math.max.apply(null, [1].concat(points.map(function (p) { return Number(p.total) || 0; })));
    var step = plotWidth / points.length;
    var children = [h('title', { key: 'title', id: 'revenue-chart-title' }, 'Biểu đồ doanh thu theo ngày')];
    [0, 0.5, 1].forEach(function (ratio, index) {
      var y = bottom - ratio * 173;
      children.push(h('line', { key: 'grid' + index, className: 'chart-grid', x1: left, x2: width - 8, y1: y, y2: y }));
      var value = max * ratio;
      var label = value >= 1000000000 ? (value / 1000000000).toFixed(1) + ' tỷ' : value >= 1000000 ? (value / 1000000).toFixed(1) + ' tr' : value >= 1000 ? (value / 1000).toFixed(0) + ' k' : String(Math.round(value));
      children.push(h('text', { key: 'axis' + index, className: 'chart-axis', x: left - 8, y: y + 4, textAnchor: 'end' }, label));
    });
    points.forEach(function (p, i) {
      var barHeight = Math.max(0, (Number(p.total) || 0) / max * 173);
      children.push(h('rect', { key: 'bar' + i, className: 'chart-bar', x: left + i * step + step * 0.2, y: bottom - barHeight, width: Math.max(2, step * 0.6), height: barHeight, rx: 3 }, h('title', null, displayDate(p.date) + ': ' + money(p.total))));
      if (i === 0 || i === points.length - 1 || i % Math.max(1, Math.ceil(points.length / 7)) === 0) children.push(h('text', { key: 'date' + i, className: 'chart-axis', x: left + i * step + step / 2, y: 226, textAnchor: 'middle' }, String(p.date).slice(5, 10).split('-').reverse().join('/')));
    });
    return h(React.Fragment, null,
      h('div', { className: 'chart-wrap' }, h('svg', { className: 'revenue-chart', viewBox: '0 0 ' + width + ' ' + height, role: 'img', 'aria-labelledby': 'revenue-chart-title' }, children)),
      h('details', null, h('summary', null, 'Xem số liệu doanh thu chi tiết'), h('div', { className: 'table-wrap' }, h('table', null, h('caption', { className: 'visually-hidden' }, 'Doanh thu từng ngày'), h('thead', null, h('tr', null, h('th', { scope: 'col' }, 'Ngày'), h('th', { scope: 'col' }, 'Doanh thu'))), h('tbody', null, points.map(function (p, i) { return h('tr', { key: i }, h('td', null, displayDate(p.date)), h('td', null, money(p.total))); }))))));
  }
  function TopProducts(props) {
    if (!props.items.length) return h(Empty, null, 'Chưa có sản phẩm bán chạy.');
    var max = Math.max.apply(null, [1].concat(props.items.map(function (p) { return Number(p.quantity) || 0; })));
    return h('div', null, props.items.map(function (p, i) {
      return h('div', { key: i, className: 'top-product' }, h('strong', null, (i + 1) + '. ' + p.name), h('small', null, numberFormat.format(p.quantity) + ' đã bán'), h('small', null, money(p.total)), h('div', { className: 'bar-track', 'aria-hidden': true }, h('div', { className: 'bar-fill', style: { width: Math.max(0, Math.min(100, Number(p.quantity) / max * 100)) + '%' } })));
    }));
  }
  function Dashboard() {
    var dates = useState(initialPeriod), period = dates[0], setPeriod = dates[1];
    var draftDates = useState(initialPeriod), draft = draftDates[0], setDraft = draftDates[1];
    var filterErrorState = useState(''), filterError = filterErrorState[0], setFilterError = filterErrorState[1];
    var filter = useState(''), statusFilter = filter[0], setStatusFilter = filter[1];
    var version = useState(0), revision = version[0], setRevision = version[1];
    var report = useResource(isAdmin ? '/api/reports/revenue?from=' + encodeURIComponent(period.from) + '&to=' + encodeURIComponent(period.to) : null, revision, validateRevenue)[0];
    var top = useResource(isAdmin ? '/api/reports/top-products' : null, revision, validateArray)[0];
    var low = useResource(isAdmin ? '/api/reports/low-stock' : null, revision, validateArray)[0];
    var orderResource = useResource('/api/orders?status=' + encodeURIComponent(statusFilter), revision, validateArray);
    var orders = orderResource[0], setOrders = orderResource[1];
    var pendingState = useState({}), pending = pendingState[0], setPending = pendingState[1];
    var pendingRef = useRef({});
    var mutationState = useState({ error: '', success: '' }), mutation = mutationState[0], setMutation = mutationState[1];
    var filterRef = useRef(statusFilter);
    filterRef.current = statusFilter;
    function refresh() { setRevision(function (v) { return v + 1; }); }
    function applyDates(event) {
      event.preventDefault();
      if (!/^\d{4}-\d{2}-\d{2}$/.test(draft.from) || !/^\d{4}-\d{2}-\d{2}$/.test(draft.to) || draft.from > draft.to) { setFilterError('Vui lòng chọn khoảng ngày hợp lệ: ngày bắt đầu không được sau ngày kết thúc.'); return; }
      setFilterError('');
      setPeriod({ from: draft.from, to: draft.to });
      refresh();
    }
    function changeStatus(order, next) {
      if (pendingRef.current[order.id] || !(NEXT_STATUS[order.status] || []).includes(next)) return;
      if (next === 'Cancelled' && !window.confirm('Bạn có chắc muốn hủy đơn ' + order.code + '?')) return;
      var meta = document.querySelector('meta[name="csrf-token"]');
      var csrf = meta && meta.content;
      if (!csrf) { setMutation({ error: 'Thiếu mã bảo vệ CSRF. Vui lòng tải lại trang trước khi cập nhật đơn hàng.', success: '' }); return; }
      if (typeof order.rowVersion !== 'string' || !order.rowVersion) { setMutation({ error: 'Đơn hàng thiếu phiên bản dữ liệu. Vui lòng tải lại danh sách.', success: '' }); return; }
      var capturedFilter = statusFilter;
      pendingRef.current[order.id] = true;
      setPending(Object.assign({}, pendingRef.current));
      setMutation({ error: '', success: '' });
      setOrders(function (current) { return Object.assign({}, current, { data: (current.data || []).map(function (row) { return row.id === order.id ? Object.assign({}, row, { status: next }) : row; }) }); });
      request('/api/orders/' + encodeURIComponent(order.id) + '/status', {
        method: 'PUT', headers: { 'Content-Type': 'application/json', Accept: 'application/json', 'X-CSRF-TOKEN': csrf },
        body: JSON.stringify({ status: next, note: '', rowVersion: order.rowVersion })
      }).then(function () {
        setMutation({ error: '', success: 'Đã cập nhật đơn ' + order.code + ' sang “' + STATUS_LABELS[next] + '”.' });
        refresh();
      }).catch(function (error) {
        if (filterRef.current === capturedFilter) setOrders(function (current) { return Object.assign({}, current, { data: (current.data || []).map(function (row) { return row.id === order.id ? order : row; }) }); });
        setMutation({ error: safeError(error), success: '' });
        refresh();
      }).finally(function () {
        delete pendingRef.current[order.id];
        setPending(Object.assign({}, pendingRef.current));
      });
    }
    var stats = [
      { label: 'Doanh thu trong kỳ', value: report.data ? money(report.data.total) : '—', note: 'Theo khoảng ngày đã chọn' },
      { label: 'Số đơn trong kỳ', value: report.data ? numberFormat.format(report.data.orders) : '—', note: 'Theo báo cáo doanh thu' },
      { label: 'Giá trị đơn trung bình', value: report.data ? money(report.data.orders > 0 ? report.data.total / report.data.orders : 0) : '—', note: 'Doanh thu / số đơn' },
      { label: 'Sản phẩm sắp hết', value: low.data ? numberFormat.format(low.data.length) : '—', note: 'Cần kiểm tra tồn kho' }
    ];
    return h('div', { className: 'admin-app' },
      h('div', { className: 'page-head' }, h('div', null, h('span', { className: 'eyebrow' }, 'TechShop · Quản trị'), h('h1', null, 'Tổng quan kinh doanh'), h('p', null, 'Theo dõi doanh thu, tồn kho và xử lý đơn hàng tại một nơi.')), h('button', { className: 'btn btn-outline', type: 'button', onClick: refresh }, 'Làm mới dữ liệu')),
      isAdmin && h('section', { className: 'panel', 'aria-label': 'Bộ lọc doanh thu' }, h('form', { className: 'dashboard-filters', onSubmit: applyDates },
        h('div', { className: 'field' }, h('label', { htmlFor: 'report-from' }, 'Từ ngày'), h('input', { id: 'report-from', className: 'form-control', type: 'date', required: true, value: draft.from, onChange: function (e) { setDraft(Object.assign({}, draft, { from: e.target.value })); } })),
        h('div', { className: 'field' }, h('label', { htmlFor: 'report-to' }, 'Đến ngày'), h('input', { id: 'report-to', className: 'form-control', type: 'date', required: true, value: draft.to, onChange: function (e) { setDraft(Object.assign({}, draft, { to: e.target.value })); } })),
        h('button', { type: 'submit', className: 'btn btn-dark' }, 'Xem báo cáo')), filterError ? h(Failure, { message: filterError }) : null),
      isAdmin && h('div', { className: 'stat-grid' }, stats.map(function (stat, i) { return h('div', { className: 'stat', key: i }, h('span', { className: 'stat-label' }, stat.label), h('strong', { className: 'stat-value' }, stat.value), h('small', null, stat.note)); })),
      isAdmin && h('div', { className: 'dashboard-columns' },
        h('section', { className: 'panel', 'aria-label': 'Doanh thu theo ngày' }, h('div', { className: 'admin-section-head' }, h('h2', null, 'Doanh thu theo ngày'), h('span', { className: 'badge' }, displayDate(period.from) + ' – ' + displayDate(period.to))), h(ResourceBody, { resource: report, retry: refresh }, report.data ? h(RevenueChart, { points: report.data.points }) : null)),
        h('section', { className: 'panel', 'aria-label': 'Sản phẩm bán chạy' }, h('h2', null, 'Sản phẩm bán chạy'), h('p', { className: 'muted' }, 'Xếp hạng theo báo cáo toàn hệ thống.'), h(ResourceBody, { resource: top, retry: refresh }, top.data ? h(TopProducts, { items: top.data }) : null))),
      isAdmin && h('section', { className: 'panel', 'aria-label': 'Cảnh báo tồn kho' }, h('h2', null, 'Cần bổ sung hàng'), h(ResourceBody, { resource: low, retry: refresh }, low.data ? low.data.length ? h('ul', { className: 'low-stock-list' }, low.data.map(function (p) { return h('li', { key: p.id }, h('span', null, p.name), h('span', { className: 'low-stock-count' }, 'Còn ' + numberFormat.format(p.stock))); })) : h(Empty, null, 'Tồn kho ổn định. Chưa có sản phẩm sắp hết hàng.') : null)),
      h('section', { className: 'panel', 'aria-label': 'Quản lý đơn hàng' },
        h('div', { className: 'admin-section-head' }, h('h2', null, 'Đơn hàng'), h('div', null, h('label', { htmlFor: 'order-status-filter', className: 'visually-hidden' }, 'Lọc trạng thái đơn hàng'), h('select', { id: 'order-status-filter', className: 'form-control', value: statusFilter, onChange: function (e) { setStatusFilter(e.target.value); } }, h('option', { value: '' }, 'Tất cả trạng thái'), Object.keys(STATUS_LABELS).map(function (key) { return h('option', { key: key, value: key }, STATUS_LABELS[key]); })))),
        mutation.error ? h(Failure, { message: mutation.error, retry: refresh }) : null,
        mutation.success ? h('div', { className: 'notice', role: 'status', 'aria-live': 'polite' }, mutation.success) : null,
        h(ResourceBody, { resource: orders, retry: refresh }, orders.data ? orders.data.length ? h('div', { className: 'table-wrap' }, h('table', null,
          h('caption', { className: 'visually-hidden' }, 'Danh sách đơn hàng và các thao tác hợp lệ'),
          h('thead', null, h('tr', null, ['Mã đơn', 'Khách hàng', 'Ngày đặt', 'Tổng tiền', 'Trạng thái', 'Thao tác'].map(function (label) { return h('th', { key: label, scope: 'col' }, label); }))),
          h('tbody', null, orders.data.map(function (order) {
            return h('tr', { key: order.id, className: pending[order.id] ? 'row-updating' : '', 'aria-busy': !!pending[order.id] },
              h('td', null, h('strong', null, order.code)), h('td', null, order.fullName), h('td', null, displayDate(order.createdAt)), h('td', null, money(order.total)),
              h('td', null, h('span', { className: 'order-status ' + (Object.prototype.hasOwnProperty.call(STATUS_LABELS, order.status) ? order.status : '') }, STATUS_LABELS[order.status] || order.status)),
              h('td', null, pending[order.id] ? h('span', { role: 'status' }, 'Đang cập nhật…') : h('div', { className: 'row-actions' }, (NEXT_STATUS[order.status] || []).length ? NEXT_STATUS[order.status].map(function (next) { return h('button', { type: 'button', key: next, className: 'btn btn-sm ' + (next === 'Cancelled' ? 'btn-danger' : 'btn-primary'), disabled: !!pending[order.id] || orders.loading, onClick: function () { changeStatus(order, next); }, 'aria-label': ACTION_LABELS[next] + ' đơn ' + order.code }, ACTION_LABELS[next]); }) : h('span', { className: 'muted' }, 'Không có thao tác'))));
          })))) : h(Empty, null, 'Không có đơn hàng phù hợp với trạng thái đã chọn.') : null)));
  }
  ReactDOM.createRoot(host).render(h(Dashboard));
/* APP_END */
})(window.React, window.ReactDOM);
