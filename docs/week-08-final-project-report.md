# Báo cáo kỹ thuật dự án — bản nháp để review

Cập nhật20/10/2026. **DRAFT / NOT FINAL ACCEPTANCE.** Báo cáo này mô tả source hiện có và kế hoạch; chưa xác nhận toàn bộ M1–M5 hoặc production hoàn tất.

## Mục tiêu và kiến trúc

Hệ thống quản lý tài sản CNTT: Auth/RBAC, vòng đời Asset, cấp phát/bảo trì, Software/License, đề xuất thay thế, Dashboard/Reports, Import/Export và Audit. ASP.NET Core Web API, Application services/contracts, Domain persistence entities, EF Core/Npgsql/PostgreSQL trên Neon, Vanilla JS SPA. Các project tách tầng; không tự coi toàn source là đầy đủ DDD hoặc mọi nghiệp vụ đã triển khai.

## Schema Baseline V1

18 bảng/41 FK là baseline đã có migration/source và evidence triển khai riêng. Snapshot hiện tại:
users, roles, permissions, user_roles, role_permissions, departments, asset_types, assets, asset_status_histories, asset_assignments, maintenance_tickets, maintenance_histories, softwares, software_licenses, license_assignments, replacement_rules, replacement_recommendations, audit_logs.

Không có bảng user_invitations/app_settings trong baseline; invitation metadata nằm ở user/account implementation. Không dùng maintenance_records/license_allocations thay cho tên thật. UserRole/RolePermission có surrogate Id; unique pair riêng. Concurrency là row_version bytea token16byte ngẫu nhiên do application tạo, EF concurrency token, không ánh xạ xmin. License key dùng encryption/protection contract; không gọi là hash nếu cần reveal lại.

## Trạng thái thực tế

Week2 technical design/evidence30task được giữ. Auth/Asset/User/RBAC/Audit và frontend fixes có các handoff/CI riêng trên nhánh review; production main không tự đồng bộ với WIP. Schema vật lý hoàn thiện không chứng minh API/UI module đã hoàn tất.

License capacity/metrics/crypto/alert/policy đã chuẩn bị; allocation/evaluator cần Thiện integration/transaction/race/HTTP acceptance. Reporting/ImportExport local fixes được mô tả ở [review20/10](import-reporting-review-2026-10-20.md); default rollout disabled, permissions chưa seed. Export/upload helper không thay thế Import page của Thiện.

## Chất lượng và giới hạn

Counts, lệnh test và evidence thực tế dùng báo cáo review mới nhất; cloud SKIP tách khỏi PASS. Không quy đổi số test thành line coverage. Diagnostic timings dataset nhỏ không phải SLA/load test. EXPLAIN tuning, production performance, PITR readiness, rehearsal với cả nhóm, email inbox E2E và Mentor sign-off còn PENDING/UNVERIFIED tùy gate.

Connection/configuration giữ nguyên theo yêu cầu owner; không công bố security gate sạch tuyệt đối hoặc Zero High/Critical. Không liệt kê secret trong báo cáo. Build/CI/synthetic smoke chỉ xác minh đúng phạm vi được chạy.

## Hướng tiếp theo

Review source với Thiện, hoàn thiện owner APIs/UI, chạy isolated concurrency/integration, joint browser rehearsal và hoạch định production rollout riêng. Chỉ ký biên bản bàn giao sau khi acceptance matrix được các owner/mentor xác nhận.
