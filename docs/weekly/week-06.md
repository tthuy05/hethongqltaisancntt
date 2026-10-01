# Week 06 — Dashboard, Reports & Budget

> **PLANNED — NOT IMPLEMENTED.** Entry: M3 data/history/cost/recommendations trustworthy. Thủy owns API/report/budget/UI; Thiện owns independent fixture/reconciliation/chart helper/query validation. Daily capacity: Thủy 3M+2S, Thiện 2M+1S (7h/4,5h đại diện); M=1–3h, S<1h. Không tuyên bố performance pass nếu chưa đo dataset thực.

## Thứ Hai — 26/10/2026

**Mục tiêu chung:** định nghĩa report query/metric/fixture trước implementation.

### THỦY
**Objective:** chốt nguồn số liệu và permission.
**Task List (theo thứ tự):**
- W6-THUY-D1-01 [ANALYSIS] [M] [PLANNED] Map EP-072–086 tới bảng/current snapshot, date range/business timezone/currency và archived policy.
- W6-THUY-D1-02 [BACKEND] [M] [PLANNED] Tạo report filter/page/sort DTO + query repository contract dùng DB projection, không in-memory full table.
- W6-THUY-D1-03 [SECURITY] [M] [PLANNED] Chốt field cost/department scope trên dashboard/report/export query dùng chung.
- W6-THUY-D1-04 [TEST] [S] [PLANNED] Tạo known-count assertion cho tổng Asset theo status/department/type.
- W6-THUY-D1-05 [DOC] [S] [PLANNED] Ghi query metric/formula trong API/UI spec để Thiện đối chiếu.
**Files / Modules:** `Application/Reporting/*`, `Infrastructure/Queries/*`, report DTO/tests, API/UI docs.
**End-of-Day Outcome:** query contract và dataset expectation không mơ hồ.
**Verification Plan:** từng metric có source/formula/permission; no historical ownership claim.
**Dependency:** stable M3 tables/current recommendation. **Fallback Task:** chốt asset-only metrics trước, đánh dấu widget License/Replacement unavailable khi dữ liệu lỗi. **Reviewer:** Thiện.

### THIỆN
**Objective:** fixture kiểm số liệu độc lập.
**Task List (theo thứ tự):**
- W6-THIEN-D1-01 [TEST] [M] [PLANNED] Tạo synthetic dataset có Asset/status/department/type/assignment/ticket/license/recommendation edge cases.
- W6-THIEN-D1-02 [ANALYSIS] [M] [PLANNED] Tính expected counts/cost/budget bằng tay từ fixture, ghi currency/year/missing data.
- W6-THIEN-D1-03 [REVIEW] [S] [PLANNED] Đối chiếu API group/permission với metric UI; gửi mismatch.
**Files / Modules:** reporting test fixture/builders, expected-value notes, API/UI review.
**End-of-Day Outcome:** oracle số liệu độc lập để bắt double count.
**Verification Plan:** fixture reproducible, không dùng production data, expected totals tính lại được.
**Dependency:** metric definition Thủy. **Fallback Task:** fixture Asset/Maintenance trước; bổ sung License/Replacement sau.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze formula actual_cost vs history snapshot, one-current recommendation, current department/type, permission/currency; Thủy giữ report query/DTO, Thiện giữ fixture.

## Thứ Ba — 27/10/2026

**Mục tiêu chung:** dashboard aggregates/API/UI theo quyền.

