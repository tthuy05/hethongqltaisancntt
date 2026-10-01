# Roadmap 28/09–07/11/2026 — PLANNED

> Bản kế hoạch 2 thành viên ngày 01/10/2026. Dự án hiện **chỉ có tài liệu**, không có app, DB nghiệp vụ, migration, UI hay test runtime. M1 là mục tiêu ngày **10/10/2026**, không phải trạng thái đạt được. Task lập kế hoạch này không implement/commit/push; Week 3 chỉ bắt đầu sau review/approval của người dùng.

## 1. Nguyên tắc triển khai

1. Audit → requirements/scope → actor/permission → UC/BR → architecture/DB/ERD → API/UI contract.
2. Skeleton + SQL Server migration → auth/RBAC + master data → Asset API → UI thật → M1 integration.
3. Assignment/Maintenance → Software/License/Replacement → Dashboard/Report/Budget → Import/Export/Hardening.
4. Unit/integration/API/UI smoke test và docs update nằm sát ngày viết module; Thứ 7 là integration checkpoint, không là ngày bắt đầu module lớn.
5. Thủy là primary contributor cho architecture, shared files, identity/asset core, frontend core, report và integration. Thiện sở hữu Department, Asset Type, Assignment, Maintenance, License, Replacement và Import/Export, với review chéo. [Team ownership](team-responsibilities.md) và [Git workflow](git-collaboration.md) là quy tắc thực thi.

## 2. Mốc và acceptance

| Mốc | Ngày | Acceptance — tất cả PLANNED |
|---|---|---|
| M0 — Design/Readiness | 03/10/2026 | 36-day plan, owner/dependency/API/UI/schema/test trace; review; không code trong task hiện tại. |
| **M1 — MVP DEMO** | **10/10/2026** | App start, SQL Server connect, initial migration từ DB sạch, login/JWT/RBAC, Department + Asset Type, Asset create/list/detail/edit/archive (API) và search/filter/page trên DB thật, health, Swagger; UI Login→Dashboard→Asset List→Create→Detail/Edit→Search, responsive cơ bản; 400/401/403/409, ít nhất một role restriction, Release build/test M1 pass, rehearsal và ảnh thật. |
| M2 — Operations | 17/10/2026 | Assign/return/transfer/history, maintenance workflow/history, audit; invariant/rollback/permission tests, UI module smoke. |
| M3 — License/Lifecycle | 24/10/2026 | Software/license/seat capacity/key protection, replacement rules/recommendation, alerts và UI; security/concurrency tests. |
| M4 — Reporting | 31/10/2026 | Dashboard/report/budget đúng quyền và số liệu, chart cơ bản, query/index measurement và UI smoke. |
| M5 — Finalization | 07/11/2026 | Import/export, validation/security/performance/regression, clean setup, docs và final demo evidence; issue còn lại ghi trung thực. |
| Week 8 — Mentor review | Sau 07/11 | Kiểm tra tiến độ, báo cáo, demo, tổng kết và sửa lỗi cuối nếu được yêu cầu; không đưa feature mới vào roadmap Week 2–7. |

### M1 demo script và bằng chứng

Login thật → Dashboard có asset data thật/empty state → Asset List → Create Asset → Detail → Edit → Filter/Search. Mở Swagger/Postman chứng minh API/health; xem row SQL Server; đăng nhập Technical Support hoặc gọi API với token Support để chứng minh 403 cho Asset write. Chạy browser 320/768/1280px. Chiều **09/10** dành rehearsal/bug fix/screenshot, ngày **10/10** chỉ integration gate và mentor demo. Ảnh cần: Login, Dashboard, List/filter, Create/Edit/Detail, Swagger, Postman, database, test result và Git history **chỉ nếu có commit thật ở giai đoạn implementation**. Không được giả tạo bằng chứng.

## 3. Roadmap theo tuần

| Week / dates | Thủy — primary | Thiện — independent stream | Gate / test gần ngày code |
|---|---|---|---|
| W2 28/09–03/10 | Audit, FR/scope/permission/architecture/API/UI, DB/ERD integration, ADR, traceability, roadmap | Phản biện requirement, mapping Department/Type/Assignment/Maintenance/License, UI module và test scenarios | M0 tài liệu nhất quán, reviewer chéo; không app code/migration/commit/push trong task hiện tại |
| W3 05/10–10/10 | Skeleton/shared config, auth/JWT/policy, Asset API/search, Login/shell/Dashboard/Asset UI, integration/demo | Department + Asset Type API/lookup UI, schema proposal, fixtures, API/UI/DB smoke, review | M1 bắt buộc có frontend và DB thật; test auth/asset/master ngay khi code |
| W4 12/10–17/10 | Audit framework, status transaction contract, shell/API client support, cross-module integration/security | Assignment, Maintenance + UI, history/concurrency tests | M2 assignment invariant, maintenance state/audit và UI smoke |
| W5 19/10–24/10 | Key-protector/security contract, integration, UI shell/API consistency, review/test | Software/License + allocation UI, replacement rule/evaluation + UI, alerts | M3 capacity/key/rule tests và cost/field scope |
| W6 26/10–31/10 | Dashboard/report/budget API/UI, aggregation/financial permission, performance measurement | Query fixtures, data reconciliation, report validation, chart/UI support, index review | M4 report accuracy/authorization/query evidence |
| W7 02/11–07/11 | Security hardening, full integration/regression/performance, docs/final demo | Excel import/export + UI/test, error-row/rollback, validation support | M5 clean migration/full test/UI smoke/secret review và demo evidence |

