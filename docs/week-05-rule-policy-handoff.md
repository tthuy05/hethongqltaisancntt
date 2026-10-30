# Quy tắc thay thế — policy, API và bàn giao



## 30/10/2026 — Replacement end-to-end, UNDER REVIEW

Cập nhật hiện tại thay thế các giới hạn read-only/commands-only ở checkpoint29/10 bên dưới. Người dùng chốt30/10: severity cấu hình riêng từng rule; MaintenanceCount = RESOLVED + FAILED, bao gồm archived. Không tự suy đoán mức ưu tiên từ số condition.

### Chức năng và luồng

Trang `#/replacement/rules` quản lý rule/version/status; `#/replacement` chọn tối đa100 tài sản, đánh giá, xem căn cứ/lịch sử và lập kế hoạch/bỏ qua. UI → session-aware API adapter → service/pure evaluator → audited transaction PostgreSQL. Client production vẫn đóng, preview local bằng `?replacement=1`. Không fake data hoặc tự retire/return/close maintenance/chi tiền.

### API & DTO

EP-063–067 thêm severity bắt buộc LOW/MEDIUM/HIGH/CRITICAL khi create/revise, GET có nullable severity cho legacy. EP-068 GET `/replacement-recommendations`: page1/pageSize20≤100, assetId/departmentId, priority, status ACTIVE/PLANNED/DISMISSED/SUPERSEDED, isCurrent, generatedFrom inclusive/generatedTo exclusive UTC, sortBy recommendedAt/priority/plannedReplacementYear, direction asc/desc, tie id. Department là owning department hiện tại, không claim historical ownership.

EP-069 POST `/replacement-recommendations/evaluate` nhận `{assetIds?,asOfDate?}`: tối đa100ID dương/không trùng; nếu omit chỉ đánh giá toàn bộ khi≤100 nonarchived assets. Chỉ ngày UTC hiện tại vì facts bảo trì là hiện tại; ngày khác400. Trả evaluated/created/superseded và results theo asset gồm outcome, recommendationId, missing-data codes; không retry tự động vì mỗi lần khớp append-new. Tối đa1000active rules, vượt409 RULE_LIMIT_EXCEEDED. Thiếu rule severity/cặp warranty-failure không hợp lệ409 RULE_CONFIGURATION_INCOMPLETE trước commit toàn batch.

EP-070 GET `/{id}` trả snapshot chỉ khi có financial grant; ETag/token16byte, no-store. EP-071 PATCH `/{id}` nhận `{disposition:PLANNED|DISMISSED,plannedReplacementYear?,note,rowVersion}`. Note bắt buộc≤2000; năm2000–2100 cho PLANNED, không gửi năm khi DISMISSED. ACTIVE→PLANNED/DISMISSED; PLANNED hiện hành cho sửa năm/note, no-op409; không phục hồi history.409 concurrency/superseded;400 invalid query/shape;401/403auth;404missing;503not ready. Không DELETE. UI link tới Asset, hiển thị kết quả đếm và không có cost input khi thiếu quyền.

### Business rules và thuật toán

Mỗi rule dùng OR giữa age>=months, completed maintenance count>=threshold, known maintenance cost/purchase cost>=ratio, hoặc warrantyExpired AND failures>=threshold. Warranty hết ngày trước assessmentDate; missing/zero-price/incomplete actual costs không biến thành0. Một condition thiếu không loại bỏ condition tuổi đã đủ. Rule active, đúng AssetType/null-global và effectiveFrom≤now<effectiveTo. Retired không tạo đề xuất. Chọn severity cao nhất rồi rule priority nhỏ nhất rồi rule ID; snapshot ghi đủ mọi rule khớp/condition/threshold/facts, rule version và định nghĩa count. Score để null, không chế tạo điểm. Estimate chỉ từ rule thắng, null vẫn null; năm mặc định UTC hiện tại.

### Transaction, lịch sử và bảo mật

Coordinator replacement-rules → asset row locks tăng dần → fresh users-identity/effective grants → facts → supersede-old/save → append-new/save → audit/save → commit. Batch all-or-nothing; không SQL retry trong transaction đã lỗi. No-match hoặc retired đóng current cũ; insufficient-only giữ current cũ và audit. Evaluation lại supersede cả ACTIVE lẫn PLANNED, giữ note/year cũ; không ghi đè snapshot. Runtime EF guard bảo vệ identity/reason/cost/evidence rule/recommendation; chỉ state/disposition/year/metadata được cập nhật. Financial snapshot chỉ trả cho assets.cost.read hoặc reports.cost.read, người khác nhận reason chung và không có estimate/snapshot property. Evaluate cần assets.read + assets.cost.read + recommendation read/evaluate; patch cần assets.read + recommendation read/update và recheck financial grant nếu response có cost. Không union quyền theo role.