### THỦY
**Objective:** hoàn thiện dashboard endpoint thật.
**Task List (theo thứ tự):**
- W6-THUY-D2-01 [API] [M] [PLANNED] Implement EP-072 summary + EP-073 Asset groups bằng SQL aggregate/projection.
- W6-THUY-D2-02 [API] [M] [PLANNED] Implement EP-074 alerts + EP-075 costs theo field policy, configurable daysAhead.
- W6-THUY-D2-03 [FRONTEND] [M] [PLANNED] Nâng Dashboard M1 lên cards/charts operational/cost theo role, loading/empty/error/refresh.
- W6-THUY-D2-04 [TEST] [S] [PLANNED] Chạy dashboard accuracy/403/field omission integration tests.
- W6-THUY-D2-05 [DOC] [S] [PLANNED] Đồng bộ dashboard API/UI contract theo actual fields.
**Files / Modules:** Dashboard query/service/controller/tests, `wwwroot/js/dashboard.js`, docs.
**End-of-Day Outcome:** Dashboard data thật, Support không thấy financial widget/data.
**Verification Plan:** fixture counts, 403 cost, browser role views, no N+1 obvious.
**Dependency:** D1 query contract/fixture. **Fallback Task:** EP-072/073 asset-only trước; metric thiếu data hiển thị unavailable, không zero giả. **Reviewer:** Thiện.

### THIỆN
**Objective:** kiểm Dashboard và chart helper độc lập.
**Task List (theo thứ tự):**
- W6-THIEN-D2-01 [TEST] [M] [PLANNED] Chạy known-fixture Dashboard total/status/type/department/alert/cost comparisons và 401/403.
- W6-THIEN-D2-02 [FRONTEND] [M] [PLANNED] Tạo chart/table helper riêng dùng dữ liệu DashboardSeries, label/empty/accessible fallback.
- W6-THIEN-D2-03 [REVIEW] [S] [PLANNED] Review Dashboard API/UI mismatch và financial field leak.
**Files / Modules:** Dashboard integration tests, `wwwroot/js/charts.js`, review note.
**End-of-Day Outcome:** chart helper và dashboard oracle được kiểm.
**Verification Plan:** browser data label/empty, known totals, Support no cost.
**Dependency:** EP-072–075 từ Thủy. **Fallback Task:** test fixture/chart with static fixture, không báo Dashboard DONE.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze DashboardSeries/alerts/cost DTO, role response; Thủy giữ Dashboard page/API client, Thiện chỉ chart helper/test.

## Thứ Tư — 28/10/2026

**Mục tiêu chung:** inventory/history/expiry reports và UI filters.

### THỦY
**Objective:** report phi tài chính trước cost/budget.
**Task List (theo thứ tự):**
- W6-THUY-D3-01 [API] [M] [PLANNED] Implement EP-076–079 inventory/department/type/status queries, page/filter/sort allowlist.
- W6-THUY-D3-02 [API] [M] [PLANNED] Implement EP-080/081/083/084 assignment/maintenance/warranty/license history/expiry queries theo scope.
- W6-THUY-D3-03 [FRONTEND] [M] [PLANNED] Tạo Reports page tabs/filter/table/page/empty/loading/error cho inventory/history/expiry.
- W6-THUY-D3-04 [TEST] [S] [PLANNED] Chạy invalid filter/date/page/role/masked license tests.
- W6-THUY-D3-05 [DOC] [S] [PLANNED] Ghi report current-snapshot limitation trong UI/API.
**Files / Modules:** Reporting query/controller/tests, `wwwroot/js/reports.js`, API/UI docs.
**End-of-Day Outcome:** report cơ bản query DB và UI đọc được.
**Verification Plan:** known fixture rows, paging/sorting, 400/403, masked key, browser filters.
**Dependency:** D1 contract + module histories M2/M3. **Fallback Task:** inventory/expiry trước, history report chờ fixture nếu data relation lỗi. **Reviewer:** Thiện.

### THIỆN
**Objective:** độc lập kiểm report hàng/field/filter.
**Task List (theo thứ tự):**
- W6-THIEN-D3-01 [TEST] [M] [PLANNED] Viết/chạy report accuracy tests inventory/history/expiry trên fixture, bao gồm archived/empty/date boundary.
- W6-THIEN-D3-02 [UI] [M] [PLANNED] Smoke Reports tabs/filter/page ở mobile/desktop, kiểm chart/table helper và export button vẫn disabled Week 6.
- W6-THIEN-D3-03 [REVIEW] [S] [PLANNED] Review query không lộ ticket cost/key cho Support.
**Files / Modules:** Reporting tests, chart/table helper, UI smoke notes.
**End-of-Day Outcome:** report oracle và UI review có evidence.
**Verification Plan:** expected rows vs API, 403/masking, 320px overflow.
**Dependency:** report endpoints/page Thủy. **Fallback Task:** hoàn thiện assertion/fixture trước endpoint.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze report filters/column names/permission và current snapshot; không thêm `asOfDate` lịch sử thiếu schema; Thủy giữ `reports.js`/query.

