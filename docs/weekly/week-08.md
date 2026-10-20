# Week 08 — Mentor Review, System Acceptance, Final Demo & Project Handover

> **PLANNED / NOT ACCEPTED — 20/10/2026.** Bản nháp trước đánh dấu 30/30 DONE thiếu evidence. Các mục dưới là tiêu chí sẽ thực hiện, không phải biên bản hoàn thành; xem [review](../import-reporting-review-2026-10-20.md).

**Database boundary:** development/manual integration/demo dùng shared Neon PostgreSQL với synthetic dataset chuẩn; automated integration/regression/performance tests chạy trên isolated PostgreSQL test target theo [testing strategy](../testing-strategy.md), fail-closed nếu trỏ shared dev. Tuyệt đối không drop/reset schema hoặc truncate tables trên shared DB. Thủy điều phối runtime configuration và demo readiness.

---

## Thứ Hai — 09/11/2026

**Mục tiêu chung:** Rà soát toàn diện hiện trạng hệ thống M1–M5 và lập ma trận nghiệm thu (System Acceptance Matrix).

### THỦY
**Objective:** Tổng kiểm kê kiến trúc, API, RBAC và ma trận nghiệm thu.
**Task List (theo thứ tự):**
- W8-THUY-D1-01 [AUDIT] [M] [PLANNED] Rà soát toàn bộ 94 API endpoints, 28 permission constants, và 18 bảng cơ sở dữ liệu đối chiếu với SRS.
- W8-THUY-D1-02 [DOC] [M] [PLANNED] Xây dựng Ma trận Nghiệm thu Hệ thống (System Acceptance Matrix) chi tiết từng tiêu chí functional và non-functional.
- W8-THUY-D1-03 [TEST] [M] [PLANNED] Chạy full unit/integration regression suite và xác nhận tỷ lệ pass 100% (số test và SKIP theo báo cáo tại thời điểm chạy).
- W8-THUY-D1-04 [SECURITY] [S] [PLANNED] Xác thực lại bảng phân quyền RBAC (ADMIN_IT, SYSTEM_MANAGER, TECHNICAL_SUPPORT) và cơ chế least-privilege cost masking.
- W8-THUY-D1-05 [DOC] [S] [PLANNED] Đồng bộ trạng thái Week 8 vào tài liệu tiến độ chung và danh mục backlog đã đóng.
**Files / Modules:** `docs/week-08-system-acceptance-matrix.md`, test suites, contracts.
**End-of-Day Outcome:** Ma trận nghiệm thu hoàn chỉnh, không có tiêu chí nào bị bỏ sót.
**Verification Plan:** Đối chiếu 1-1 giữa UC/BR và API endpoints, kết quả test suites thật.
**Reviewer:** Thiện.

### THIỆN
**Objective:** Rà soát chéo các module nghiệp vụ và kiểm tra độc lập.
**Task List (theo thứ tự):**
- W8-THIEN-D1-01 [REVIEW] [M] [PLANNED] Rà soát độc lập các module Department, Asset Type, Assignment, Maintenance, License và Import/Export.
- W8-THIEN-D1-02 [TEST] [M] [PLANNED] Chạy smoke tests giao diện người dùng trên 3 kích thước màn hình (Mobile 320px, Tablet 768px, Desktop 1280px).
- W8-THIEN-D1-03 [DOC] [S] [PLANNED] Gửi phản hồi rà soát chéo và ghi nhận các điểm cần lưu ý cho Thủy.
**Files / Modules:** UI modules, module test fixtures, cross-review notes.
**End-of-Day Outcome:** Danh sách phản hồi rà soát chéo từ Thiện.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** Thống nhất ma trận nghiệm thu, chốt phạm vi demo và kịch bản thuyết trình cuối kỳ.

---

## Thứ Ba — 10/11/2026

**Mục tiêu chung:** Soạn thảo Báo cáo Kỹ thuật Tổng kết Dự án Toàn diện (Final Project Report & Technical Dossier).