## 4. Chính xác 36 ngày và workstream

Chi tiết task ID, type, estimate, file, dependency, fallback, reviewer, verification và sync cuối ngày ở sáu file [W2](weekly/week-02.md), [W3](weekly/week-03.md), [W4](weekly/week-04.md), [W5](weekly/week-05.md), [W6](weekly/week-06.md), [W7](weekly/week-07.md). Mỗi ngày có riêng Thủy và Thiện, status **PLANNED**; không có daily-report template.

| Week | Dates Monday–Saturday | Thủy target | Thiện target |
|---|---|---|---|
| 2 | 28, 29, 30/09; 01, 02, 03/10 | Analysis, technical contract, coordination | Domain review, module contract, validation |
| 3 | 05–10/10 | Critical path M1 backend + frontend | Master data, fixture, review/smoke |
| 4 | 12–17/10 | Audit/integration/permission | Assignment + Maintenance/API/UI |
| 5 | 19–24/10 | Security/shared integration | License + Replacement/API/UI |
| 6 | 26–31/10 | Dashboard/Report/Budget/UI | Fixture/query validation/performance support |
| 7 | 02–07/11 | Hardening/full integration/demo | Import/Export/API/UI/tests |

## 5. Workload estimate và capacity

Quy ước task: **S <1h** (tính đại diện 0,5h), **M 1–3h** (2h), **L 3–5h** (4h), **XL >5h** (6h+; không được đưa vào một ngày nguyên khối). Mỗi ngày plan có Thủy **3M+2S = 7h** và Thiện **2M+1S = 4,5h**; phần còn lại của ngày Thiện là buffer/review/trao đổi, không ghi thành giờ fake. Estimate này là năng lực tương đối của kế hoạch, không phải thời gian thực tế đã làm. Nếu một task M vượt 3h phải tách/điều chỉnh plan.

| Week | Thủy tasks / giờ đại diện | Thiện tasks / giờ đại diện | Tỷ lệ Thủy : Thiện |
|---|---:|---:|---:|
| W2 | 30 / 42h | 18 / 27h | 60,9% : 39,1% |
| W3 | 30 / 42h | 18 / 27h | 60,9% : 39,1% |
| W4 | 30 / 42h | 18 / 27h | 60,9% : 39,1% |
| W5 | 30 / 42h | 18 / 27h | 60,9% : 39,1% |
| W6 | 30 / 42h | 18 / 27h | 60,9% : 39,1% |
| W7 | 30 / 42h | 18 / 27h | 60,9% : 39,1% |
| **Tổng** | **180 / 252h** | **108 / 162h** | **60,9% : 39,1%** |

## 6. Critical path và buffer

`ERD/DB/API/UI contract → skeleton/migration → auth + master lookup → Asset API → UI integration → M1`. [Dependency graph](task-dependencies.md) chỉ rõ deadline và fallback cho dependency của Thiện. Trễ một nút không được giải bằng dữ liệu UI giả; ưu tiên cắt endpoint non-M1 như asset archive/user-admin đầy đủ và ghi deferred task rõ. Thủy giữ shared-file/migration lock; Thiện làm DTO/service/test trong module riêng cho đến khi có contract; review PR M1 trong ngày.

## 7. Definition of Done và weekly integration

Một task code chỉ DONE sau implementation, Release build pass, test liên quan pass, validation/error/permission kiểm, reviewer đã review, docs/API/ERD/status cập nhật, không secret, không conflict. Không có task nào hiện DONE. Thứ 7 mỗi tuần: merge PR đủ gate (nếu workflow được cho phép), clean database/migration verification, build, unit/integration/API/UI smoke, DB integrity, bug fix, docs và Git checkpoint. Nếu không đủ điều kiện, ghi blocker/owner/next action; không tự động bước sang tuần/module tiếp theo như thể gate pass.

## 8. Documentation và change control

Khi module thay đổi contract/schema: owner cập nhật FR/UC/BR, database/ERD, API, UI spec, permission và test cùng PR; Thủy review integration và cập nhật README/PROJECT_STATUS/CHANGELOG/ADR khi cần. Open questions OQ-001–013 phải chốt trước migration/security/cost tương ứng hoặc áp safe assumption được ghi rõ. Mọi thay đổi mốc M1 hoặc tỷ lệ tải cần người dùng/Mentor review. Task lập kế hoạch hiện tại kết thúc sau báo cáo và **STOP**.
