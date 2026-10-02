# Deployment Design

> Trạng thái: **PLANNED — NOT IMPLEMENTED**. Repository hiện chưa có API, database nghiệp vụ, migration, pipeline hoặc deployment artifact.

## 1. Mục tiêu

M1 dự kiến phục vụ web UI HTML/Tailwind/JavaScript theo ADR-021 từ một `Api/wwwroot/index.html` shell có Login/admin views cùng origin với `/api/v1`; hash navigation không tải HTML mới để giữ token in-memory. Không có frontend dev server/CDN dependency khi real demo; Node localhost static preview hiện tại chỉ phục vụ review mock trước backend. Build output/CSS/fonts phải được package vào ASP.NET ở skeleton được approve; production không bật demo flag hoặc deploy mock fixtures. Deployment/cache headers phải giữ shell cập nhật được, JS/CSS pin version, API/auth/key-reveal responses `no-store` phù hợp. Reload/tab mới cần đăng nhập lại. UI/API cùng HTTPS origin; không cần mở CORS cho UI nội bộ này. Real deployment và toàn bộ Neon setup ở đây vẫn **PLANNED**.

Thiết kế triển khai ưu tiên một ứng dụng ASP.NET Core modular monolith dùng EF Core/Npgsql và PostgreSQL hosted on Neon. Thủy và Thiện chạy backend trên máy của mình, cùng kết nối shared Neon development database; local database không là development target chính. Kiến trúc UI/API và milestone MVP **10/10/2026** giữ nguyên.

```text
Client
  -> HTTPS / Reverse proxy (môi trường demo/production)
  -> ASP.NET Core API (.NET 10)
  -> Entity Framework Core
  -> Npgsql.EntityFrameworkCore.PostgreSQL
  -> PostgreSQL hosted on Neon
```

Không có microservice, message broker, distributed cache hoặc Kubernetes trong MVP.

## 2. Môi trường

| Environment | Mục đích | Database | Secret/config | Trạng thái |
|---|---|---|---|---|
| Local Backend / Shared Development | Thủy/Thiện lập trình và debug trên máy riêng | Cùng Neon PostgreSQL development database, runtime roles riêng | .NET User Secrets hoặc environment variables trên từng máy | PLANNED / NOT YET CONNECTED |
| Test/CI | Integration test tự động | PostgreSQL disposable database hoặc Neon test branch cô lập từng run, khác shared dev | Dedicated test secret; CI secret store khi có CI | PLANNED |
| MVP Demo / Manual Integration | Mentor review và demo | Shared Neon development database, demo records đã kiểm; không reset toàn cục | Runtime secrets riêng của backend demo | PLANNED |
| Staging / Production | Chỉ khi scope được phê duyệt | Neon PostgreSQL environment/branch riêng, recovery plan theo khả năng thực tế | Managed secret store | OUT OF SCOPE hiện tại |

Shared development/MVP demo là cùng target theo quyết định hiện tại; test/CI và môi trường staging/production tương lai phải cô lập khỏi target này. Không copy dữ liệu thật sang test nếu chưa ẩn danh. **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED.** Chưa có thông tin project/branch/host/database/roles/version thực tế; application physical schema và EF migrations chưa được tạo.

## 3. Runtime và package **PLANNED**

- Target framework: .NET 10.
- ASP.NET Core Web API chạy bằng Kestrel; reverse proxy/TLS termination tùy hạ tầng được Mentor xác nhận.
- EF Core provider `Npgsql.EntityFrameworkCore.PostgreSQL`; chọn version sau khi đối chiếu `dotnet --version`, `dotnet --list-sdks`, target framework, EF Core version và compatibility/dependency của Npgsql provider. Không suy version provider chỉ từ số SDK; chưa chọn/cài package trong phase này.
- OpenAPI/Swagger chỉ bật có kiểm soát; production không cho phép thao tác không xác thực.
- xUnit cho unit/integration test.

Docker là tùy chọn, không phải dependency bắt buộc vì Docker engine hiện không chạy. Không chọn nền tảng hosting cụ thể trước khi biết hạ tầng demo.

## 4. Configuration và secret

Thứ tự nguồn cấu hình **PLANNED**:

1. `appsettings.json`: chỉ giá trị không nhạy cảm và default an toàn.
2. `appsettings.{Environment}.json`: cấu hình môi trường không có secret.
3. User Secrets chỉ cho Development trên máy developer, sau khi project skeleton tồn tại.
4. Environment variables / managed secret injection ghi đè cấu hình theo host ASP.NET Core mặc định; kiểm precedence thực tế khi triển khai.

Không commit:

- Password database, JWT signing key, encryption key.
- Connection string có credential.
- License key thực tế.
- `.env`, certificate private key hoặc database backup.

ASP.NET Core connection key là `ConnectionStrings:DefaultConnection`, có thể inject bằng .NET User Secrets hoặc environment variable `ConnectionStrings__DefaultConnection`. Giá trị runtime lấy từ actual Neon .NET pooled connection string; không ghi fake host, username hoặc password vào tài liệu. `Jwt__SigningKey` và key-encryption material cũng là secrets. Không log connection string, username/password hoặc secret values.

