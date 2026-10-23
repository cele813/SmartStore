# SmartStore

Backend quản lý cửa hàng xây dựng bằng C# và ASP.NET Core Web API.

## Công nghệ

- .NET 10, ASP.NET Core Web API
- Entity Framework Core, PostgreSQL
- Git, GitHub Actions, xUnit

## Chức năng hiện có

- Tạo và xem danh mục, nhà cung cấp, sản phẩm.
- Phân trang sản phẩm, điều chỉnh tồn kho và xem lịch sử nhập/xuất.
- Tạo đơn bán hàng, tự động trừ kho và kiểm tra tồn kho.

Dự án đang phát triển, chưa có đăng nhập và phân quyền.

## Cấu trúc

```text
src/
  SmartStore.Api/             API controllers và cấu hình
  SmartStore.Application/     Request DTO và validation
  SmartStore.Domain/          Entities và quy tắc nghiệp vụ
  SmartStore.Infrastructure/  DbContext và EF migrations
tests/SmartStore.Tests/       Kiểm thử
```

## Chạy dự án

Cài .NET SDK 10.0.401 hoặc bản 10.0 mới hơn và PostgreSQL 16 trở lên. Tạo user `smartstore` và database `smartstore` với user này làm owner, sau đó chạy tại thư mục gốc:

```powershell
dotnet tool restore
dotnet restore

# Thay YOUR_PASSWORD bằng mật khẩu PostgreSQL local.
dotnet user-secrets set "ConnectionStrings:Store" "Host=localhost;Port=5432;Database=smartstore;Username=smartstore;Password=YOUR_PASSWORD" --project src/SmartStore.Api

dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
dotnet run --project src/SmartStore.Api --launch-profile http
```

API: http://localhost:5080. OpenAPI: http://localhost:5080/openapi/v1.json (Development).

## Kiểm thử

```powershell
dotnet test --filter "Category!=Integration"
```

Để chạy integration test, tạo database riêng `smartstore_test`, đặt biến môi trường `ConnectionStrings__Store` trỏ tới database đó và chạy `dotnet test`. GitHub Actions tự động build và chạy toàn bộ kiểm thử.
