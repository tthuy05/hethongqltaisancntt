# Git Collaboration — PLANNED

> Snapshot 01/10/2026: tài liệu đã được xuất bản ở `main`/`origin/main`, commit `0c99374`. Các thay đổi bàn giao Tuần 2 hiện ở working tree. Workflow code bên dưới áp dụng sau review và bắt đầu implementation; lượt bàn giao Tuần 2 này không commit, push hay merge.

## Nhánh và review

- `main`: nhánh tích hợp, chỉ nhận thay đổi đã build/test/review; không force-push.
- `feature/thuy-<module>-<short-task>` và `feature/thien-<module>-<short-task>`: mỗi workstream/PR nhỏ. Tên ngắn, không trộn module độc lập.
- Một PR có một Primary Owner theo [team responsibilities](team-responsibilities.md), reviewer là người còn lại. Người tạo PR không tự duyệt PR của mình; merge sau review và gate. `origin/main` đã có documentation baseline; nhánh implementation bắt đầu từ baseline được đồng bộ, không bootstrap lịch sử mới.
- Review trong cùng ngày nếu PR đang nằm trên critical path M1. PR chưa đủ điều kiện không được merge chỉ để có Git history/screenshot.

## Chu trình mỗi task code

1. Pull `main` mới nhất; xác nhận working tree và dependency/API/schema contract.
2. Tạo/cập nhật `feature/thuy-...` hoặc `feature/thien-...` từ `main` đã đồng bộ.
3. Implement một lát cắt nhỏ kèm unit/integration/UI smoke tương ứng; cập nhật docs liên quan.
4. Chạy restore/build, test liên quan, migration verify nếu có, Swagger/Postman/UI smoke theo task. Không dùng test chưa chạy làm evidence.
5. Kiểm diff/secret, commit message ngắn phản ánh đúng thay đổi, push feature branch.
6. Tạo PR ghi task ID, FR/BR/EP/UI, test command/result, schema/API/shared-file impact và screenshot thật nếu có.
7. Người kia review contract, quyền/BOLA, validation/error, transaction/history, test và docs; tác giả xử lý feedback.
8. Merge sau approval và green gate; cả hai pull `main`, chạy smoke liên quan sau integration.

## Shared file và migration lock

- Owner ưu tiên của `Program.cs`, `DbContext`, migrations, shared enums/response, middleware, `appsettings`, package config, frontend layout và `api-client.js` theo bảng tuần trong [team-responsibilities.md](team-responsibilities.md). Không sửa đồng thời trên hai nhánh khi một người đang mở PR shared-file.
- Owner module cần mapping mới gửi entity/config proposal và test mong đợi; Thủy tích hợp DbContext và tạo **một migration theo thứ tự** sau khi pull `main`. Generated migration và SQL phải review; không merge hai migration độc lập cùng base rồi đoán thứ tự.
- API DTO/route đổi phải cập nhật `docs/api-spec.md`, `docs/ui-ux-spec.md`, permission matrix, test và thông báo người dùng contract trước code phụ thuộc. Không đổi field/enum ngầm.
- Nếu có conflict: dừng merge, chốt semantic contract với reviewer, giải conflict trên branch của tác giả, chạy lại build/test; không dùng chiến lược “chọn ours/theirs” mù.
- Nếu critical dependency trễ: dùng fallback trong daily plan (DTO/mock fixture/test/docs trên contract đã freeze); không merge stub giả là feature DONE.

## Database change lock — Neon shared development (PLANNED)

- **Primary database/migration coordinator: Thủy.** Thủy và Thiện dùng chung Neon PostgreSQL development database; database local không là development target chính. Thiện vẫn sở hữu module đã phân công, được sửa entity module, đề xuất schema và review DB; Thủy tích hợp mapping/DbContext/migrations. Không đổi ownership hoặc tạo lại task Tuần 2.
- Trước `dotnet ef migrations add` **hoặc** `dotnet ef database update`, Thiện phải sync với Thủy và có lock tường minh. Thủy cũng xác nhận lock trước lượt của mình. Lock record gồm owner, Git branch/base, schema/migration change, actual Neon project/branch/database và khoảng thời gian; chỉ ghi metadata không nhạy cảm, không credential.
- Một thời điểm chỉ có một schema-changing migration đang được tạo/apply trên baseline shared DB. Schema proposal phải cập nhật database design/ERD và được review trước Entity/Configuration; generated migration/SQL được kiểm trên PostgreSQL test target cô lập trước khi apply shared Neon.
- Thủy apply migration qua **direct endpoint/identity riêng** sau khi cả hai sync baseline, xác nhận target/version và tạm dừng writes nếu change yêu cầu. Runtime dùng pooled endpoint với least-privilege roles; không cấp DDL credential cho backend để tự migrate khi startup. Direct endpoint tránh giới hạn session/advisory locks của PgBouncer transaction pooling. [Neon pooling](https://neon.com/docs/connect/connection-pooling).
- Sau apply: smoke schema/seed/API, ghi kết quả sanitized, commit migration/docs khi workflow implementation cho phép; cả hai sync Git và schema, sau đó Thủy nhả lock. Migration đã apply không được sửa/reorder trên nhánh khác; xử lý thay đổi bằng migration tiếp theo đã review.
- Không merge/apply hai migration độc lập cùng base, không chạy global drop/schema/truncate/reset test trên shared-development/demo DB. Automated tests nhận dedicated disposable PostgreSQL/Neon branch target và fail closed nếu isolation chưa cấu hình; không fallback runtime connection.
- Hiện **NEON SETUP: PLANNED; NEON CONNECTION: NOT CONFIGURED; DATABASE CONNECTION: NOT VERIFIED; MIGRATION: NOT CREATED**. Đây là quy trình tương lai, không evidence tạo/apply migration hoặc mở Tuần 3.

## Definition of Done và checkpoint Thứ 7

Task code chỉ DONE khi implementation hoàn tất, build pass, test liên quan pass, validation/error/permission kiểm, reviewer đã review, docs cập nhật, không secret và không unresolved conflict. PR nhỏ có thể merge giữa tuần để unblock; Thứ 7 là checkpoint tích hợp: merge các PR đủ gate, clean migration test DB, Release build, full test liên quan, Swagger/Postman/UI smoke, DB integrity, bug fix, docs/status và Git checkpoint. Screenshot commit history chỉ từ commit thật sau khi workflow được cho phép trong giai đoạn implementation, không từ task planning.