### THỦY
**Objective:** Hoàn thiện hồ sơ báo cáo kỹ thuật toàn diện cho Mentor và Hội đồng chấm.
**Task List (theo thứ tự):**
- W8-THUY-D2-01 [DOC] [M] [PLANNED] Soạn thảo Báo cáo Tổng kết Dự án Toàn diện: Kiến trúc Clean Architecture, DDD patterns, Security by Design, và Tech Stack (.NET 10, EF Core 10, PostgreSQL Neon, Vanilla JS SPA).
- W8-THUY-D2-02 [DOC] [M] [PLANNED] Tổng hợp phân tích thiết kế cơ sở dữ liệu: 18 bảng, 41 quan hệ foreign key, cơ chế khóa lạc quan (RowVersion), và Audit Trail.
- W8-THUY-D2-03 [DOC] [M] [PLANNED] Trình bày chi tiết chiến lược phòng thủ chiều sâu (Defense-in-Depth): Chống Zip Bomb, XXE, Formula Injection, Brute-force lockout, In-memory JWT, CSRF/XSS, và Least Privilege.
- W8-THUY-D2-04 [ANALYSIS] [S] [PLANNED] Đo lường và tổng hợp số liệu đóng góp mã nguồn (Code Metrics): SLOC, Test Coverage, số lượng API endpoints, tỷ lệ tự động hóa kiểm thử.
- W8-THUY-D2-05 [DOC] [S] [PLANNED] Hoàn thiện phần Tự đánh giá Dự án, So sánh với các giải pháp hiện nay (Snipe-IT, Jira Asset), và Hướng phát triển tương lai.
**Files / Modules:** `docs/week-08-final-project-report.md`, architecture diagrams, metrics summary.
**End-of-Day Outcome:** Báo cáo Tổng kết Dự án hoàn chỉnh, sẵn sàng nộp và trình bày.
**Verification Plan:** Kiểm tra tính chính xác của các sơ đồ, số liệu SLOC và test pass rates.
**Reviewer:** Thiện.

### THIỆN
**Objective:** Đóng góp số liệu và rà soát nội dung báo cáo thuộc phân hệ nghiệp vụ.
**Task List (theo thứ tự):**
- W8-THIEN-D2-01 [DOC] [M] [PLANNED] Tổng hợp tài liệu hướng dẫn sử dụng các phân hệ Cấp phát, Bảo trì, Bản quyền và Import/Export Excel.
- W8-THIEN-D2-02 [DOC] [M] [PLANNED] Bổ sung hình ảnh minh họa quy trình thao tác người dùng thực tế vào báo cáo.
- W8-THIEN-D2-03 [REVIEW] [S] [PLANNED] Đọc duyệt nội dung báo cáo tổng kết của Thủy và xác nhận tính nhất quán thuật ngữ.
**Files / Modules:** User guides, UI screenshots, report addendum.
**End-of-Day Outcome:** Phần tài liệu người dùng và hình ảnh minh họa hoàn tất.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** Hợp nhất nội dung báo cáo kỹ thuật và tài liệu hướng dẫn sử dụng thành hồ sơ hoàn chỉnh.

---

## Thứ Tư — 11/11/2026

**Mục tiêu chung:** Xây dựng Kịch bản Thuyết minh & Demo Trực quan Cuối kỳ Toàn diện (Comprehensive Final Demo Script).

