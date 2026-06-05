# Lệnh EF Core Migration và Database

Chạy các lệnh dưới đây tại root repo:

```bash
D:\GitHub\BE-SWP
```

## Cài EF Core CLI

Nếu máy chưa có `dotnet ef`, cài bằng lệnh:

```bash
dotnet tool install --global dotnet-ef
```

Kiểm tra version:

```bash
dotnet ef --version
```

## Package cần có cho EF tools

Project chứa `DbContext` và startup project đều nên có `Microsoft.EntityFrameworkCore.Design`.

Trong repo này, `AIStudyHub.Data` đã có package này. Nếu startup project `AIStudyHub.API` báo lỗi thiếu package, chạy:

```bash
dotnet add AIStudyHub.API package Microsoft.EntityFrameworkCore.Design --version 8.0.27
```

## Tạo migration mới

Ví dụ migration khởi tạo schema database ban đầu:

```bash
dotnet ef migrations add InitialDatabaseSchema --project AIStudyHub.Data --startup-project AIStudyHub.API
```

Nếu cần đặt tên migration khác, đổi `InitialDatabaseSchema` thành tên phù hợp:

```bash
dotnet ef migrations add TenMigrationMoi --project AIStudyHub.Data --startup-project AIStudyHub.API
```

## Cập nhật database

Chạy migration vào database:

```bash
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

## Xóa database dev

Cẩn thận: lệnh này xóa toàn bộ database hiện tại theo connection string trong `AIStudyHub.API/appsettings.json`.

Lệnh có hỏi xác nhận:

```bash
dotnet ef database drop --project AIStudyHub.Data --startup-project AIStudyHub.API
```

Lệnh xóa ngay, không hỏi xác nhận:

```bash
dotnet ef database drop --project AIStudyHub.Data --startup-project AIStudyHub.API --force
```

Sau khi xóa, tạo lại database bằng migration:

```bash
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

## Quy trình làm lại database dev từ đầu

Dùng khi database dev bị lệch schema hoặc vừa thêm ASP.NET Core Identity:

```bash
dotnet ef database drop --project AIStudyHub.Data --startup-project AIStudyHub.API --force
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

## Build kiểm tra

Sau khi migration/update database, build lại solution:

```bash
dotnet build AIStudyHub.slnx
```
