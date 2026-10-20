# Thủy — Import/Export và Reporting: checkpoint review 20/10/2026

## Addendum — Financial completeness sau publication b8f241f

Checkpoint b8f241f đã push nhánh review; [CI39404589440](https://github.com/tthuy05/hethongqltaisancntt/actions/runs/39404589440) SUCCESS gồm actual Linux container build/smoke. Chưa merge main/deploy. Bảng704/190/304 bên dưới giữ evidence của checkpoint đó, không dùng cho source addendum mới.

Đã hoàn thiện một phần gate4: Dashboard costs có AssetCount/MissingPurchaseCostCount và MaintenanceTicketCountYtd/MissingMaintenanceCostCountYtd; monthly costs và maintenance cost report có TicketCount/MissingCostCount ở tổng/group. SUM known values và COUNT/COUNT-null cùng SQL. Known zero khác null; tập rỗng0 count; YTD có upper bound now UTC. Report không mặc định YTD: phiếu chưa resolved vẫn được count ở nhóm “Chưa có ngày hoàn thành” nếu không có date filter. UI hiển thị missing/total và không dựng missing=0 khi response thiếu field.

4 test mới RED trước implementation → GREEN; final local **708 unit PASS/192 offline integration PASS/146 cloud SKIP/305 frontend PASS**; **5 focused isolated PostgreSQL PASS**; Release0 warning/error,47 syntax/51 boundary và production frontend42 assets PASS. Isolated query assertions so sánh null/zero counts với DB, gồm explicit unknown/zero Asset và tickets, subtotal/group counts khớp. Hai synthetic HTTP cases kiểm tên JSON counts0 ở empty dataset; một Node DOM case kiểm missing counters/response cũ. Không coi các case đó là production JWT/grant hoặc browser acceptance.

Raw evidence Git-ignored: `artifacts/verification/financial-completeness-20261020.trx`, `completeness-dotnet.log`, `completeness-frontend.log`. Target isolated/guards giống mục3. Không thêm query detail materialization, schema/migration/config/permission/rollout flag changes; không ghi shared Neon hoặc deploy Render. Gate snapshot consistency, browser/load/EXPLAIN, owner review và rollout vẫn mở. CI cho source addendum phải kiểm riêng sau publication, không tái dùng CI b8f241f.

**SOURCE PREPARED / UNDER REVIEW — NOT PRODUCTION ACCEPTED.** Tiếp tục WIP sau checkpoint `24fe193`, không làm lại thiết kế hoặc phần Thiện. Tài liệu này thay các kết luận hoàn thành/hiệu năng thiếu bằng chứng trong bản nháp Week 6–8; evidence Week 2 và các handoff đã publish vẫn giữ đúng thời điểm của chúng.

## 1. Phần đã xử lý

- Workbook parser: giới hạn file5MiB, ZIP256 entries/25MiB uncompressed, XML không DTD/external resolver, tối đa1000 data rows/32 columns. Kiểm header thiếu/trùng/lạ; typed date/price/ID không hợp lệ trả row errors, không âm thầm bỏ giá trị. Không evaluate formula; duplicate sheet relationship trả400 thay vì500. Excel1900 serial1/61 đúng; serial60 và date1904 không hỗ trợ.
- Preview import không UoW/SaveChanges/audit/sequence. Commit validate lại master/reference/duplicate trong audited transaction dùng cùng master-data lock, sinh AST qua generator hiện hữu, tạo history và per-Asset audit. Một lỗi validation không tạo Asset; persistence/audit failure rollback toàn batch. Không có tự retry ghi; sequence gaps được phép.
- Controller allowlists chấp nhận đúng query, chặn unknown/duplicate; UTC/offset/sort/status validation. Multipart cho phép5MiB file cộng64KiB envelope, không lưu file upload ra webroot.
- Dashboard/Reports cost và budget aggregate tại PostgreSQL bằng SUM/GROUP BY/COUNT, không materialize matching detail set để cộng trong RAM. Alert total tách khỏi preview50; grouping áp dụng cả department/type/status filters; paging tie-break bằng Id. License projection không lấy ciphertext/full key/allocation detail list; summary License null nếu thiếu licenses.read.
- Export chọn tối đa5001 dòng rồi báo413 khi vượt5000, writer cũng không truncate. Date/status/department/priority filters và stable ordering được áp dụng; chi phí mask theo grant; user label lookup chỉ project Id/FullName. Workbook formula neutralization, safe filename/MIME giữ nguyên; ký tự XML không hợp lệ trả422 thay vì phát tệp hỏng.
- Upload/download dùng token trong closure adapter, không lấy token từ CurrentUser/URL; no-store, credentials omit; kiểm actor/session revision/grant trước/sau async, current401 clear session, stale401 không xóa phiên mới. Kiểm file/response shape/MIME/size/filename, không retry ghi khi kết quả chưa rõ.
- Reporting UI default closed; không biến lỗi API thành zero/empty giả, không render response sau đổi actor/hash/quyền. Sửa typo quyền `reports.replacement.read`. Dashboard M1 vẫn chỉ dùng assets.read và endpoints Asset; không bị ép sang permission Reporting khi chưa mở module. Expiry preview ghi rõ10 dòng đầu không lọc thời hạn; chưa coi đây là đầy đủ UI acceptance.

## 2. Rollout và ownership

`Features:ReportingApi:Enabled` và `Features:ImportExportApi:Enabled` đều mặc định false (startup Controller discovery). Frontend cần `reportingEnabled:true` riêng; default entry không truyền opt-in. Reporting/Excel constants chưa nằm trong `Permissions.All`, catalog/grants chưa seed; không chỉ bật flag rồi suy API đã usable. Không thay config/connection string/migration/index/schema, shared Neon, Render, Assignment flag hoặc Controller/Service của Thiện.

Thủy chuẩn bị query/security/contract/helper và integration evidence. Thiện tiếp tục owner Assignment/Maintenance và Import page/owner review theo kế hoạch; không xem code query đọc bảng workflow là hoàn tất API nghiệp vụ của Thiện. Latest fetched `origin/feature/assignment-maintenance` vẫn `5dbef3a3fd9ef045eafc984b82fc00d8850b0576` ở lượt kiểm này; không pull/merge vào WIP.

## 3. Kiểm thử thực tế

| Lượt chạy local | Kết quả | Phạm vi / giới hạn |
|---|---|---|
| Release build | PASS,0 warnings/0 errors | Source local, không deploy |
| .NET unit | 704 PASS/0 FAIL | Import/parser/export/report và regression cũ |
| Integration offline | 190 PASS/146 cloud SKIP/0 FAIL | Bao gồm32 synthetic HTTP query/binding/default-closed cases; không phải production JWT/grant proof |
| Isolated PostgreSQL focused | 5 PASS/0 FAIL/0 SKIP | 3 import transaction/rollback/duplicate-race;2 reporting translation/count/nonzero-cost/budget comparisons |
| Frontend Node | 304 PASS/0 FAIL/0 SKIP | Synthetic transport/DOM/session/regression, không thay browser acceptance |
| Frontend checks | 47 syntax/51 boundary PASS | Source-level, không xác minh database/security tuyệt đối |
| Production frontend build | PASS,42 local assets | API-only, không mock/Swagger |
| Published package | PASS,72 files/42 public assets | Không Development config/mock/Swagger/database-secret markers trong artifact; không kiểm Git history |
| Published HTTP smoke | 32 maintenance OFF/36 ON PASS | Không query/seed/migrate DB; build revision ở lượt local là HEAD24fe193 với WIP, không phải published revision mới |
| Docker local | NOT RUN | Docker CLI có nhưng Linux daemon chưa chạy; cần CI Linux xác minh exact publication |

Cloud146 SKIP và focused5 PASS là hai lượt khác nhau: không gọi toàn bộ cloud suite PASS. Không quy đổi test count thành coverage percentage. Red/green trước đó:9 Import regressions,5 aggregate-materialization cases và2 real-adapter cases đã tái hiện lỗi trước khi sửa; full frontend còn phát hiện2 login-subtest failures (+parent failure) do Dashboard grant, sau sửa full304 PASS.

Lệnh chính:

```powershell
$env:ITAM_RUN_NEON_TESTS='0'
$env:ITAM_FULL_SCHEMA_TESTS='0'
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj -c Release --no-restore
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --no-restore
pnpm check
pnpm test
pnpm build:production
node scripts/verify-production-publish.mjs artifacts/production-publish
node scripts/smoke-production.mjs --publish artifacts/production-publish --expected-revision <build-sha>
node scripts/smoke-production.mjs --publish artifacts/production-publish --expected-revision <build-sha> --maintenance
```

Focused PostgreSQL chạy riêng với `ITAM_RUN_NEON_TESTS=1`, `ITAM_FULL_SCHEMA_TESTS=0`, target `it_asset_management_full_schema_verify_fb_20261011_b21b`, private config path ngoài command output và filter `FullyQualifiedName~DashboardQueryIntegrationTests|FullyQualifiedName~ImportTransactionIntegrationTests`. Fixture bắt buộc isolated target name và actual DB name; không trỏ shared neondb. Không cung cấp connection string/password trong báo cáo.

Raw local evidence (Git-ignored): `artifacts/verification/reporting-import-20261020-final.trx`, `frontend-20261020-final.log`, `offline-final.log`. Focused tests tạo synthetic fixtures trên DB test hiện hữu, không reset/drop/truncate, không thay database chung.

Diagnostic service-call timings (ms), isolated scoped129 Assets/26 tickets/2 recommendations, concurrency1, warmup1,10 samples:

```text
dashboard-costs: 187.54,186.06,191.05,187.73,195.39,191.41,191.56,184.88,188.07,188.95
maintenance-cost: 69.16,62.69,67.86,62.04,62.19,62.69,61.66,63.74,61.30,62.36
replacement-budget: 61.46,62.84,61.71,62.19,61.16,62.49,61.38,62.30,63.00,66.43
```

Bao gồm network/service overhead, không phải HTTP end-to-end/p95 production/EXPLAIN/load acceptance. Không có index migration hoặc claim index-only scan. Browserslist báo dữ liệu caniuse cũ; không tự cập nhật dependencies ngoài scope, production build vẫn PASS.

## 4. Các gate còn mở

1. Thiện review owner integration, master/reference/rollback behavior và export/report filter parity; chưa có xác nhận review độc lập.
2. Broad PostgreSQL known fixtures, real JWT/grant HTTP matrix, status/date/currency boundaries và workbook interoperability với Excel/LibreOffice.
3. UI filter/paging cho expiry, lựa chọn năm ngân sách, upload/download trên owner page và responsive/browser acceptance. Report replacement reader có contract nhưng chưa có tab riêng. Không coi shell/helper là UI hoàn thiện.
4. Financial completeness tại b8f241f chỉ có known subtotals; missing-price/ticket counts được bổ sung ở addendum đầu tài liệu, không phải production acceptance. Forecast/budget có missingEstimateCount. Nhiều query read-committed không phải cùng transaction snapshot; asOfUtc không khóa trạng thái xuyên request. Chốt UX/policy này trước nghiệm thu.
5. EXPLAIN/load/query-count measurement với workload thống nhất; chưa chứng minh SLA/performance production.
6. Lecturer-feedback migration/permission rollout/backup-writer coordination trên shared và production revision/smoke phải phê duyệt riêng. Giữ BLOCK MERGE/DEPLOY của PR, không tự mở modules hoặc gửi email thật. Connection string giữ nguyên theo owner; không tuyên bố security finding đã sạch.
7. Week8 final demo, PITR verification, email inbox E2E, Mentor acceptance/sign-off còn PLANNED/UNVERIFIED, không được ghi100%.

## 5. Git và bàn giao

Chỉ publication lên nhánh review `codex/lecturer-feedback` khi kiểm thử đạt; không main merge/Render deploy. Source/test/docs của checkpoint này không sửa migration/config; ba artifact báo cáo cũ (DOCX, Markdown kịch bản và script tạo DOCX) giữ nguyên ngoài commit. Logs/TRX ở Git-ignored verification folder. Không git add toàn bộ WIP, không reset/clean/restore.

Kết quả CI exact pushed revision phải xem riêng trên PR; bảng trên là evidence local, chưa tự suy CI/Docker PASS từ cấu hình workflow. Trạng thái UNDER REVIEW không tự chuyển thành DONE sau push.
