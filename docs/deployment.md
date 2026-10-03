# Deployment Design

> **LOCAL M1 API/UI/NEON VERIFIED; PRODUCTION DEPLOYMENT PLANNED.** Same-origin Development hosting, Auth/master/Asset API and persisted data exist. [Current runbook](m1-backend-handoff.md). Repository owner credential is an explicit user exception, not production approval.

## 1. Mục tiêu

Development serves built HTML/Tailwind/JS from `artifacts/frontend` and API on localhost:5080 with hash navigation/in-memory JWT and CSP/no-store. Explicit localhost demo remains separate. Node server is static preview only. Production static packaging/HTTPS/proxy/key management/least privilege/CI remain PLANNED; source/mock fixtures are excluded from .NET publish. No startup seed/migration.

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

Shared development/demo target thực là `neondb`, PostgreSQL 18.6, UTF8/C.UTF-8; **M1 schema/connection VERIFIED** bằng setup credential. Isolated validation database `it_asset_management_m1_verify_20261002` được tạo mới, schema tương tự, fixture rollback. Không tạo Neon account/project/branch; user tạo project, Console project/branch labels/access chưa independent verification. Runtime roles/DefaultConnection, seed, CI/test role và deployment vẫn PLANNED.

## 3. Runtime và package — foundation VERIFIED

- Target framework: .NET 10.
- ASP.NET Core Web API chạy bằng Kestrel; reverse proxy/TLS termination tùy hạ tầng được Mentor xác nhận.
- Npgsql provider **10.0.3** (official package requires EF >=10.0.4 <11); EF Core/Relational/Design, dotnet-ef, ASP.NET OpenAPI/Mvc.Testing **10.0.11**, SDK **10.0.400** verified. Central versions + 6 lock files; tools local manifest, no global tool install. [Package details](neon-database-setup.md).
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

Runtime key `ConnectionStrings:DefaultConnection` / env `ConnectionStrings__DefaultConnection` hiện để trống, cần least-privilege pooled credential. Setup key riêng `ConnectionStrings:NeonSetupConnection` / env `ConnectionStrings__NeonSetupConnection` chỉ Development CLI, secret trên máy ngoài Git. Helper masked input `scripts/configure-neon-secret.ps1` dùng JSON stdin; không password trong command args, Program.cs hoặc logs. User Secrets không encrypted, không là production vault. JWT/encryption secrets chưa tạo.

Runtime ưu tiên **Neon pooled endpoint**; Npgsql client pool được giới hạn theo compute/concurrency thực tế của hai backend. Migration dùng **direct endpoint với credential riêng**, inject vào configuration chỉ trong migration process do Thủy điều phối; API không nhận DDL credential. Đây là lựa chọn vận hành để tránh phụ thuộc session/advisory lock trong PgBouncer transaction pooling, không phải khẳng định mọi EF migration đều không thể chạy pooled. Không dùng session-level `SET`/`search_path` làm điều kiện cho runtime; schema/table mapping tường minh theo database design. [Neon connection pooling](https://neon.com/docs/connect/connection-pooling), [Npgsql pooling parameters](https://www.npgsql.org/doc/connection-string-parameters).

TLS Npgsql→Neon dùng `SSL Mode=VerifyFull` và certificate/hostname validation; kiểm `Channel Binding=Require` với driver/endpoint thực tế khi setup. Không dùng `Trust Server Certificate`, không in secret lúc connection failure. [Npgsql TLS](https://www.npgsql.org/doc/security.html), [Neon secure connections](https://neon.com/docs/connect/connect-securely).

### 4.1 Historical setup checklist + remaining manual access — PLANNED

1. Project đã được người dùng tạo, actual database/version/connection/schema verified 02/10. Còn cần xác nhận Console project/branch dùng làm shared development (không suy từ hostname), access inventory và region/backup policy.
2. Chọn shared-development branch/compute/database dùng chung cho Thủy/Thiện. `it_asset_management_dev` chỉ là tên logical đề xuất; dùng database thật được chọn/tạo trong Neon, không giả định Neon mặc định dùng tên này.
3. Cấp quyền project/Console phù hợp cho hai thành viên qua tài khoản riêng. Thủy điều phối database; sau khi được phép setup, provision owner/migration identity riêng và runtime least-privilege roles bằng SQL với grants tường minh. Role tạo bằng Console/CLI/API có thể mang `neon_superuser`, không dùng trực tiếp làm runtime role. [Neon roles](https://neon.com/docs/manage/roles).
4. Trong **Connect**, chọn đúng actual Branch/Compute/Database/Role và phương thức .NET: pooled cho runtime, direct cho migration. Mỗi người tự lưu connection secret trên máy qua User Secrets/environment; migration secret do Thủy quản lý riêng. **Không gửi password, full connection string hoặc API key vào chat**; chỉ chia sẻ non-secret inventory và trạng thái đã cấu hình.
5. Dedicated verification database đã tạo cho lượt setup này; fixtures rollback, không drop/reset database dùng chung. CI isolation/separate test role/secret còn PLANNED. Không chạy test DML trên shared dev.
6. Initial M1 migration/direct TLS/constraints/catalog đã verified theo yêu cầu riêng. Tiếp theo review Thiện, provision runtime roles, seed/health readiness rồi Auth/master/Asset/UI integration sau authorization; không coi các phần này DONE.

## 5. Database release strategy **PLANNED**

**Migration: `20261002151601_InitialM1` CREATED / APPLIED 02/10/2026.** Thiện independent review vẫn pending; user yêu cầu thực hiện DB setup riêng ngay. Protocol dưới đây tiếp tục áp dụng cho migration sau; primary owner Thủy, không sửa migration đã apply hoặc auto-migrate API startup. [Lock record](git-collaboration.md#database-change-lock--neon-shared-development-planned).

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
