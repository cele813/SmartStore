# SmartStore

Bộ khung backend quản lý cửa hàng bằng C# / ASP.NET Core Web API, Entity Framework Core và PostgreSQL.

## Công nghệ

- .NET SDK 10.0.401, target .NET 10.
- EF Core 10.0.12, Npgsql EF provider 10.0.3.
- PostgreSQL 17 qua Docker Compose; GitHub Actions cho build/test.
- Controllers, DTO validation, Problem Details, OpenAPI JSON và phân trang sản phẩm.

## Cấu trúc

```text
src/
  SmartStore.Domain/          Entities và quy tắc tồn kho
  SmartStore.Application/     Request DTO và validation
  SmartStore.Infrastructure/  DbContext, mapping, migration
  SmartStore.Api/             HTTP controllers, cấu hình, xử lý lỗi
 tests/SmartStore.Tests/      Unit test và PostgreSQL integration test
 database/                   SQL khởi tạo và tài liệu quan hệ
 docs/                       Kiến trúc và hướng phát triển
```

Đây là bộ khung có luồng chạy mẫu. Application hiện chứa hợp đồng request; controllers truy cập DbContext qua DI. Khi nghiệp vụ mở rộng, chuyển việc điều phối sang application services. Không thêm generic repository vì EF Core đã cung cấp cơ chế tracking và unit of work.

## Chạy local (PowerShell)

Yêu cầu .NET 10 SDK và Docker Desktop đang chạy, hoặc PostgreSQL 17 có sẵn.

```powershell
Copy-Item .env.example .env
# Sửa POSTGRES_PASSWORD trong .env thành mật khẩu local của bạn.
docker compose up -d db
dotnet tool restore
dotnet restore

# Dùng cùng mật khẩu đã đặt trong .env; không commit connection string thật.
dotnet user-secrets set "ConnectionStrings:Store" "Host=localhost;Port=5432;Database=smartstore;Username=smartstore;Password=YOUR_LOCAL_PASSWORD" --project src/SmartStore.Api

dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
dotnet run --project src/SmartStore.Api --launch-profile http
```

API: http://localhost:5080. OpenAPI JSON: http://localhost:5080/openapi/v1.json (Development). Chưa tích hợp Swagger UI.

Nếu dùng PostgreSQL có sẵn, tạo database `smartstore` và user tương ứng, bỏ qua bước Docker. Không tự động chạy migration khi API khởi động.

Có thể thay user-secrets bằng biến môi trường `ConnectionStrings__Store`. File `.env` chỉ dùng cho Docker Compose, ASP.NET Core không tự đọc file này.

## API mẫu

| Method | Đường dẫn | Chức năng |
|---|---|---|
| GET, POST | /api/categories | Danh sách tối đa 200 / tạo danh mục |
| GET | /api/categories/{id} | Chi tiết danh mục |
| GET, POST | /api/suppliers | Danh sách tối đa 200 / tạo nhà cung cấp |
| GET | /api/suppliers/{id} | Chi tiết nhà cung cấp |
| GET, POST | /api/products | Phân trang / tạo sản phẩm |
| GET | /api/products/{id} | Chi tiết sản phẩm |
| POST | /api/products/{id}/stock-adjustments | Nhập / điều chỉnh tồn kho |
| GET | /api/products/{id}/stock-movements | 200 biến động mới nhất |
| POST | /api/sales | Tạo đơn bán và trừ kho |
| GET | /api/sales/{id} | Chi tiết đơn bán |
| GET | /health/live, /health/ready | Trạng thái API / kết nối database |

Lưu các GUID trả về ở từng bước để sử dụng bước tiếp theo. File `SmartStore.http` chứa request mẫu.

```json
// POST /api/categories
{ "name": "Đồ uống" }
// POST /api/products
{ "sku": "SP001", "name": "Cà phê", "price": 25000, "categoryId": "CATEGORY_GUID", "supplierId": null }
// POST /api/products/PRODUCT_GUID/stock-adjustments
{ "quantity": 100, "reason": "Nhập kho ban đầu" }
// POST /api/sales
{ "items": [ { "productId": "PRODUCT_GUID", "quantity": 2 } ] }
```

Sản phẩm mới có tồn kho bằng 0. Giá dùng decimal với tối đa 2 chữ số thập phân trong database; đơn vị tiền tệ mặc định theo quy ước ứng dụng là VND. SKU được trim và chuyển in hoa. Giá bán được chụp lại trên dòng đơn hàng. Không cho tồn âm; trùng SKU/danh mục trả 409; dữ liệu không hợp lệ trả 400. Khi xung đột tồn kho, API trả 409, client cần tải lại trạng thái trước khi thử lại.

## Build và kiểm thử

```powershell
dotnet build -c Release
dotnet test -c Release --filter "Category!=Integration"
dotnet ef migrations has-pending-model-changes --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
```

Integration test chỉ chạy với database test riêng, được phép tạo schema và thêm dữ liệu. Tuyệt đối không trỏ vào database production.

```powershell
$env:ConnectionStrings__Store = "Host=localhost;Database=smartstore_test;Username=smartstore;Password=YOUR_LOCAL_PASSWORD"
dotnet test -c Release --filter "Category=Integration"
```

GitHub Actions khởi chạy PostgreSQL riêng và chạy toàn bộ test.

## Database và migration

Xem [database/README.md](database/README.md). Migration EF là nguồn chuẩn. `database/initial.sql` là script sinh từ migration đầu tiên, có cả bảng lịch sử EF. Chọn EF migration hoặc script SQL cho lần khởi tạo, không chạy cả hai tùy tiện.

```powershell
dotnet ef migrations add TenThayDoi --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
```

## Phạm vi tiếp theo

Chưa có đăng nhập/JWT, phân quyền, giao diện, thanh toán, trả hàng, sửa/xóa danh mục/sản phẩm, đơn nhập hàng hay quản lý nhiều chi nhánh. Không triển khai public API cho dữ liệu thực trước khi bổ sung xác thực, phân quyền và HTTPS. Repository public chỉ chứa mã và dữ liệu cấu hình mẫu, không chứa bí mật.

Xem [docs/architecture.md](docs/architecture.md) để biết các quyết định kiến trúc và lộ trình mở rộng. Chưa chọn giấy phép phân phối mã nguồn; chủ repository có thể bổ sung LICENSE phù hợp.