## Thứ Năm — 29/10/2026

**Mục tiêu chung:** maintenance cost, replacement report và annual budget.

### THỦY
**Objective:** số liệu tài chính đúng và chống double count.
**Task List (theo thứ tự):**
- W6-THUY-D4-01 [API] [M] [PLANNED] Implement EP-082 maintenance cost + EP-085 replacement report với scope/cost permission.
- W6-THUY-D4-02 [API] [M] [PLANNED] Implement EP-086 annual budget: một current recommendation/Asset/year, missing estimate/year counts, decimal/currency.
- W6-THUY-D4-03 [FRONTEND] [M] [PLANNED] Thêm cost/budget tabs/chart vào Reports/Dashboard, 403/empty/missing-data state.
- W6-THUY-D4-04 [TEST] [S] [PLANNED] Chạy known-fixture total/subtotal/year boundary/Support denied assertions.
- W6-THUY-D4-05 [DOC] [S] [PLANNED] Ghi currency/timezone/formula trong API/UI/README.
**Files / Modules:** financial query/service/controller/tests, `reports.js`, `dashboard.js`, docs.
**End-of-Day Outcome:** budget/cost report có breakdown và field scope.
**Verification Plan:** subtotal=total, no duplicate asset/event, 403 Support, browser currency label.
**Dependency:** maintenance actual_cost + replacement current row. **Fallback Task:** build cost report từng nguồn độc lập, không đưa budget fake.
**Reviewer:** Thiện.

### THIỆN
**Objective:** đối soát tài chính độc lập.
**Task List (theo thứ tự):**
- W6-THIEN-D4-01 [TEST] [M] [PLANNED] Tính/chạy known-fixture maintenance cost và replacement budget có 2 rule/1 Asset, null estimate/year.
- W6-THIEN-D4-02 [TEST] [M] [PLANNED] Kiểm financial 403/field omission, archived records, decimal/year boundary và UI labels.
- W6-THIEN-D4-03 [REVIEW] [S] [PLANNED] Review source-of-truth actual_cost, query filter/year semantics.
**Files / Modules:** financial integration tests, expected totals, UI review.
**End-of-Day Outcome:** independent reconciliation không double count.
**Verification Plan:** SQL fixture vs report JSON/Excel future contract, browser Support no cost.
**Dependency:** EP-082/085/086 Thủy. **Fallback Task:** complete expected totals/tests skeleton and review query plan.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze report query contract để Week 7 export reuse, `actual_cost` snapshot rule, timezone/currency; Thiện không sửa financial service shared.

## Thứ Sáu — 30/10/2026

**Mục tiêu chung:** đo hiệu năng và index/query review có số liệu.

### THỦY
**Objective:** tối ưu dựa trên evidence, không suy đoán.
**Task List (theo thứ tự):**
- W6-THUY-D5-01 [PERF] [M] [PLANNED] Đo p95/query count/dashboard/report trên dataset/load/environment đã ghi, lưu raw results.
- W6-THUY-D5-02 [DATA] [M] [PLANNED] Inspect SQL execution plans, chọn projection/index change có rationale; tạo migration duy nhất nếu cần.
- W6-THUY-D5-03 [TEST] [M] [PLANNED] Re-run accuracy/permission/performance before/after và migration clean nếu đổi index.
- W6-THUY-D5-04 [DOC] [S] [PLANNED] Ghi target đạt/chưa đạt và dataset vào docs/status, không khái quát hóa.
- W6-THUY-D5-05 [REVIEW] [S] [PLANNED] Review report query/export handoff với Thiện.
**Files / Modules:** Reporting queries, optional migration, perf evidence, DB design/status.
**End-of-Day Outcome:** measured baseline/changes, export query frozen.
**Verification Plan:** same dataset/load before/after, actual execution plan, accuracy suite.
**Dependency:** D2–D4 report endpoints. **Fallback Task:** ghi baseline + known issue nếu tối ưu chưa đủ an toàn.
**Reviewer:** Thiện.

