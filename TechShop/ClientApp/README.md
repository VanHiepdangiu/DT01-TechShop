# TechShop Admin

## Bản chạy offline (không cần Node)
Trang MVC hiện có chỉ cần CSS `/css/shop.css`, phần tử `#admin-app` và meta `name="csrf-token"` chứa token antiforgery do máy chủ tạo. Nạp theo thứ tự:

1. `/lib/react/react.production.min.js`
2. `/lib/react/react-dom.production.min.js`
3. `/js/admin.js`

Không cần CDN lúc chạy; dashboard vẫn cần kết nối đến API cùng origin. Không có dữ liệu demo hay fallback giả. Endpoint GET danh sách và báo cáo cần xác thực quyền quản trị ở máy chủ; UI không thay thế phân quyền.

## Bản nguồn Vite (tùy chọn)
`src/main.js` dùng React imports và cùng nội dung ứng dụng với bản UMD. Node/npm/Vite chưa được cài đặt và chưa chạy build trong môi trường tạo assets. Khi có môi trường Node phù hợp, chạy `npm install`, sau đó `npm run build` để tạo `dist/`.

`vite.config.js` proxy `/api` tới biến môi trường `TECHSHOP_API_ORIGIN` (mặc định `http://localhost:5000`). Đăng nhập qua cùng origin/proxy để có cookie. Thẻ meta CSRF trong `index.html` để trống có chủ ý: tích hợp trang này với backend để cấp token hợp lệ trước thao tác PUT; không hardcode hoặc commit token. Khi triển khai bundle, giữ meta do MVC phát sinh và các request cùng origin.

Bộ lọc ngày chỉ áp dụng báo cáo doanh thu; top sản phẩm và tồn kho là báo cáo toàn hệ thống theo API. Lọc trạng thái đơn hàng là độc lập. Trạng thái Returned hiển thị được nhưng không có thao tác chuyển tới từ dashboard theo hợp đồng hiện tại.

## Kiểm tra không cài thêm phần mềm
Từ thư mục TechShop: `python ClientApp/tests/verify_assets.py`. Kiểm tra hợp đồng CSS/JS, SVG XML, bản nguồn tương đương và hash so với manifest tải HTTPS. Đây là kiểm tra tĩnh, không phải kiểm thử trình duyệt/API thực tế.