### Phụ thuộc, schema và rollout

Migration mới `20261030024812_AddReplacementRuleSeverity`: đúng1nullable varchar20 severity +1CHECK, không default/backfill, không đổi bảng/FK/index cũ. Legacy null phải revise để chọn severity; không tự activate. SQL Up giữ transaction; rehearsal lên database local mới từ5migration, giữ legacy row và verify6history/HasPendingModelChanges=false. Các DB rehearsal riêng được giữ, không overwrite/drop target cũ. Nullable-column tương thích source Render bfb3657; chưa apply shared.

Source catalog62:6replacement definitions, grants baseline dự kiến Admin6/Manager4, Support0. Narrow `DevelopmentSeed.RunReplacementPermissionsAsync` chỉ chạy khi operator gọi, lock và audit, additive/idempotent16→0, không startup hook. Chặn inactive definitions, Support grants/misgrant rule writes để review. Gate `Features:ReplacementRuleApi:Enabled` mặc địnhfalse bao cả hai Controller, READ ONLY readiness kiểm severity column, migration history và6definitions; chung ingress120/min/8concurrent. Client `replacementEnabled` mặc địnhfalse trên production. Reporting/License/Assignment/Maintenance flags giữ nguyên.

### Verification và trạng thái

Release build0warnings/errors; unit916 PASS/0FAIL/0SKIP, integration265 PASS/0FAIL/163cloudSKIP, Frontend444 PASS/0FAIL/0SKIP. Frontend check59JS/63CSP PASS; production package58local assets. Logs: test-results/replacement-rule/publication-release.log,publication-regression.log,publication-frontend.log,publication-csp.log,publication-frontend-build.log. Browser Admin local createRule → evaluate asset được chọn → plan2027 → reschedule2028 → reevaluate giữ2history PASS, không auto-retire. Ảnh ignored: test-results/replacement-rule/browser/history-verified.png,plan-verified.png. Unit/integration có kiểm giá ẩn, grant revocation, concurrency, rollback và narrow seed. Cloud SKIP không tính PASS; CI/Docker đúng commit vẫn cần chạy. Console browser local không có warn/error tại lần nghiệm thu cuối. Local readiness probe được thay bằng fixture vì Neon policy không chấp nhận localhost; PostgreSQL migration/model/transaction được kiểm thực tế riêng, không coi đây là production readiness PASS.

Read-only Neon30/10 lúc22:08 UTC+7:19business tables/44FK/85CHECK/5migrations,56permissions/108grants;0rules/0recommendations, chưa severity. Backup snapshot22:10:11 UTC+7 và restore drill PASS, đối chiếu hash từng bảng/columns/constraints/indexes/history. DumpSHA256 4AD5B6B3A73BD25B2DF255DD28338054D72C2C3B61EAC06F2BA6A0608217CAFE,135484bytes; local outsideGit Backups/replacement-20261030_151010_41e38ba3. pg_dump verify-full + channel binding, CA bundle từ trusted Windows Root stores; lần đầu dùng libpq system roots FAIL được giữ là FAIL. Logical recovery cần đối chiếu thay đổi sau snapshot; PITR UNVERIFIED. Shared writes=false.

Operator SQL được review ở scripts/deploy-replacement-severity.sql, cùng2DDL và history insert của EF Up, có transaction/advisory lock, history/table baseline guards, lock_timeout3s/statement_timeout30s/idle transaction timeout30s. Cần ACK GUC trên cùng connection sau phê duyệt, target neondb hoặc prefix isolated chính xác. Không tự áp dụng khi đã có history/column; guard reapply/missing approval phải từ chối, không retry mù. Rehearsal thực thi đúng script trên localDB mới giữ legacy row,6history/19tables/44FK/86CHECK và verify varchar20nullable, invalid severity bịCHECK23514. Đã kiểm protected Admin matrix vẫn hoạt động khi module đóng/chưa có grants; không ép thêm quyền module chưa seed.

Source đang chuẩn bị publication nhánh review; chưa merge/deploy, Neon chưa ghi. Render settings kiểm tra30/10: main, Live bfb3657, Auto-Deploy After CI Checks Pass. Không thay đổi cấu hình Render. Cần review PR/CI, backup+restore mới, xác nhận không có migration/seed/deploy hoặc chỉnh permission matrix song song, phê duyệt riêng migration+6definitions/10grants, rồi Save-only backend flag + publish client flag/merge sau CI, verify Live SHA/readiness/UI. Không merge với client mở khi schema chưa sẵn sàng. Nếu lỗi, đóng flag/rollback application; không Down/drop/reset database. PITR chưa xác minh không được coi recovery đã PASS.