### THỦY
**Objective:** Chuẩn bị kịch bản demo sống động, chặt chẽ từ M1 đến M5.
**Task List (theo thứ tự):**
- W8-THUY-D3-01 [DEMO] [M] [PLANNED] Xây dựng kịch bản Demo trực quan 10 bước bao phủ toàn bộ luồng nghiệp vụ từ Auth -> Users -> Assets -> Assignments -> Maintenance -> Licenses -> Replacement -> Reports/Cost -> Import/Export -> Audit Logs.
- W8-THUY-D3-02 [DEMO] [M] [PLANNED] Thiết kế các tình huống kiểm thử bảo mật trực tiếp trên live demo: Thử nhập sai 5 lần kích hoạt Lockout, thử bypass quyền xem chi phí (Technical Support), thử upload file Excel chứa mã độc Formula Injection.
- W8-THUY-D3-03 [DOC] [M] [PLANNED] Soạn thảo Bộ câu hỏi bảo vệ đồ án chuyên sâu dành cho Lập trình viên Backend kèm câu trả lời chuẩn ghi điểm cao.
- W8-THUY-D3-04 [TEST] [S] [PLANNED] Tổng duyệt kịch bản demo (dry-run rehearsal) với 3 tài khoản demo có sẵn trên môi trường thực tế.
- W8-THUY-D3-05 [DOC] [S] [PLANNED] Lưu tài liệu kịch bản demo chi tiết tại `docs/week-08-final-demo-script.md`.
**Files / Modules:** `docs/week-08-final-demo-script.md`, demo accounts, rehearsal checklists.
**End-of-Day Outcome:** Kịch bản demo từng giây, có lời thoại giải thích kỹ thuật và các màn hình minh chứng.
**Verification Plan:** Chạy thử toàn bộ luồng demo thực tế, đo thời gian hoàn thành (15-20 phút).
**Reviewer:** Thiện.

### THIỆN
**Objective:** Phối hợp rehearsal kịch bản demo và chuẩn bị dữ liệu mẫu.
**Task List (theo thứ tự):**
- W8-THIEN-D3-01 [DEMO] [M] [PLANNED] Chuẩn bị bộ dữ liệu mẫu (synthetic test data) phục vụ phân hệ Cấp phát và Bảo trì.
- W8-THIEN-D3-02 [DEMO] [M] [PLANNED] Chuẩn bị file Excel mẫu hợp lệ và file Excel chứa lỗi để demo chức năng Import.
- W8-THIEN-D3-03 [REVIEW] [S] [PLANNED] Tham gia phiên rehearsal thử nghiệm và bấm giờ cùng Thủy.
**Files / Modules:** Demo Excel files, sample workflows.
**End-of-Day Outcome:** Bộ dữ liệu demo sẵn sàng và khớp với kịch bản.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** Thử nghiệm toàn bộ kịch bản demo từ đầu đến cuối, khắc phục các độ trễ hoặc lỗi phát sinh trong quá trình trình diễn.

---

## Thứ Năm — 12/11/2026

**Mục tiêu chung:** Đánh giá an ninh toàn diện (Security Audit & Vulnerability Assessment) và phòng thủ hệ thống.

### THỦY
**Objective:** Quét bảo mật, rà soát lỗ hổng và lập biên bản đánh giá an toàn thông tin.
**Task List (theo thứ tự):**
- W8-THUY-D4-01 [SECURITY] [M] [PLANNED] Thực hiện audit bảo mật toàn diện: OWASP Top 10 API Security (Broken Object Level Authorization, Broken Authentication, Mass Assignment, Security Misconfiguration, Injection, SSRF).
- W8-THUY-D4-02 [SECURITY] [M] [PLANNED] Rà soát cấu hình Content Security Policy (CSP), CORS, HTTPS, Cookie/Header Flags, và cấm rò rỉ secret key trong source code/git history.
- W8-THUY-D4-03 [TEST] [M] [PLANNED] Thực hiện kiểm thử thâm nhập mô phỏng (Penetration Testing Simulation): SQL Injection, Cross-Site Scripting (XSS), Path Traversal, Zip Bomb, Formula Injection.
- W8-THUY-D4-04 [AUDIT] [S] [PLANNED] Kiểm tra tính toàn vẹn của Audit Trail: Đảm bảo mọi thao tác quan trọng (Create, Update, Delete/Archive, Status Change, Import) đều có log với ActorId, Timestamp, Old/New Values đã che giấu mật khẩu và chi phí.
- W8-THUY-D4-05 [DOC] [S] [PLANNED] Lập Biên bản Báo cáo Kiểm toán An ninh (Security Audit Report) ghi findings và những vấn đề còn mở.
**Files / Modules:** Security reports, audit verifier, CSP checkers, middleware configs.
**End-of-Day Outcome:** Biên bản thẩm định bảo mật sạch, chứng minh hệ thống đạt chuẩn an toàn cao.
**Verification Plan:** Kết quả chạy `npm run check` (51 CSP/boundary checks PASS) và 671 unit tests PASS.
**Reviewer:** Thiện.

