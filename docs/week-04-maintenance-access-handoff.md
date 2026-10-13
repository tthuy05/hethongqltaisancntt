# Tuần 4 — scope và chi phí Maintenance dùng chung

Ngày 12/10/2026. Thủy: **W4-THUY-D4-02 — helper IMPLEMENTED LOCALLY / UNDER REVIEW**; **D4-03 — PARTIAL, policy harness**, chưa nghiệm thu endpoint. Không có thay đổi shared DB/Render hoặc API mới.

## 1. Source chuẩn bị, chưa kích hoạt

- `MaintenanceAccessPolicy` dùng `CurrentUser` đã được backend xác thực và refresh permissions/roles từ DB cho request hiện tại. Không truyền body người dùng hoặc role trong JWT chưa refresh vào constructor; không cache/singleton policy qua nhiều request.
- Dùng lại tên `MaintenanceRead`/`MaintenanceCreate` từ proposal `5dbef3a` của Thiện; bổ sung các constants đúng API spec: update, assign, work, resolve, cancel, cost.write. Không nhận `maintenance.complete` của scaffold vì khác EP-042/043.
- Không đưa constants vào `Permissions.All/Read/Operate/Admin`, seed hay authorization registration. Catalog active vẫn 28 quyền. Không hiển thị checkbox/menu Maintenance chưa hoạt động.
- Không sửa Controller, DTO hoặc Service Maintenance của Thiện; helper chưa nối vào endpoint production. Assignment rollout vẫn nguyên trạng.

## 2. Quy tắc truy cập

| Trường hợp | Hành vi helper |
|---|---|
| Admin/Manager có maintenance.read và đúng quyền command | Phạm vi toàn ticket; vẫn phải kiểm business state/rowVersion ở service |
| Có role Admin/Manager nhưng thiếu quyền command/read | 403, không bypass bằng role |
| Support hoặc nhóm tùy chỉnh không có role global | Chỉ ticket assigned_to_user_id là chính user; vẫn cần permission |
| Cùng department, là reporter, hoặc biết ID nhưng ticket giao người khác | Không tự có quyền; 403 |
| Ticket chưa có technician | Support không tự nhìn/nhận; queue policy chưa chốt nên fail-closed |
| Assign/reassign technician | Global role + maintenance.read + maintenance.assign; kiểm technician/state thuộc service |
| Cost | Global role + maintenance.read + maintenance.cost.write; một cost grant không tự mở global scope |

Đây là bản helper bảo thủ để review: phạm vi Maintenance của nhóm tùy chỉnh và queue còn **PLANNED** trong matrix ngày 11/10. Không tự cấp quyền rộng vì mới có nhóm động. Tài khoản có nhiều role tuân theo union hiện tại: có Admin/Manager hợp lệ thì được xét global scope, nhưng vẫn cần từng permission. Trước rollout cần Thiện/owner review mapping scope của nhóm tùy chỉnh; không biến quy tắc tạm thời thành quyết định cấp quyền production.

`ScopeRead(IQueryable<MaintenanceTicket>)` phải gọi **trước Count/paging/materialization** để không lộ total/count của ticket ngoài scope. `Require(ticket, action)` áp dụng cùng object-scope cho read/update/work/resolve/cancel. `RequireCreate()` kiểm read/create; asset visibility/eligibility kiểm riêng. Không dùng permission update thay cho resolve/cancel.

Queue được phép/self-claim chưa có dữ liệu hoặc policy rõ: helper chưa cho Support gọi assign, kể cả tự assign. Cần review policy queue trước khi mở khả năng này; không nhận `isAllowedQueue=true` từ client hoặc coi mọi ticket unassigned là public queue.

## 3. Field cost: kiểm sự hiện diện, không chỉ giá trị

`RequireCostWrite(JsonElement body)` kiểm các field top-level `cost`, `estimatedCost`, `actualCost`, không phân biệt hoa thường. Nếu không được quyền, sự hiện diện dưới mọi dạng (null, 0, số, chuỗi sai type hoặc field lặp) đều 403; không im lặng bỏ qua. Body không phải object trả validation error.

Helper không thay thế DTO validator: unknown properties, duplicate-property policy, type, precision/scale, nonnegative và state rules vẫn cần validation ở API/service. Phải gọi trước khi binding nullable decimal làm mất phân biệt **absent** với **explicit null**; DTO thực tế của Thiện có thể chọn giữ raw JSON hoặc setter presence flag tương đương nhưng phải kiểm lại bằng endpoint tests.

