# Git Collaboration — PLANNED

> Repository hiện tại là `main` chưa có commit; tài liệu đang ở working tree. Workflow bên dưới áp dụng **sau khi** kế hoạch được review và hai thành viên bắt đầu implementation. Task hiện tại **không commit, push hay merge**.

## Nhánh và review

- `main`: nhánh tích hợp, chỉ nhận thay đổi đã build/test/review; không force-push.
- `feature/thuy-<module>-<short-task>` và `feature/thien-<module>-<short-task>`: mỗi workstream/PR nhỏ. Tên ngắn, không trộn module độc lập.
- Một PR có một Primary Owner theo [team responsibilities](team-responsibilities.md), reviewer là người còn lại. Người tạo PR không tự duyệt PR của mình; merge sau review và gate. Nếu GitHub chưa có remote commit/branch, bootstrap repository phải được hai người thống nhất riêng sau planning.
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

## Definition of Done và checkpoint Thứ 7

Task code chỉ DONE khi implementation hoàn tất, build pass, test liên quan pass, validation/error/permission kiểm, reviewer đã review, docs cập nhật, không secret và không unresolved conflict. PR nhỏ có thể merge giữa tuần để unblock; Thứ 7 là checkpoint tích hợp: merge các PR đủ gate, clean migration test DB, Release build, full test liên quan, Swagger/Postman/UI smoke, DB integrity, bug fix, docs/status và Git checkpoint. Screenshot commit history chỉ từ commit thật sau khi workflow được cho phép trong giai đoạn implementation, không từ task planning.