### Giới hạn và tiếp quản

Không đánh giá lịch sử/tương lai, không schedule publication, không auto-procurement hoặc thu hồi tài sản. Forecast dùng owning department hiện tại và current recommendation. Sau nghiệm thu/rollout Replacement mới chuyển Reports/Dashboard V2 acceptance. Các checkpoint dưới giữ evidence cũ theo thời điểm, không phải trạng thái hiện tại.

## 29/10/2026 — Tạo, revision và trạng thái quy tắc (UNDER REVIEW)

Đây là cập nhật mới nhất, thay thế giới hạn read-only của checkpoint phía dưới. EP-063–067 có source/API local; evaluator, recommendations và giao diện vẫn PLANNED. Chưa push/merge/deploy, không đổi Neon shared.

### Chức năng và luồng

POST tạo họ quy tắc; PUT luôn tạo hàng mới cùng code, version tăng1, đóng effectiveTo và isActive của bản trước trong cùng transaction. Ngưỡng/tên/giá và ID của bản cũ không bị ghi đè; recommendation FK tới bản cũ không đổi. PATCH status chỉ cho phiên bản mới nhất chưa đóng effectiveTo; không thể kích hoạt lại bản đã supersede. Không có hard-delete. Runtime EF guard cấm sửa cấu hình/code/version trực tiếp, chỉ cho đổi trạng thái/kết thúc hiệu lực và metadata concurrency của hàng đã lưu.

### API và DTO

- POST `/api/v1/replacement-rules`:201+Location, `{name, assetTypeId?, minimumAgeMonths?, maximumMaintenanceCostRatio?, minimumMaintenanceCount?, requireWarrantyExpired, minimumFailureCount?, estimatedUnitCost?, priority, description?}`. Backend sinh code `RR-`+32hex ngẫu nhiên từ Guid; client không gửi code/version/effectiveFrom hoặc ID. Version bắt đầu1, hiệu lực UTC ngay khi publish; chưa hỗ trợ lên lịch.
- PUT `/{id}`: cùng cấu hình và rowVersion Base64 token16byte. Trả200 với **ID mới**, version kế tiếp; client phải điều hướng theo ID trả về. Chỉ từ bản mới nhất chưa có effectiveTo; phiên bản chạm int.MaxValue bị409. Một revision giữ giá cũ nếu JSON không có estimatedUnitCost; explicit null xóa giá khi có quyền.
- PATCH `/{id}/status`: `{isActive:true|false,rowVersion}`;200; thiếu trạng thái400, no-op409. Chuyển trạng thái không tạo revision vì không đổi thresholds.
-400 VALIDATION_ERROR,401 UNAUTHORIZED,403 FORBIDDEN,404 REPLACEMENT_RULE_NOT_FOUND,409 CONCURRENCY_CONFLICT/RULE_SUPERSEDED/RULE_REVISION_UNAVAILABLE/RULE_STATUS_UNCHANGED. JSON field ngoài DTO bị400. Các response no-store. Concurrency token trong body; GET detail trả ETag.

### Nghiệp vụ

Name bắt buộc≤250, mô tả≤2000; assetType mới phải active, reference inactive giữ nguyên được khi revise. Ít nhất một điều kiện: tuổi tháng>0, số lần bảo trì>0, cost ratio[0,1] scale4, hoặc cặp hết bảo hành AND failure count>0; cặp này phải đủ hai field. Priority≥0. Giá nullable numeric(18,2), không âm, tối đa9999999999999999.99, scale2. Không tự tạo threshold hoặc giá mặc định. Đánh giá điều kiện là phần tiếp theo, không được coi đã triển khai.

### Quyền và bảo mật

Source catalog59: thêm create/update vào nhóm Admin (read trước đó vào Admin/Manager); bootstrap chỉ môi trường test/dev và bảo toàn matrix có sẵn. Production vẫn56/108; không chạy seed shared. Lệnh ghi cần active ADMIN_IT membership **và** effective read+action grant hiện tại. Giữ EffectivePermissionQuery, không cộng quyền theo tên role. Server refresh identity/token/grants trong transaction sau khi lấy coordinator; quyền bị thu hồi lúc chờ lock phải403. Role matrix đánh dấu hai quyền ghi AdminOnly, chặn gán cho non-Admin; service vẫn chặn misgrant từ ngoài.