### THIỆN
**Objective:** xác nhận benchmark/reconciliation.
**Task List (theo thứ tự):**
- W6-THIEN-D5-01 [PERF] [M] [PLANNED] Tạo/chạy workload repeatable, ghi row count/filter/concurrency/tool/config cùng raw timings.
- W6-THIEN-D5-02 [TEST] [M] [PLANNED] So result checksum/totals trước-sau index/query change, kiểm không đổi permission/field.
- W6-THIEN-D5-03 [REVIEW] [S] [PLANNED] Review index write/storage trade-off và export query contract.
**Files / Modules:** perf fixture/commands/results, report tests, query review.
**End-of-Day Outcome:** measurement có thể tái lập và không sai số liệu.
**Verification Plan:** repeat run, known totals, no N+1/full materialization.
**Dependency:** report query Thủy. **Fallback Task:** baseline workload/reconciliation trước optimization.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** chốt API filter/scope/projection cho export Week 7; chỉ Thủy tạo index migration, Thiện review SQL/perf; merge sau accuracy tests.

## Thứ Bảy — 31/10/2026

**Mục tiêu chung:** M4 integration checkpoint và handoff Export.

### THỦY
**Objective:** đóng gate Dashboard/Report/Budget.
**Task List (theo thứ tự):**
- W6-THUY-D6-01 [INTEGRATION] [M] [PLANNED] Merge PR reviewed, Release build + clean migration + Week 3–6 full unit/integration suite.
- W6-THUY-D6-02 [TEST] [M] [PLANNED] Swagger/Postman/UI smoke dashboard/reports/budget, fixture accuracy, financial 403 và DB integrity.
- W6-THUY-D6-03 [FIX] [M] [PLANNED] Fix blocker, rerun affected/full tests, verify measured performance notes.
- W6-THUY-D6-04 [DOC] [S] [PLANNED] Cập nhật README/PROJECT_STATUS/CHANGELOG/API/DB/testing theo actual.
- W6-THUY-D6-05 [AUDIT] [S] [PLANNED] Git diff/secret/evidence checkpoint, báo issue/owner.
**Files / Modules:** all solution/UI/DB/tests, docs/tracking.
**End-of-Day Outcome:** M4 pass hoặc gap/owner minh bạch, export-ready query.
**Verification Plan:** clean migration, Release build/full tests, UI 320px, totals/403, perf dataset documented.
**Dependency:** D5 report query review. **Fallback Task:** cô lập report fail, không khởi Import/Export trên query sai.
**Reviewer:** Thiện.

### THIỆN
**Objective:** independent report/UI/performance review.
**Task List (theo thứ tự):**
- W6-THIEN-D6-01 [TEST] [M] [PLANNED] Chạy accuracy/financial field/expiry/history regression trên known fixture.
- W6-THIEN-D6-02 [UI] [M] [PLANNED] Smoke Dashboard/Reports charts/tables/mobile/empty/error, sửa chart helper module.
- W6-THIEN-D6-03 [REVIEW] [S] [PLANNED] Review performance evidence và export handoff filters/scope.
**Files / Modules:** fixture/tests/chart helper/M4 evidence.
**End-of-Day Outcome:** independent gate evidence.
**Verification Plan:** browser + xUnit + SQL expected totals and perf record.
**Dependency:** merged build Thủy. **Fallback Task:** isolate report discrepancies trước Week 7.
**Reviewer:** Thủy.

**Cuối ngày — Sync / Integration:** freeze M4 report query and permissions for Excel export; Git checkpoint/docs, no new module on Saturday; migration/client/layout remain Thủy-owned.