### THIỆN
**Objective:** Rà soát an toàn dữ liệu phía client và form validation.
**Task List (theo thứ tự):**
- W8-THIEN-D4-01 [SECURITY] [M] [PLANNED] Rà soát toàn bộ các form nhập liệu phía client: đảm bảo sanitize input, encode HTML entities, và không sử dụng `innerHTML` không an toàn.
- W8-THIEN-D4-02 [TEST] [M] [PLANNED] Kiểm tra hành vi UI khi server trả về các mã lỗi 400, 401, 403, 404, 409, 413, 415.
- W8-THIEN-D4-03 [REVIEW] [S] [PLANNED] Xác nhận không còn URL nào truyền token qua query string trên toàn bộ frontend.
**Files / Modules:** Frontend templates, error handlers, form inputs.
**End-of-Day Outcome:** Frontend đạt chuẩn an toàn và không có lỗi rò rỉ token.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** Thống nhất biên bản an ninh, xác nhận hệ thống an toàn tuyệt đối trước ngày nghiệm thu.

---

## Thứ Sáu — 13/11/2026

**Mục tiêu chung:** Đánh giá hiệu năng tải (Performance Benchmarking) và kịch bản phục hồi sự cố (Disaster Recovery).

### THỦY
**Objective:** Đo lường hiệu năng xử lý, độ trễ API và xác thực quy trình sao lưu/phục hồi dữ liệu.
**Task List (theo thứ tự):**
- W8-THUY-D5-01 [PERF] [M] [PLANNED] Đo lường thời gian phản hồi (Response Time) và thông lượng (Throughput) của các API trọng yếu: Dashboard Summary (< 100ms), Asset Search & Paging (< 150ms), Report Aggregation (< 250ms), Excel Import 1,000 dòng (< 1.5s).
- W8-THUY-D5-02 [DATABASE] [M] [PLANNED] Phân tích kế hoạch thực thi câu lệnh SQL (EXPLAIN ANALYZE) cho các truy vấn phức tạp: Xác nhận tận dụng tối đa Index (AssetCode, SerialNumber, Status, DepartmentId, AssetTypeId), không quét toàn bảng không cần thiết.
- W8-THUY-D5-03 [TEST] [M] [PLANNED] Xác thực kịch bản sao lưu và phục hồi dữ liệu PostgreSQL trên Neon Cloud (Point-in-Time Recovery, Logical Backup `pg_dump`/`pg_restore`).
- W8-THUY-D5-04 [ANALYSIS] [S] [PLANNED] Đánh giá mức độ sử dụng tài nguyên (CPU, RAM, DB Connections pooling với Npgsql) ở trạng thái chịu tải thông thường và đỉnh tải.
- W8-THUY-D5-05 [DOC] [S] [PLANNED] Lập Báo cáo Đo lường Hiệu năng & Khả năng Chịu tải (Performance & Scalability Report).
**Files / Modules:** Performance reports, query plan analysis, connection pool configs.
**End-of-Day Outcome:** Báo cáo hiệu năng chứng minh hệ thống đáp ứng đầy đủ các chỉ số SLA cam kết.
**Verification Plan:** Số liệu đo lường thực tế từ log và công cụ giám sát, không dùng số liệu ước tính.
**Reviewer:** Thiện.

