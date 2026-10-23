# HƯỚNG DẪN DÀNH CHO CÁC THÀNH VIÊN VÀ TÁC NHÂN AI (AGENTS.MD)

Tài liệu này quy định các nguyên tắc làm việc, tiêu chuẩn kỹ thuật và quy trình đồng bộ tài liệu bắt buộc áp dụng cho toàn bộ thành viên dự án (Thủy, Thiện) và các công cụ AI (Antigravity, Codex, Copilot,...).

---

## 1. NGUYÊN TẮC CỐT LÕI

> **Mỗi khi thay đổi chức năng hoặc cấu trúc kỹ thuật của project, phải kiểm tra và cập nhật tài liệu liên quan để người khác có thể hiểu và tiếp tục phát triển.**

1. **Bảo toàn dữ liệu sản xuất & an toàn hạ tầng**:
   - Không tự ý chạy migration trực tiếp trên database Neon shared/production (`neondb`).
   - Mọi migration phải được rehearsal trên database isolated trước khi công bố.
   - Không tự động trigger deploy Render từ tài liệu hoặc các thay đổi thử nghiệm.
   - Không đưa credential, password, JWT secret, API key hoặc connection string vào Git hay tài liệu markdown.

2. **Bảo vệ mã nguồn & lịch sử Git**:
   - Tuyệt đối không `git push --force`, `git reset --hard` hoặc `git clean -fd` làm mất WIP của thành viên khác.
   - Không commit đè các artifact cá nhân (`.docx`, kịch bản báo cáo riêng tư).
   - Phát triển trên nhánh riêng; chỉ hợp nhất vào `main` khi tất cả bài kiểm thử bắt buộc PASS và được phê duyệt.
   - Không bật các module hoặc feature flag chưa hoàn thiện (`AssignmentApi`, `Reporting`, `MaintenanceMode`).

---

## 2. QUY TRÌNH KHI CHỈNH SỬA CHỨC NĂNG

### 2.1. Trước khi sửa (Pre-modification)
1. **Xác định module cần chỉnh sửa**: Khu biệt chính xác phạm vi (ví dụ: Users, Assets, Masters, Maintenance, Reports).
2. **Tìm tài liệu kỹ thuật của module đó**: Tra cứu trong thư mục `docs/` (ví dụ: `database-design.md`, `scope.md`, `use-cases.md`, `business-code-generation.md`).
3. **Đọc tài liệu và đối chiếu với source thực tế**: Kiểm tra schema, DTO, entity, service, controller và giao diện frontend tương ứng.
4. **Xác định đúng phạm vi chỉnh sửa**: Ưu tiên thay đổi tối thiểu, bảo toàn dữ liệu, giữ tương thích ngược.
5. **Không đọc lại hoặc sửa toàn bộ project** nếu không thuộc phạm vi yêu cầu.

### 2.2. Trong khi sửa (Implementation)
1. **Quy tắc sinh mã định danh nghiệp vụ**:
   - Toàn bộ mã nghiệp vụ nội bộ (Tài sản `AST-xxxxxx`, Nhân viên `NVxxxxxx`, Phòng ban `PBxxxxxx`, Bảo trì `BTxxxxxx`, Phần mềm `PMxxxxxx`, Bản quyền `BQxxxxxx`) phải do Backend tự động sinh qua sequence/atomic generator.
   - Người dùng không được tự chỉ định mã qua API hoặc giao diện.
   - Mã là bất biến (`immutable`), không cho phép sửa sau khi đã cấp. Với tài khoản cũ chưa có mã, hệ thống tự động sinh khi cập nhật, không nhận mã tự đặt từ client.
   - Mã phân loại danh mục (`AssetType.Code`) là mã cấu hình ngữ nghĩa thủ công có chủ đích, bất biến sau khi tạo.
   - Không sử dụng `COUNT(*) + 1` hoặc `MAX(id) + 1` thiếu kiểm soát concurrency. Khởi tạo sequence phải dùng advisory lock (`pg_advisory_xact_lock`) và bảo vệ cache trước transaction rollback.
2. **Quy tắc phân quyền & bảo mật**:
   - Kiểm tra quyền ở cả Frontend (ẩn/hiện nút, vô hiệu hóa form) và Backend (`[Authorize(Policy = ...)]`).
   - Che giấu dữ liệu tài chính (`purchaseCost`) nếu tài khoản thiếu quyền `assets.cost.read` hoặc `reports.cost.read`.
   - Không lộ mật khẩu, activation token hoặc hash bảo mật trong response JSON hay audit log.