Explicit estimatedUnitCost (kể cả null) cần assets.cost.read hoặc reports.cost.read; recheck financial grants dưới identity lock, không đoán/so sánh giá ẩn. Omission giữ nguyên giá khi revise; response bỏ property nếu actor thiếu quyền. Audit chỉ liên kết rule/version/trạng thái/code, không log giá hoặc secrets. Immutable rows lưu các ngưỡng lịch sử.

### Phụ thuộc và transaction

Rule coordinator `replacement-rules` → users-identity → version/head/reference checks → đóng hàng cũ/save → insert/save → audit/save → commit qua AuditedUnitOfWork. Đóng slot unique trước insert, nhưng mọi Save còn trong cùng transaction; lỗi audit rollback cả hai hàng và log. Các evaluator sau này bắt buộc cùng coordinator để serialize với publish/status. Không tiếp tục SQL sau transaction bị PostgreSQL đánh lỗi. Mã họ quy tắc không dùng MAX/COUNT, không cần sequence/schema migration mới.

Gate ReplacementRuleApi mặc địnhfalse, readiness READ ONLY yêu cầu đủ3 permission definitions active, bảng rules/type; giới hạn connect/command3s và shared ingress120/min,8 concurrent. Không bật trên Render. Không có startup seed/migrate. Schema/index/constraint/migration giữ nguyên; test kiểm HasPendingModelChanges=false.

### Kiểm thử, giới hạn và tiếp quản

Focused local tests có JWT và PostgreSQL thật: create/revision/status, concurrency một winner, historical immutable guard, giá ẩn omission/explicit null, misgrant non-Admin, rollback audit và revoke khi chờ lock. Kết quả cuối: Release0warnings/errors;891unit PASS/0FAIL/0SKIP;260integration PASS/0FAIL/163cloudSKIP;436frontend PASS/0FAIL/0SKIP;57JavaScript/61CSP PASS. Focused31unit và11integration là subset, không cộng thêm vào tổng. Logs ignored: test-results/replacement-rule/commands-release-final.log,commands-regression-final.log,commands-frontend.log,commands-csp.log,commands-focused-final.log. HasPendingModelChanges=false đã kiểm trên isolated. Chưa nghiệm thu browser/UI, CI/Docker head mới hoặc production. Tiếp theo là evaluator deterministic và UI; trước rollout cần seed rehearsal/grants/backup review riêng, không bootstrap toàn bộ shared. Các ghi chú read-only bên dưới chỉ còn là lịch sử checkpoint.

Ngày 14/10/2026, W5-THUY-D4-02: **PARTIAL / UNDER REVIEW**.

ReplacementRuleAccessPolicy yêu cầu server-refreshed CurrentUser. Create/update (bao gồm publish/deactivate) cần active Admin membership và respective explicit grant, có read dependency. Role Admin không tự bypass grants; misgrant create/update cho Manager/Support/custom group vẫn403. Checkpoint14/10: các constants chỉ là contract; cập nhật29/10 bên dưới đưa riêng quyền read vào catalog và API đọc, create/update vẫn PLANNED.

RequireRead chỉ kiểm grant, không tự triển khai financial projection hoặc baseline grants. Module owner còn phải kiểm object/field policy, đặc biệt estimatedUnitCost và cost-derived evidence; không trả raw entity cho caller chỉ vì CanRead. Evaluate authorization/cost query đã chuẩn bị riêng trong [lifecycle facts contract](week-05-lifecycle-metrics-handoff.md); policy này không implement evaluate/threshold/rule algorithm.

Owner writes phải refresh identity/grants dưới coordinator lock, version check, versioned rule/history/audit trong transaction; helper snapshot không bảo vệ riêng against concurrent grant revocation. Không hard-code thresholds/price/currency hoặc seed rules.

Verification: **7 unit PASS /0 FAIL/SKIP**, `test-results/week5-rule-policy/rule-policy_net10.0_20261014192724.trx`; synthetic actors/grants. Không phải real HTTP403/DB-rule-state audit test. Owner RuleService/DTO chưa có để review deterministic ordering/projection; những phần đó và M3 acceptance vẫn PLANNED. Config/migrations/shared Neon/Render không đổi.

Full offline checkpoint: **576 unit +158 integration PASS /0 FAIL**, **130 cloud SKIP**, Release compilation thành công. TRX `rule-policy-checkpoint_net10.0_20261014192910.trx` / `...192914.trx` cùng folder. Focused7 là subset; 2 capacity protocol PostgreSQL PASS được lưu riêng, không biến cloud SKIP thành PASS. Frontend130 PASS của checkpoint e4efddd không chạy lại do policy này không sửa UI. Linux Docker/CI ở đúng head mới là gate publication riêng.


