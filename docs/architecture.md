# Kiến trúc và quy ước

## Phụ thuộc

API -> Infrastructure -> Application -> Domain. Domain không phụ thuộc EF hay HTTP. Application chứa request DTO có DataAnnotations. Infrastructure chịu trách nhiệm mapping database và migration. API điều phối các thao tác mẫu qua DbContext được inject theo scope request.

Đây là kiến trúc phân lớp khởi đầu, chưa phải triển khai đầy đủ Clean Architecture. Bước mở rộng: định nghĩa use case/interface trong Application, chuyển điều phối bán hàng khỏi controller, thêm response DTO và integration test cho từng nghiệp vụ.

## Quy tắc chính

- Mọi thay đổi Stock đi qua Product.AdjustStock và ghi StockMovement.
- Không cho tồn kho âm, không chấp nhận điều chỉnh bằng 0 hoặc lý do trống.
- Tạo đơn bán dùng giá hiện tại từ database, không nhận giá từ client.
- Không chấp nhận dòng sản phẩm trùng trong cùng request.
- Mỗi lần SaveChanges là một transaction, kết hợp xmin để tránh bán vượt kho khi đồng thời.
- API chưa có idempotency key: client không tự retry POST bán hàng khi timeout vì có thể tạo đơn trùng. Nên bổ sung idempotency trước khi triển khai thực tế.
- Trường thời gian dùng UTC, frontend chuyển sang múi giờ hiển thị.
- Số tiền dùng decimal/numeric(18,2). Cần thống nhất quy tắc làm tròn trước khi triển khai VAT/chiết khấu.
- OpenAPI chỉ bật ở Development. Readiness hiện kiểm tra kết nối database, chưa kiểm tra phiên bản migration.

## Git

Nhánh mặc định `main`; mỗi tính năng dùng nhánh `feature/ten-tinh-nang`. Commit nhỏ, rõ mục đích; tạo pull request và để CI chạy trước khi merge. Không commit .env, user-secrets, bin/obj hay credential. Chưa bật branch protection tự động.

## Lộ trình

1. Xác thực và phân quyền Owner/Manager/Cashier; kiểm thử quyền truy cập.
2. CRUD hoàn chỉnh, lọc/tìm kiếm và phân trang tất cả danh sách.
3. Đơn nhập hàng, khách hàng, hủy/trả hàng và đối soát tồn kho.
4. Application services, response DTO, logging/audit và idempotency.
5. Kiểm thử migration/rollback, cạnh tranh tồn kho nhiều request, deployment HTTPS và backup.