### THIỆN
**Objective:** Kiểm tra hiệu năng giao diện và tốc độ tải trang phía client.
**Task List (theo thứ tự):**
- W8-THIEN-D5-01 [PERF] [M] [PLANNED] Đo lường chỉ số Core Web Vitals (LCP, FID, CLS) trên trình duyệt cho các trang chính (Dashboard, Asset List, Reports).
- W8-THIEN-D5-02 [PERF] [M] [PLANNED] Kiểm tra dung lượng bundle frontend và thời gian tải tài nguyên tĩnh (Static Asset Caching).
- W8-THIEN-D5-03 [REVIEW] [S] [PLANNED] Xác nhận giao diện mượt mà, không giật lag khi chuyển đổi giữa các tab báo cáo lớn.
**Files / Modules:** Frontend assets, browser devtools metrics.
**End-of-Day Outcome:** Báo cáo hiệu năng client xác nhận trải nghiệm người dùng đạt loại Tốt (Good).
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** Tổng hợp báo cáo hiệu năng cả Backend và Frontend, hoàn tất các số liệu kỹ thuật.

---

## Thứ Bảy — 14/11/2026

**Mục tiêu chung:** Đóng gói sản phẩm, bàn giao dự án hoàn chỉnh (Final Handover & Project Sign-off).

### THỦY
**Objective:** Tổng nghiệm thu, đóng gói sản phẩm và hoàn tất bàn giao dự án.
**Task List (theo thứ tự):**
- W8-THUY-D6-01 [INTEGRATION] [M] [PLANNED] Biên dịch hoàn chỉnh Release build cho toàn bộ Solution, xác nhận 0 Warning, 0 Error, và chạy lại toàn bộ test suites (test suites theo checkpoint thực tế).
- W8-THUY-D6-02 [DEPLOY] [M] [PLANNED] Kiểm tra trạng thái triển khai Production trên Render và cơ sở dữ liệu PostgreSQL trên Neon Cloud, xác minh đúng revision và checklist production thực tế.
- W8-THUY-D6-03 [DOC] [M] [PLANNED] Cập nhật toàn diện các tài liệu cốt lõi: `README.md`, `PROJECT_STATUS.md`, `CHANGELOG.md`, `DECISIONS.md`, và Runbook hướng dẫn cài đặt từ đầu (Clean Setup Guide).
- W8-THUY-D6-04 [AUDIT] [S] [PLANNED] Kiểm tra Git status, Git history, đảm bảo cây thư mục sạch sẽ, không có file tạm, file rác hoặc thông tin nhạy cảm.
- W8-THUY-D6-05 [HANDOVER] [S] [PLANNED] Ký biên bản bàn giao kỹ thuật cuối kỳ (Technical Project Handover Sign-off) và chỉ công bố nghiệm thu sau khi các owner/reviewer xác nhận.
**Files / Modules:** `PROJECT_STATUS.md`, `CHANGELOG.md`, `README.md`, final packages.
**End-of-Day Outcome:** Mục tiêu dự kiến: hoàn thiện hồ sơ và evidence để hội đồng/mentor đánh giá.
**Verification Plan:** Clean build, test suite xanh 100%, deployment URL truy cập ổn định, tài liệu đầy đủ và đồng bộ.
**Reviewer:** Thiện / Mentor.

### THIỆN
**Objective:** Xác nhận nghiệm thu độc lập và ký biên bản hoàn thành dự án.
**Task List (theo thứ tự):**
- W8-THIEN-D6-01 [REVIEW] [M] [PLANNED] Rà soát lần cuối toàn bộ tài liệu hướng dẫn và sản phẩm bàn giao.
- W8-THIEN-D6-02 [TEST] [M] [PLANNED] Chạy smoke test độc lập lần cuối trên môi trường live production.
- W8-THIEN-D6-03 [HANDOVER] [S] [PLANNED] Ký biên bản đồng thuận nghiệm thu nội bộ nhóm và bàn giao sản phẩm cho Mentor.
**Files / Modules:** Final handover sign-off, acceptance checklist.
**End-of-Day Outcome:** Nhóm thống nhất 100% hoàn thành dự án.
**Reviewer:** Thủy / Mentor.

**Cuối ngày — Sync / Integration:** Hoàn tất tuần 8! Chúc mừng nhóm đã đưa Hệ thống Quản lý Tài sản CNTT về đích xuất sắc, bảo đảm chất lượng cao nhất cả về tính năng, bảo mật, hiệu năng và tài liệu.