### 2.3. Sau khi sửa (Post-modification)
1. **Đồng bộ tài liệu kỹ thuật**:
   - Cập nhật tài liệu của chức năng vừa thay đổi trong `docs/`.
   - Cập nhật **API specification** nếu thay đổi endpoint, request DTO hoặc response DTO.
   - Cập nhật **Database specification** nếu thay đổi Entity, Mapping, Index hoặc Sequence.
   - Cập nhật **Quyền (Role/Permission)** nếu có hằng số quyền hoặc phân bổ mới.
   - Cập nhật **Business Rules** mô tả rõ ràng nghiệp vụ, định dạng mã, ràng buộc dữ liệu.
   - Ghi rõ tác động chéo đến các module khác (ví dụ: ảnh hưởng giữa Asset và Maintenance/Assignment).
   - Cập nhật tiến độ vào `PROJECT_STATUS.md`.
2. **Kiểm thử bắt buộc**:
   - `dotnet build --configuration Release`: 0 warnings, 0 errors.
   - Backend Unit Tests: PASS 100%.
   - Backend Integration Tests (offline): PASS 100%.
   - Frontend Tests (`npm.cmd test`): PASS 100%.
   - Frontend Code & CSP Checks (`node scripts/frontend-check.mjs`): PASS.
3. **Không tạo tài liệu trùng lặp**:
   - Tận dụng và bổ sung vào các file tài liệu chuyên đề đã có trong `docs/`.
   - Tránh tạo các file ghi chú vụn vặt; khi bàn giao module, lập file handoff có cấu trúc rõ ràng.

---

## 3. NGUYÊN TẮC QUẢN LÝ TÀI LIỆU KỸ THUẬT

1. **Tính trung thực và có bằng chứng (Evidence-based)**:
   - Chỉ ghi nhận trạng thái hoàn thành (`COMPLETED`, `VERIFIED`, `PASS`) khi đã thực sự chạy kiểm thử và có log đối chiếu.
   - Tuyệt đối không xem các bài kiểm thử bị bỏ qua (`SKIP`) là `PASS`.
   - Các chức năng chưa xong phải ghi rõ `PLANNED` hoặc `UNDER REVIEW`.
2. **Cấu trúc tài liệu module chuẩn**:
   Mỗi module trong `docs/` cần thể hiện đủ 8 nội dung:
   - **Chức năng và cách hoạt động**: Luồng xử lý chính từ UI đến Database.
   - **API & DTO**: Các endpoint HTTP, phương thức, cấu trúc body, mã lỗi RFC7807.
   - **Quy tắc nghiệp vụ**: Điều kiện hợp lệ, ràng buộc dữ liệu, quy tắc sinh mã.
   - **Phân quyền & Bảo mật**: Quyền cần thiết, kiểm tra quyền theo vai trò.
   - **Thành phần phụ thuộc**: Các service, repository, bảng cơ sở dữ liệu liên quan.
   - **Trạng thái triển khai**: Đã release, đang kiểm thử hay đang phát triển.
   - **Những phần chưa hoàn thiện**: Các giới hạn kỹ thuật cần tiếp tục xử lý.
   - **Lưu ý phát triển tiếp**: Hướng dẫn chi tiết cho thành viên khác tiếp quản.

---

## 4. QUY ĐỊNH PHỐI HỢP GIỮA CÁC THÀNH VIÊN VÀ AI

- **Thủy**: Phụ trách chính phần Frontend, Reports UI, License alert queries, Role matrix và kiểm thử E2E giao diện.
- **Thiện**: Phụ trách chính phần Backend Cấp phát tài sản (Assignment) và Bảo trì (Maintenance).
- **Codex / Antigravity / AI Agents**:
  - Đóng vai trò kỹ sư cặp (pair-programming), giải quyết conflict, tối ưu hóa kiến trúc, kiểm tra an toàn hệ thống và đồng bộ tài liệu.
  - Phải tuân thủ nghiêm ngặt các quy tắc trên, đọc kỹ các ghi chú bàn giao trước khi bắt đầu và bàn giao sạch sẽ sau khi hoàn tất.