`ReadCosts(ticket)` kiểm scope trước, trả null khi không có quyền; caller phải **bỏ các field tài chính khỏi response**, không trả entity MaintenanceTicket trực tiếp, không serialize cost=null như một thay thế cho field filtering. Chỉ dùng `MaintenanceCostView` phía service nếu có quyền. Không log request body hoặc gửi email/export chi phí ngoài quyền. Chưa có DTO production integration/response-redaction E2E.

AuditWriter hiện tại có allowlist, loại cost/estimatedCost/actualCost/resolution tự do; test đã xác nhận chỉ giữ assetId/reasonProvided/correlation ở payload kiểm thử. Các events/history Maintenance đầy đủ chưa triển khai: không mở rộng allowlist để serialize toàn ticket. Cost-history requirement phải được Thiện/Thủy thiết kế snapshot an toàn trước rollout.

**Addendum audit 12/10:** đã bổ sung [shared audit helper](week-04-maintenance-audit-handoff.md), metadata status/technician/event và cost-change flags; số tiền/free text vẫn bị loại. Endpoint và cost-history response filtering vẫn cần Thiện tích hợp.

## 4. Trình tự tích hợp cho Thiện

1. Endpoint có authentication/permission tương ứng và feature/readiness gate được review; helper không thay JWT middleware.
2. Dùng CurrentUser đã refresh ở server; kiểm quyền và cost-presence.
3. Read/list qua scope trước paging/count. Với command, lấy asset lock trong audited unit of work, đọc lại ticket rồi kiểm scope **lại dưới lock**, version, state, technician eligibility. Tránh TOCTOU khi ticket đổi người phụ trách.
4. Dùng [shared status contract](week-04-shared-status-handoff.md) và ghi ticket/asset/history/audit trong cùng transaction.
5. Response projection không cost với scoped user; kiểm 401/403/404/409, race và rollback trên isolated DB.

Không trả success DTO rỗng từ scaffold, không bật Controller chỉ vì build PASS. Không seed Maintenance permissions vào shared khi module chưa được duyệt.

## 5. Verification

- Release build: **0 warnings / 0 errors**.
- Focused: **21 unit +9 HTTP policy-harness PASS**, 0 FAIL/SKIP. Matrix scope/permission, default-deny queue, presence-null/zero/casing, revoked grant, unknown action/context, PostgreSQL SQL translation và audit allowlist.
- Harness dùng TestServer + helper thật + `Errors.WriteAsync` thật; identity/ticket giả lập, không DB. **Không phải production Maintenance API, không phải JWT E2E**.
- PostgreSQL translation dùng `ToQueryString`, không mở kết nối; audit test chỉ ChangeTracker, không SaveChanges. Không gửi email thật, không ghi Neon và không chạy lại migration/restore.
- Regression offline: **476 unit +157 integration PASS /0 FAIL;117 cloud/opt-in SKIP**. Trong đó đã bao gồm 30 focused cases mới, không cộng lần hai. TRX: `maintenance-policy-regression_net10.0_20261012230040.trx` và `maintenance-policy-regression_net10.0_20261012230042.trx`. Catalog 28 quyền và các regression rollout/auth/audit hiện có vẫn PASS.

```powershell
$env:ITAM_RUN_NEON_TESTS='0'
$env:ITAM_FULL_SCHEMA_TESTS='0'
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test ItAssetManagement.slnx -c Release --no-build --no-restore --filter 'FullyQualifiedName~MaintenanceAccessPolicyTests|FullyQualifiedName~MaintenancePolicyHarnessTests' --logger 'trx;LogFilePrefix=maintenance-policy-focused' --results-directory test-results/week4-maintenance-policy
dotnet test ItAssetManagement.slnx -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=maintenance-policy-regression' --results-directory test-results/week4-maintenance-policy --verbosity quiet
```

TRX nằm trong thư mục ignored `test-results/week4-maintenance-policy/`; không đưa test artifacts vào Git.

## 6. Trạng thái bàn giao

D4-02 helper và fallback test đã chuẩn bị, **UNDER REVIEW / NOT WIRED / NOT DEPLOYED**. D4-03 mới chứng minh policy/harness và audit filtering; endpoint 403, DTO omission, actual Maintenance workflow atomicity/concurrency còn PLANNED sau tích hợp Thiện. D4-04 không mở menu tới endpoint chưa có.

WIP shared-status trước đó và ba file báo cáo của người dùng giữ nguyên. Chưa commit/push; cấu hình connection, migrations và Render không đổi. CI PASS của checkpoint `5cbedf1` không thay thế kết quả source WIP mới này.