## 29/10/2026 — Danh sách/chi tiết quy tắc, UNDER REVIEW

### Chức năng và luồng xử lý

Slice chỉ đọc EP-063/065: Controller → ReplacementRuleQueryService → IRepository → bảng replacement_rules đã có. Không tạo rule, revise, publish/deactivate, evaluate, đề xuất hoặc auto-retire. Không thêm migration/index/sequence; không thay đổi dữ liệu shared Neon.

### API và DTO

- GET `/api/v1/replacement-rules`: page≥1, pageSize1–100, offset không tràn int; assetTypeId>0 (lọc đúng loại, không tự gộp rule toàn cục), status Active/Inactive, keyword≤200 tìm literal không phân biệt hoa thường trong code/name. Active phản ánh is_active; không suy diễn rule đang trong thời hạn hiệu lực.
- sortBy chỉ code/name/version/priority/createdAt; sortDirection asc/desc; mặc định priority asc, tie-break id cùng chiều. Không cho sort/filter theo giá ẩn. Phân trang/count trên cùng filter trước projection.
- GET `/api/v1/replacement-rules/{id}`: trả version, thresholds, thời hạn UTC, trạng thái, mô tả, rowVersion và ETag; rule inactive vẫn đọc để xem lịch sử. EstimatedUnitCost chỉ có khi được quyền xem giá; response không có property này khi bị redact. Cả hai response no-store.
- RFC7807:400 VALIDATION_ERROR,401 UNAUTHORIZED,403 FORBIDDEN,404 REPLACEMENT_RULE_NOT_FOUND,503 REPLACEMENT_RULE_NOT_READY. Query field lạ bị400. Các POST/PUT/PATCH chưa có action, fallback404; không nhận ghi rule.

### Nghiệp vụ, phân quyền và bảo mật

`replacement-rules.read` là quyền thứ57 của source catalog. Development bootstrap mới cấp Admin/Manager, không cấp Support; bootstrap giữ nguyên matrix đã tùy chỉnh khi chạy lại. Không chạy bootstrap trên shared. Backend dùng effective RBAC hiện hành, không union theo role. Giá estimatedUnitCost cần assets.cost.read hoặc reports.cost.read ngoài quyền read; không trả raw entity. Create/update vẫn chưa đăng ký policy/catalog. Quyền read không mở quyền thay đổi threshold.

### Phụ thuộc và rollout

`Features:ReplacementRuleApi:Enabled` mặc định false; không có config bật trong repository. Khi đóng:404 trước auth/DB và Controller không xuất hiện trong discovery. Khi bật:readiness READ ONLY, timeout connect/command3s, kiểm bảng rules/asset_types và permission active; không đạt503 Retry-After30. Dùng chung ingress120 requests/phút, tối đa8 readiness requests đồng thời với workflow, không ảnh hưởng health. Cần review grant/availability và readiness trước bất kỳ rollout riêng. Production vẫn56permissions/108grants, không bị thay bởi source catalog57.

### Trạng thái, giới hạn và tiếp quản

UNDER REVIEW trên `codex/replacement-rule-catalog`; chưa push/merge/deploy. Kiểm thử cuối: Release0warnings/errors;878 unit PASS/0FAIL/0SKIP;255 integration PASS/0FAIL/163cloudSKIP;436frontend PASS/0FAIL/0SKIP;57JavaScript/61CSP PASS. Integration có JWT và PostgreSQL local xác thực mật khẩu thực tế, kiểm SQL translation/filter, redacted JSON/ETag/no-store,403Support,400query,404missing/no-write và dữ liệu rule giữ nguyên. Readiness rollout/discovery/503/401 cùng ingress được kiểm qua test host với probe thay thế; kết nối readiness Neon thật, browser UI và Docker/CI head này NOT VERIFIED. Logs local ignored: test-results/replacement-rule/release-final.log,regression-final.log,frontend.log,csp.log. Các lần đầu FAIL vì PostgreSQL chưa chạy, fixture thiếu Manager role grant do generic seed bảo toàn matrix, kỳ vọng405 thay vì fallback404 và assertions catalog56 đã được giữ log; kết quả cuối là rerun sau sửa, không đổi FAIL cũ thành PASS. API đọc đã có, chưa có trang UI; EP-064/066/067 và EP-068–071 PLANNED. Không coi toàn bộ Replacement hoàn thành. Tiếp theo triển khai rule revision bảo toàn evidence, actor/grants refresh dưới lock, version/audit atomic, rồi evaluator và UI. Giữ production flag đóng trong toàn bộ giai đoạn này.