Runtime ưu tiên **Neon pooled endpoint**; Npgsql client pool được giới hạn theo compute/concurrency thực tế của hai backend. Migration dùng **direct endpoint với credential riêng**, inject vào configuration chỉ trong migration process do Thủy điều phối; API không nhận DDL credential. Đây là lựa chọn vận hành để tránh phụ thuộc session/advisory lock trong PgBouncer transaction pooling, không phải khẳng định mọi EF migration đều không thể chạy pooled. Không dùng session-level `SET`/`search_path` làm điều kiện cho runtime; schema/table mapping tường minh theo database design. [Neon connection pooling](https://neon.com/docs/connect/connection-pooling), [Npgsql pooling parameters](https://www.npgsql.org/doc/connection-string-parameters).

TLS Npgsql→Neon dùng `SSL Mode=VerifyFull` và certificate/hostname validation; kiểm `Channel Binding=Require` với driver/endpoint thực tế khi setup. Không dùng `Trust Server Certificate`, không in secret lúc connection failure. [Npgsql TLS](https://www.npgsql.org/doc/security.html), [Neon secure connections](https://neon.com/docs/connect/connect-securely).

### 4.1 Manual Neon setup needed — PLANNED

1. Người dùng tạo/chọn Neon account và project, region phù hợp đường mạng demo, PostgreSQL major version được Neon hỗ trợ; ghi non-secret metadata thực tế. Task này chưa tạo account/project hoặc kết nối.
2. Chọn shared-development branch/compute/database dùng chung cho Thủy/Thiện. `it_asset_management_dev` chỉ là tên logical đề xuất; dùng database thật được chọn/tạo trong Neon, không giả định Neon mặc định dùng tên này.
3. Cấp quyền project/Console phù hợp cho hai thành viên qua tài khoản riêng. Thủy điều phối database; sau khi được phép setup, provision owner/migration identity riêng và runtime least-privilege roles bằng SQL với grants tường minh. Role tạo bằng Console/CLI/API có thể mang `neon_superuser`, không dùng trực tiếp làm runtime role. [Neon roles](https://neon.com/docs/manage/roles).
4. Trong **Connect**, chọn đúng actual Branch/Compute/Database/Role và phương thức .NET: pooled cho runtime, direct cho migration. Mỗi người tự lưu connection secret trên máy qua User Secrets/environment; migration secret do Thủy quản lý riêng. **Không gửi password, full connection string hoặc API key vào chat**; chỉ chia sẻ non-secret inventory và trạng thái đã cấu hình.
5. Trước automated integration tests, provision disposable test branch/database và dedicated test secret theo [testing isolation](testing-strategy.md#31-database-isolation--planned). Không chuyển fixture sang shared dev nếu chưa có test target.
6. Week 3 sau khi implementation được phép: xác minh actual PostgreSQL version/database/TLS/permissions/network, initial migration trên test target trước khi Thủy apply lên shared Neon; kiểm schema/seed/health và ghi evidence sanitized. Chưa bước nào ở đây được ghi VERIFIED.

## 5. Database release strategy **PLANNED**

**Migration: NOT CREATED.** Migration chỉ bắt đầu sau khi kế hoạch Week 2 được phê duyệt, schema/module được phép triển khai và Neon setup có credentials thật. Thủy là primary migration owner; Thiện review, mọi lượt tạo/apply tuân thủ [database change lock](git-collaboration.md#database-change-lock--neon-shared-development-planned).

1. Xác định requirement/schema change.
2. Cập nhật `database-design.md`.
3. Cập nhật `erd.md` nếu schema/relationships bị ảnh hưởng.
4. Review với Thiện và nhận migration lock của Thủy.
5. Sửa Entity/Configuration theo contract đã review.
6. Thủy tạo EF migration nhỏ từ Git/schema baseline đã đồng bộ.
7. Review generated migration/SQL, dữ liệu/index/quyền; apply và integration test trên disposable target, kiểm recovery cho thay đổi có rủi ro.
8. Thủy apply qua direct endpoint lên đúng shared Neon target đã xác minh trong maintenance window đã sync; không có hai schema-changing migrations độc lập cùng lúc.
9. Smoke test schema/migration history/seed/API và thông báo kết quả để cả hai sync.
10. Commit migration cùng schema/docs sau gate/review trong phase implementation được phép; nhả lock. Task đổi design hiện tại không tạo hoặc commit migration.

API runtime không tự migrate shared database lúc startup. Backup/recovery trước thay đổi destructive được kiểm theo Neon plan/retention thực tế; không tuyên bố backup/restore có sẵn khi chưa kiểm.

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
- Neon restore/backup retention và khả năng phục hồi phụ thuộc project/plan thực tế, cần xác nhận và restore drill trước staging/production; `pg_dump`/`pg_restore` nếu được chọn dùng direct endpoint và backup artifact mã hóa, quyền hạn chế. Không giả định fixed retention hoặc một branch thay thế đầy đủ backup.
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
