# Deployment Design

> Trạng thái: **PLANNED — NOT IMPLEMENTED**. Repository hiện chưa có API, database nghiệp vụ, migration, pipeline hoặc deployment artifact.

## 1. Mục tiêu

M1 phục vụ web UI tĩnh Bootstrap 5/JavaScript từ `Api/wwwroot` cùng origin với `/api/v1`; không có frontend dev server/CDN dependency khi demo. Deployment và cache headers phải giữ `login.html`/shell cập nhật được, JS/CSS pin version, API/auth/key-reveal responses `no-store` phù hợp. UI và API cùng HTTPS origin; không cần mở CORS cho UI nội bộ này. Mọi nội dung ở đây vẫn **PLANNED**.

Thiết kế triển khai ưu tiên một ứng dụng ASP.NET Core modular monolith và một SQL Server riêng biệt. Thiết kế đủ đơn giản cho dự án thực tập, nhưng tách cấu hình/secret và có đường nâng cấp cho môi trường demo hoặc production sau này.

```text
Client
  -> HTTPS / Reverse proxy (môi trường demo/production)
  -> ASP.NET Core API (.NET 10)
  -> SQL Server
```

Không có microservice, message broker, distributed cache hoặc Kubernetes trong MVP.

## 2. Môi trường

| Environment | Mục đích | Database | Secret/config | Trạng thái |
|---|---|---|---|---|
| Local Development | Lập trình và debug | SQL Server local, database riêng theo developer | .NET User Secrets + environment variables | PLANNED |
| Test/CI | Integration test tự động | Database cô lập, tạo mới theo test run | CI secret store | PLANNED |
| Demo/Staging | Mentor review và demo | SQL Server riêng, không dùng dữ liệu production | Secret store của nền tảng | PLANNED |
| Production | Chỉ khi scope được phê duyệt | SQL Server managed/dedicated, backup | Managed secret store | OUT OF SCOPE hiện tại |

Mọi environment phải có database và credential riêng. Không copy dữ liệu thật sang local/test nếu chưa ẩn danh.

## 3. Runtime và package **PLANNED**

- Target framework: .NET 10.
- ASP.NET Core Web API chạy bằng Kestrel; reverse proxy/TLS termination tùy hạ tầng được Mentor xác nhận.
- EF Core SQL Server provider cùng major version với target framework.
- OpenAPI/Swagger chỉ bật có kiểm soát; production không cho phép thao tác không xác thực.
- xUnit cho unit/integration test.

Docker là tùy chọn, không phải dependency bắt buộc vì Docker engine hiện không chạy. Không chọn nền tảng hosting cụ thể trước khi biết hạ tầng demo.

## 4. Configuration và secret

Thứ tự nguồn cấu hình **PLANNED**:

1. `appsettings.json`: chỉ giá trị không nhạy cảm và default an toàn.
2. `appsettings.{Environment}.json`: cấu hình môi trường không có secret.
3. Environment variables / managed secret store.
4. User Secrets chỉ cho local development.

Không commit:

- Password database, JWT signing key, encryption key.
- Connection string có credential.
- License key thực tế.
- `.env`, certificate private key hoặc database backup.

Tên cấu hình nhạy cảm dự kiến gồm `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` và key-encryption material. Log cấu hình phải redact toàn bộ giá trị này.

## 5. Database release strategy **PLANNED**

Migration chỉ bắt đầu sau khi kế hoạch Week 2 được phê duyệt.

1. Tạo migration nhỏ, có tên mô tả thay đổi.
2. Review generated SQL và tác động dữ liệu/index.
3. Chạy migration trên database test mới.
4. Chạy integration test và kiểm tra rollback/recovery.
5. Backup trước thay đổi destructive ở staging/production.
6. Áp dụng migration như một bước release có kiểm soát; API runtime không tự ý migrate production khi startup.

Migration đã áp dụng không bị sửa nội dung; sửa bằng migration tiếp theo. Dữ liệu history/audit không bị drop nếu chưa có migration plan và phê duyệt.

## 6. Release pipeline **PLANNED**

```text
Checkout
  -> Restore
  -> Build (Release)
  -> Unit tests
  -> Integration/security tests
  -> Secret/dependency scan
  -> Publish artifact
  -> Apply reviewed migration
  -> Deploy API
  -> Smoke test
```

Quality gate:

- Restore/build thành công, không compile error.
- Test bắt buộc pass; không bịa hoặc bỏ qua kết quả.
- Git diff và generated migration được review.
- Không phát hiện secret.
- OpenAPI contract và tài liệu liên quan được cập nhật.
- Có backup/rollback plan cho thay đổi database.

## 7. Security vận hành **PLANNED**

- HTTPS bắt buộc ngoài local development; HSTS ở môi trường phù hợp.
- CORS allowlist chính xác, không dùng wildcard với credential.
- Process chạy bằng least-privileged identity; database login chỉ có quyền cần thiết.
- JWT signing/encryption keys có vòng đời và rotation; không dùng key phát triển cho demo/production.
- Encryption key của license key tách khỏi database và backup.
- Swagger, log, error response không để lộ stack trace/secret.
- Upload Excel giới hạn kích thước, extension, content signature, số dòng và thời gian xử lý.

## 8. Observability, backup và recovery **PLANNED**

- Structured application log có correlation ID; không log password, token, full license key hoặc nội dung file nhạy cảm.
- Audit log nghiệp vụ tách khỏi diagnostic log; retention được xác nhận trước production.
- Theo dõi request error rate, latency, database timeout và import failure.
- SQL Server backup định kỳ cho staging/production; kiểm thử restore, không chỉ kiểm tra file backup tồn tại.
- Recovery point/time objective là open question vì chưa có yêu cầu vận hành production.

## 9. Rollback **PLANNED**

- Giữ artifact release trước để rollback application.
- Ưu tiên migration backward-compatible; không dựa vào downgrade tự động cho thay đổi mất dữ liệu.
- Khi schema mới không tương thích, dừng rollout, khôi phục backup theo runbook đã kiểm thử và ghi incident.
- Không xóa history/audit để giải quyết lỗi deployment.

## 10. Verification checklist

Chỉ đánh dấu VERIFIED khi có bằng chứng thực tế ở tuần triển khai:

- [ ] Build Release thành công.
- [ ] Unit/integration/security tests pass.
- [ ] Migration chạy trên database test mới.
- [ ] Secret scan không có finding chưa xử lý.
- [ ] Cấu hình HTTPS/CORS được kiểm chứng.
- [ ] Smoke test các endpoint trọng yếu.
- [ ] Backup restore thử thành công trước production.
- [ ] Screenshot/log bằng chứng được lưu cho báo cáo.

Hiện tại toàn bộ checklist trên là **PLANNED**.
