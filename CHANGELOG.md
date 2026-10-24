# Changelog

## Unreleased — 24/10/2026 — Reports integration review

- Fixed: preserve RBAC v2 effective permissions without Admin report/Excel bonus; retain Excel and PR #4/#5 UI.
- Fixed: default Reporting closed; Dashboard permission denial returns403 rather than a SESSION_CHANGED render loop. No timeout increase or skipped frontend tests.
- Verified locally:797 unit/207 integration/384 frontend PASS;163 cloud tests SKIP; Release0 warnings/errors;50 JS/54 CSP checks. Added real-JWT custom Admin and all11 report queries on password-authenticated isolated PostgreSQL. No production migration/deploy.

## Unreleased — 24/10/2026 — Production rollout & Neon permissions sync

- Added: enabled `Features:ImportExportApi:Enabled` in `appsettings.json` for live production deployment.
- Added: synced 5 Excel permissions into Neon production database (`neondb`) with advisory lock and audit logging; granted to `ADMIN_IT` and `SYSTEM_MANAGER` (0 grants to `TECHNICAL_SUPPORT`).
- Added: backup snapshot of pre-sync permissions in `artifacts/neon-permissions-backup-before-sync.json`.
- Merged: PR #3 fast-forwarded and merged into `main`. Cleaned up temporary `takeover/excel-acceptance` branch.

## Unreleased — 24/10/2026 — Excel authorization & permission catalog sync

- Fixed: resolved 403 Forbidden error for valid accounts by integrating 5 Excel permissions (`import.assets.execute`, `export.assets.execute`, `export.maintenance.execute`, `export.assignments.execute`, `export.replacement.execute`) into `Permissions.ImportExport`, `Permissions.Operate`, and `Permissions.All` (catalog count: 33).
- Added: `PermissionContract.Action` ("Nhập Excel", "Xuất Excel") and prerequisite mapping in `RoleManagementService`; gated Excel catalog entries behind `Features:ImportExportApi:Enabled`.
- Added: additive role grants for `ADMIN_IT` and `SYSTEM_MANAGER` in `DevelopmentSeed`; scoped controller authorization policies on `ImportController` and `ExportController`.
- Verified: Release build 0 warnings/0 errors; 793 unit tests PASS; 202 offline integration tests PASS (165 cloud/local-opt-in skipped); 380 frontend tests PASS; 50 JS/54 CSP checks PASS; production frontend build 45 assets PASS.

## Unreleased — 24/10/2026 — Excel acceptance

- Fixed: offline sequence-test assertion now accepts the CI disabled opt-in value0; only value1 enables cloud tests. Reproduced failing CI case locally before fix; workflow and database guards unchanged.

- Fixed: bounded XLSX column widths after browser download/render exposed clipped headers; cell values/types, formula protection and cost masking unchanged.
- Verified: actual browser/synthetic-auth + isolated PostgreSQL flow; added real-JWT regression proving the catalog gate remains closed even with an isolated import grant. No permission activation, production config, migration or deployment changes.
- Local regression:787 unit/200 offline integration/4 PostgreSQL/380 frontend PASS;161 cloud SKIP. CI exact revision and catalog/grants/UI activation remain review gates. [Evidence](docs/week-07-import-export-spec.md).

## Unreleased — 24/10/2026 — Asset Excel UI

- Added: permission/opt-in guarded import preview/confirmation UI, blank XLSX template endpoint and filtered downloads using existing export APIs.
- Fixed: no blind import retry on unknown outcome; stale financial grants discard downloads; dashboard-only cost grant no longer exposes XLSX financial columns.
- Verified locally: 786 unit / 200 offline integration / 3 password-authenticated PostgreSQL / 380 frontend PASS; 161 cloud SKIP; Release 0 warnings/errors; 50 JS/54 CSP checks and production frontend build PASS. No production flags, migrations, shared database or Render changes. [Contract/handoff](docs/week-07-import-export-spec.md).

## Unreleased — 21/10/2026 — Maintenance integration fix

- Fixed: duplicate Maintenance permission declarations after local merge; shared DI/policies, scoped reads/cost presence, schema-compatible pending/resolved states and transactional history/audit.
- Added: default-closed Maintenance discovery/middleware and real PostgreSQL rollback/concurrent-resolve regression coverage. Existing owner Controller unchanged; remaining workflows/UI await Thiện.
- Verified:742 unit/197 offline integration/334 frontend PASS;150 cloud SKIP;12 focused isolated PostgreSQL PASS;Release0 warning/error. Publication scoped to review branch; no config/migration/shared database/main/deployment changes. CI requires the new checkpoint SHA. [Handoff](docs/maintenance-integration-fix-2026-10-21.md).

## Unreleased — 21/10/2026 — Reports UI của Thủy

- Added: independent warranty/license filter/paging, UTC/year validation, budget-year/group controls and read-only replacement report tab with cost/access guards.
- Fixed: stable recommendation report paging for identical timestamps; stale report controls no longer navigate after detach/session/grant changes.
- Verified locally:709 unit/192 offline integration/334 frontend PASS;146 cloud SKIP;synthetic desktop/mobile browser QA. No owner-module business/config/migration/shared database or deployment changes. [Handoff](docs/reports-ui-handoff-2026-10-21.md).

## Unreleased — 17/10/2026 — License/lifecycle preparation review

- Added: License capacity/alert helpers, batch lifecycle metrics, Replacement policy and typed audit metadata with unit tests; owner module integration remains PENDING.
- Fixed: foreign-license/future-effective transfer guards, negative seat counts, schema bounds, ACTIVE self-transition and tracked-object mutation before note validation.
- Verified: selected source in a separate checkout:643 unit/158 offline integration/283 frontend PASS;141 cloud SKIP;Release0 warning/error;32/36 published HTTP smoke PASS. Linux container and exact pushed revision await CI.
- Documented: [review scope and deferred WIP](docs/thuy-review-2026-10-17.md). Reporting/import/export and final-acceptance drafts are excluded; no config, migration, shared database or deployment changes.

## Unreleased — 20/10/2026 — Prepared Import/Reporting fixes

- Added after b8f241f: financial completeness counters for unknown purchase/maintenance costs, grouped SQL count/sum and YTD upper bound; prepared UI shows missing/total instead of treating unknown as known zero. Focused tests/evidence in the review addendum; flags/grants/deployment remain closed.

- Fixed: typed workbook/header validation, read-only preview, server-generated asset codes, transaction reference/duplicate validation, per-Asset audit coverage and rollback.
- Fixed: Controller query allowlists, pagination overflow/UTC/sort validation, database cost/budget aggregates, bounded/filter-aware export, bearer transport and stale-session response handling.
- Added: default-closed Reporting/ImportExport Controller gates; prepared frontend Reporting requires explicit opt-in. No module permission seed, migration, shared Neon or Render changes.
- Corrected: withdrawn unsupported Week 6–8 completion/performance/PITR/production acceptance claims. Actual evidence and remaining owner integration: [review](docs/import-reporting-review-2026-10-20.md).

## Unreleased — 16/10/2026 — License capacity, alert queries, batch lifecycle candidates & replacement policy

- Added: LicenseCapacityPolicy enforcing target XOR, seat capacity, quantity reduction floors, archive blockers, and transfer rules.
- Added: LicenseAlertsReader with expiring licenses query, utilization/capacity alerts, dashboard summary metrics, and least-privilege cost masking.
- Added: AssetLifecycleMetricsReader candidate queries for efficient batch lifecycle evaluation.
- Added: ReplacementRecommendationPolicy with recommendation field validation, evaluation snapshot JSON parsing, disposition state machine transitions, actor timestamps, and single-current invariant per asset.
- Fixed: Synchronized Audit snapshot sanitization in Persistence.cs and AuditLogService.cs for Week 5 domain entities, masking purchaseCost and preserving maintenance cost secrecy.
- Verified: 638 unit PASS (+55 cases), 158 offline integration PASS, 270 frontend PASS, 43 syntax / 47 boundary checks PASS; [handoff](docs/week-05-capacity-alert-query-handoff.md).

## Unreleased — 16/10/2026 — User write lifecycle

- Fixed: User create/profile/status/membership dialogs lock captured inputs, validate response identity and block uncertain replay while preserving self-change Login and known field validation.
- Added:5 write lifecycle cases,270frontend PASS; no backend/email/config/production changes.

## Unreleased — 16/10/2026 — User dialog access

- Fixed: User dialogs recheck actor/route/grants before reads and confirmation, suppress stale completions, and guard invitation resend single-flight without changing email delivery.
- Added:5 access regressions,265frontend PASS; no backend/config/shared/production changes.

## Unreleased — 16/10/2026 — Group editor lifecycle

- Fixed: create/rename group forms support Enter and guard captured commands, pending inputs, stale responses and uncertain outcomes; require whole-catalog reconciliation after unknown metadata writes.
- Added:5 editor regressions,260frontend PASS; no backend/grant/config/production changes.

## Unreleased — 16/10/2026 — Group status lifecycle

- Fixed: group status confirmation snapshots target/version, warns about unsaved matrix, locks pending commands and blocks uncertain replay; check returned state and matrix permission sets before claiming success.
- Added:3 status lifecycle cases,255frontend PASS; no backend/grant/production changes.

## Unreleased — 16/10/2026 — Permission matrix save

- Fixed: snapshot reviewed matrix commands, lock pending controls, reject stale/malformed success and block blind replay after unknown outcomes; preserve draft until explicit read refresh.
- Added:6 lifecycle cases,252frontend PASS. No backend/grant/config/production changes; [handoff](docs/permission-save-lifecycle-handoff.md).

## Unreleased — 16/10/2026 — Client error boundary

- Fixed: public activation401 cannot revoke an unrelated bearer session; malformed JSON error envelopes retain HTTP status with safe typed fallbacks instead of TypeError.
- Added:3 transport regressions,246frontend PASS; private401 invalidation and valid validation metadata preserved. No backend/config/production changes.

## Unreleased — 16/10/2026 — Pending Asset inputs

- Fixed: prevent editing captured Asset fields during save and restore original control states on completion, including detached forms.243 frontend PASS; no API/backend/production changes.

## Unreleased — 16/10/2026 — Asset list access

- Fixed: Asset readers can view results without unrelated catalog grants; optional lookup failures disable only their filters, preserve URL IDs and never hide session expiration.
- Added:7 access/lifecycle regression cases,242 frontend PASS; no backend/grant/production changes. [Handoff](docs/asset-list-access-handoff.md).
- Added: actual isolated HTTP/PostgreSQL verification for a single-grant Asset reader, including denied mutations and preserved data/history;1 new +3 affected financial cases PASS.

## Unreleased — 16/10/2026 — Archive lifecycle

- Fixed: Archive confirmation captures reviewed ID/version, rejects stale page/session actions and blocks blind replay after conflicts or uncertain outcomes; list confirmation reads single-flight safely.
- Fixed: reject noncanonical strong If-Match before DELETE.235 frontend PASS; [handoff](docs/asset-archive-lifecycle-handoff.md). No backend/schema/production changes.

## Unreleased — 16/10/2026 — Login lifecycle

- Fixed: stale login page responses cannot redirect after navigation or logout a newer session; readonly pending inputs, bounded password validation and password clearing on completion.
- Added:11 regression cases,226 frontend PASS; no backend/config/production changes. [Handoff](docs/login-lifecycle-handoff.md).

## Unreleased — 15/10/2026 — Activation client safety

- Fixed: strict activation DTO/canonical token validation, duplicate fragment token rejection, single-flight/readonly passwords and expired/unknown outcome handling without replay or stale page updates.
- Fixed: Asset save blocks replay after same-account SESSION_CHANGED.215 frontend PASS; [handoff](docs/activation-client-safety-handoff.md). No backend/email/schema/production changes.

## Unreleased — 15/10/2026 — Workflow guard verification

- Added:3 actual isolated PostgreSQL/Asset HTTP regression cases for open and terminal Maintenance fixture snapshots; no Maintenance API implementation/acceptance claim.
- Fixed: Asset API-mode list/detail labels unavailable current-assignment data as unknown, not unassigned;205 frontend PASS. [Evidence](docs/week-04-workflow-guard-verification.md).

## Unreleased — 15/10/2026 — Asset save lifecycle

- Fixed: stale Asset form responses cannot redirect/toast after navigation or account/permission changes; unknown write outcomes preserve draft and block blind resubmit, with explicit same-origin read reconciliation.
- Added:8 lifecycle regression cases,201 frontend PASS; no token persistence/cross-tab transfer, backend idempotency or production changes. [Handoff](docs/asset-save-lifecycle-handoff.md).

## Unreleased — 15/10/2026 — Asset financial field guard

- Fixed: metadata-only custom operators can update assets without reading/clearing hidden price; create/update reject explicit financial fields without cost grant, includingnull.
- Fixed: Asset form omits the financial input/property when unauthorized; cost-granted nullable PUT behavior retained. [Red/green tests and compatibility](docs/asset-cost-field-handoff.md), ADR-033. No schema/grant/production changes.

## Unreleased — 15/10/2026 — Asset retire UI

- Added: permission-aware Asset detail retirement confirmation using the existing Retired-only endpoint; required reason/version, single-flight and explicit read refresh after conflicts/unknown write outcomes.
- Fixed: stacked shared concurrency feedback on narrow dialogs.190 frontend PASS;2 isolated backend contract tests PASS. No backend/schema/module activation changes; [handoff](docs/asset-retire-ui-handoff.md).

## Unreleased — 15/10/2026 — Asset status history

- Added: actual Asset detail status-history tab consuming the existing paging-only endpoint, with UTC timestamps, current actor names, permission/session guards and honest loading/error/empty states.
- Changed: history read projection includes additive nullable `changedByDisplayName`; no schema, new permission or workflow activation.181 frontend and4 focused isolated PostgreSQL HTTP tests PASS; [handoff](docs/week-04-asset-status-history-handoff.md).

## Unreleased — 15/10/2026 — License async guards

- Fixed: prepared License pages reject stale-account/revoked-read responses, use session/query snapshots and preserve matching filter/paging context.167 frontend PASS; no module activation.

## Unreleased — 15/10/2026 — Allocation draft form

- Added: prepared allocation draft/form and separately opt-in License detail composition; no POST or runtime route activation.163 frontend PASS; [handoff](docs/week-05-allocation-draft-handoff.md).

## Unreleased — 15/10/2026 — License target và shared409 UX

- Added: prepared user/asset target lookup with XOR/stale-session guards, no allocation POST or module activation.
- Changed: Asset edit409 keeps draft and offers explicit discard/read refresh, never retries mutation. 154 frontend PASS; [evidence](docs/week-05-license-target-conflict-handoff.md).

## Unreleased — 14/10/2026 — License read UI preparation

- Added: prepared License read adapter/list/detail with strict masked metadata and unknown-capacity display; default-disabled components, no app route/menu.
- Fixed: prepared mobile empty state/message and nowrap license code/key. 142 frontend PASS; [browser QA/limits](docs/week-05-license-ui-handoff.md).

## Unreleased — 14/10/2026 — Replacement rule access prerequisites

- Added: prepared Admin-only rule configuration policy with explicit grants/read dependency, 7 unit PASS. [Bàn giao](docs/week-05-rule-policy-handoff.md); constants chưa active, API/projection/algorithm còn PLANNED.

## Unreleased — 14/10/2026 — Session response validation

- Fixed: reject malformed login/me role/permission collections; safe CurrentUser-only session and array-only permission hints. 130 frontend PASS; [verification](docs/frontend-session-contract-handoff.md).

## Unreleased — 14/10/2026 — License capacity protocol tests

- Added: capacity transaction review và 2 isolated PostgreSQL protocol tests cho contention/reread và full-seat transfer rollback/history. Không implement Allocation API; [bàn giao](docs/week-05-license-capacity-review.md).

## Unreleased — 14/10/2026 — Shared UI permission hints

- Added: immutable default-deny Maintenance scope/cost and Admin-only License key permission hints, 125 frontend PASS. [Bàn giao](docs/week-04-05-ui-permissions-handoff.md); chưa mở page/menu/module.

## Unreleased — 14/10/2026 — Audited license reveal prerequisites

- Added: prepared reveal service rechecking DB identity/grants under coordinator lock, audit-before-response and no-store adapter; no route/DI/module activation. [Evidence](docs/week-05-license-reveal-handoff.md).
- Fixed: audit reader preserves dynamic role/permission counts and typed invitation metadata, without exposing secrets or changing request validation.

## Unreleased — 14/10/2026 — Lifecycle input query

- Added: readonly immutable lifecycle facts/metrics query, age/warranty và cost completeness; actual ticket costs không cộng history snapshots, raw status counts không tự chọn rule semantics. [Handoff](docs/week-05-lifecycle-metrics-handoff.md); evaluator/API còn PLANNED.

## Unreleased — 14/10/2026 — License security prerequisites

- Added: versioned AES-256-GCM protector/external key-ring interface và Admin-only key field/reveal policy; 34 focused unit PASS. [Bàn giao](docs/week-05-license-security-handoff.md). Chưa DI, cấu hình secret thật, persist key, API hoặc migration.

## Unreleased — 14/10/2026 — API client Tuần 4

- Added: shared Assignment client list/detail/assign/return và user lookup, reuse auth/error/session transport; validation theo Controller hiện có. [Bàn giao và checks](docs/week-04-api-client-handoff.md). Chưa thêm page/menu, bật API hoặc deploy.

## Unreleased — 12/10/2026 — Shared status contract Tuần 4

- Added: isolated PostgreSQL cross-module scenarios cho AssignmentService/shared Maintenance status/audit protocol, bao gồm return trong lúc sửa, kết quả hỏng và rollback sau Save. [Verification](docs/week-04-cross-module-verification.md); Maintenance Service/API vẫn cần Thiện tích hợp.

- Added: shared Maintenance ticket/history audit helper, immutable before snapshot và kiểm actor/correlation; metadata reader/writer giữ status/technician/event/change flags, không chứa cost amounts/free text. [Bàn giao audit](docs/week-04-maintenance-audit-handoff.md); chưa nối endpoint hoặc deploy.

- Added: Maintenance scope/cost helper và policy harness cho phần Thủy D4-02/03; field-presence guard cả null/0, assigned-only scope và default-deny queue. Constants chưa vào active catalog/seed; [trạng thái tích hợp](docs/week-04-maintenance-access-handoff.md).

- Added: helper transition/history cho Assignment/Maintenance/retire theo BR-007/013, UTC/actor/correlation và matrix default-deny; contract transaction/audit bàn giao Thiện.
- Refactored: retire dùng helper chung, giữ nguyên API, quyền, version và active-workflow guard. Không mở Maintenance endpoint hoặc thay schema/config.
- Verification và giới hạn: [bàn giao Tuần 4](docs/week-04-shared-status-handoff.md). Source local chưa commit/push/deploy.

## Unreleased — 11/10/2026 — Sửa 6 nhận xét giảng viên

- Added: Admin quản lý nhóm phẳng/ma trận checkbox và gán nhóm khi mời tài khoản; invitation HTTPS email API/sandbox, token SHA-256/UTC 30 phút/một lần/resend; migration bổ sung users/sequence/trigger.
- Changed: mã tài sản tự sinh/bất biến, không đổi mã cũ; tên phòng ban; bỏ cột/dòng chi tiết/hai bộ lọc “Đối tượng” ở Nhật ký, giữ audit backend; menu không dẫn đến module chưa triển khai; bootstrap seed không tự khôi phục quyền đã thu hồi.
- Fixed: loại bỏ test live Brevo gửi link không có token lưu trong database và báo PASS khi không chạy; giữ provider mock và luồng activation kiểm thử trên database riêng.
- Verified/deployment: [báo cáo và test](docs/lecturer-feedback-remediation-report.md). Publication trên nhánh `codex/lecturer-feedback`; chưa merge/deploy hoặc apply shared, không sửa 3 migration cũ.

## 09/10/2026 — Chuẩn bị rollout Assignment an toàn

### Added

- Patch tương thích archive/retire độc lập, immutable artifact revision, rollout flagfalse và READ ONLY schema/permission readiness trước Assignment Controller. Không rewrite Controller Thiện, không auto migration/seed. [Phân chia commit A/B/C và gate triển khai](docs/assignment-safe-rollout.md).

### Verified

- WIP offline **408 PASS /0 FAIL /103 cloud SKIP**; focused 46 PASS gồm hai regression reload/disable, affected 11 PASS, published Production 32 smoke PASS kiểm lại. Giữ run 10 HTTP FAIL do config capture và rerun PASS; Docker Linux local NOT RUN. Dashboard Render thực tế On Commit/Live `9524a6c`; chưa deploy A hoặc apply/seed shared. Readiness-load hardening trước public activation vẫn PLANNED.

## 08/10/2026 — Chuẩn bị tích hợp Assignment/Maintenance của Thiện

### Added

- Mapping hai bảng thuộc baseline, migration `AddAssignmentMaintenance` riêng, Scoped DI AssignmentService, catalog/seed hẹp ba quyền Admin/Manager; isolated migration12/27 và44 setup checks,14 service +10 HTTP Assignment focused PASS. Đã review mới `5dbef3a` và tích hợp Assignment Controller của Thiện, không viết lại hoặc kích hoạt Maintenance TODO. [Báo cáo bàn giao](docs/assignment-maintenance-integration-handoff.md).
- Full .NET regression **295 unit +169 integration =464 PASS /0 FAIL /0 SKIP**, Release0 warnings/errors. Run đầu có1 FAIL ở assertion UserAccount warning cũ; chỉ cập nhật test theo schema12/BR-055, không đổi nghiệp vụ tài khoản; giữ TRX lỗi và rerun PASS.

### Fixed

- Assignment validation/permission, khóa parent Asset, concurrency, audit Asset cùng Assignment và guard lịch sử đóng; workflow query theo từng tài sản thay vì sự tồn tại bảng. Nghiệp vụ kiểm trên DB test12 bảng; query mới tương thích schema10 bảng của shared được kiểm read-only, không ghi asset public.

### Changed

- **Dừng trước apply/seed shared `neondb`** do nguy cơ archive/retire của backend Render cũ; shared setup mặc định bị chặn trước kết nối đến khi backend compatibility được xác minh. Shared vẫn10/19, InitialM1 và dữ liệu shared không đổi; riêng test DB12/27 đã apply/seed và retain fixture unique. Không triển khai MaintenanceService/frontend, không commit/push; đây không phải completion của toàn bộ Assignment/Maintenance contract.

## 08/10/2026 — Kiểm tra M1 trước demo

### Fixed

- Giao diện trở về Login khi phiên hết hạn/401, kể cả lỗi bị form hoặc detail callback xử lý; giữ đường dẫn cần quay lại. Request Login/Người dùng cũ không xóa phiên hoặc điều hướng lần đăng nhập mới.

### Added

- 29 regression tests cho adapter/app/Login/Users; tổng lượt này **328 PASS /0FAIL /78 cloud SKIP**,27Production HTTP smoke PASS. [Bằng chứng Render và checklist diễn tập Thủy/Thiện](docs/m1-rehearsal.md).

### Changed

- Ghi nhận URL Render đã chạy và Manager/Support browser reads thực tế; không lưu thay đổi nghiệp vụ public, không schema/migration hoặc sửa module của Thiện. Giữ M1 **10/10/2026**, review/rehearsal/Mentor acceptance PENDING và credential finding FAIL1/UNRESOLVED. Local Linux Docker smoke chưa chạy do engine không hoạt động; CI/deploy của commit mới cần xác minh riêng.

## 07/10/2026 — Chuẩn bị deploy demo trên Render

### Added

- Docker multi-stage UI/API .NET10 chạy non-root, Render Blueprint và GitHub Actions chờ build/test/package/container smoke; secrets chỉ inject runtime, không auto migration/seed.
- Bản frontend Production API-only và publish guard loại mock/Swagger/config Development; PORT/exact Render hostname/trusted forwarding support, targeted tests và [runbook](docs/render-deployment.md).

### Changed

- README/deployment/status phân biệt repo chuẩn bị deploy với URL public Live. Lệnh Development local, nghiệp vụ, phân công,18bảng/41quan hệ và InitialM1 giữ nguyên. Owner credential remediation/review/rehearsal/public checks PENDING; không dùng evidence320PASS cũ thay lượt verify mới.
- Cập nhật tài khoản và mật khẩu demo công khai cho cả 3 vai trò (Admin IT `AdminDemo123!`, System Manager `ManagerDemo1!`, Technical Support `SupportDemo1!`) trong README theo yêu cầu bàn giao nội bộ; đồng bộ mật khẩu trên database `neondb` dùng chung.

## 06/10/2026 — Chốt bàn giao Nhật ký thao tác

### Changed

- Chốt Audit-read phần Thủy và kiểm lại: Release0warnings/errors, **165 unit +98 integration +57 Node =320 PASS/0FAIL/0SKIP**,38preservation-source checks,26syntax/30boundary PASS. Shared HTTP role gates/health/Swagger/OpenAPI và browser list/detail/mobile kiểm lại; evidence06/10 lưu riêng, không ghi đè05/10/run lỗi. [Closeout](docs/audit-read-handoff.md#closeout--publication-addendum--06102026).
- README có thông tin Manager demo Development theo yêu cầu của người dùng; ADR-030 ghi rủi ro và việc đổi hash/TokenVersion có audit, login mới200 và mật khẩu/JWT cũ401. Admin/Support/private files không public, không thêm reset-password API hoặc seed tự reset.
- Đồng bộ hướng dẫn publication/bàn giao: yêu cầu push mới supersede checkpoint không commit05/10, giữ human review/M1 acceptance PENDING. No schema/migration/Thiện-owned module changes; owner-secret scanner **FAIL1/UNRESOLVED**, không production-ready claim. Ghi technical debt hiển thị tiền lớn bằng JS Number; chưa tự đổi API contract.

## 05/10/2026 — Nhật ký thao tác (phần Thủy)

### Added

- Scoped EP-093/094 Admin-only list/detail audit, strict UTC range/filter/paging, safe scalar/cost projection và một audit.view marker khi xem thành công; giao diện Nhật ký thao tác chỉ đọc dùng API thật. [Contract và kiểm chứng](docs/audit-read-handoff.md). **IMPLEMENTED / VERIFIED**: Release0 warnings/errors, **165 unit +98 integration +57 Node =320 PASS /0 FAIL /0 SKIP**,38 preservation-source checks,26 JS syntax/30 boundary checks PASS. Shared narrow seed added2 then0, catalog25 permissions; Admin200/Manager-Support403/anonymous401 và health/Swagger/OpenAPI200 PASS; browser desktop/mobile PASS. Full integration đầu97 PASS/1 FAIL, scoped assertion fix rồi rerun98 PASS; run lỗi giữ nguyên, không dùng242 PASS cũ chứng nhận phần mới.
- D3-04 redaction/correlation verification và phần tài liệu D3-05; Assignment hook và transfer-race review Thiện còn PLANNED/PENDING. Không workflow, schema/migration, export/write API hoặc sửa Department/Asset Type của Thiện.

### Changed

- Assertion legacy toàn-DB kiểm snapshot nay loại riêng raw hostile-reader fixtures `T*.audit.*`, đồng thời kiểm event ghi thật phải loại secret và giữ `IsActive`; không xóa/sửa audit rows hoặc nới redaction/writer policy. Run lỗi vẫn giữ trong artifacts, full rerun thực tế98 PASS.
- Ghi publication user-admin UI `main/cba71a6`; no-commit notes ngày04/10 là historical snapshot. Current Week indicator vào Week3 đúng05/10, giữ nguyên original task rows/dates/ownership,30-task Week2 evidence,18 tables/41 relationships và physical10 tables/19 FKs/InitialM1.
- M1 **10/10/2026** và independent review gates không đổi; owner-credential finding **UNRESOLVED**. Lượt Audit-read mới **UNCOMMITTED /UNPUSHED**, không stage/commit/push.

## 04/10/2026 — Giao diện quản trị người dùng

### Added

- Màn Người dùng dùng API thật: danh sách, tìm kiếm/lọc, phân trang, tạo/sửa/xem hồ sơ, trạng thái tài khoản và gán role bằng ID thật; xử lý validation, 401/403/409 và tự đăng nhập lại sau khi sửa tài khoản đang dùng.
- EP-010–012 danh mục role/quyền chỉ đọc và API account-state tối thiểu, không đổi UserDto cũ. Manager chỉ đọc ID/tên role; quyền chi tiết và quản trị tài khoản dành cho Admin.
- Seed catalog riêng `--seed-role-catalog`, không tạo/reset tài khoản, mật khẩu hoặc dữ liệu demo. [Contract và kiểm chứng](docs/user-admin-ui-handoff.md).
- Regression **107 unit +85 integration +50 Node =242 PASS /0 FAIL /0 SKIP**; Release 0 warnings/errors, 32 docs/schema/source checks và 24 JS syntax/28 frontend boundary checks PASS. Shared narrow seed adds5 rồi0; 3 users/3 memberships, 24 permissions/42 mapped grants. Browser list/validation/detail/role dialog/self profile no-op relogin và desktop/320px/768px smoke verified trên isolated target.

### Changed

- Giữ README tiếng Việt đang sửa, evidence gốc, ownership/task rows và M1 10/10/2026. Không schema/migration/role-definition write/password reset/Assignment/Maintenance; independent review PENDING, owner credential UNRESOLVED, chưa commit/push.

## 04/10/2026 — Xuất bản quản lý tài khoản

### Changed

- Người dùng yêu cầu commit/push User management part 1 +account control lên `main`, rồi dừng để tiếp tục ngày khác. Giữ evidence/handoff và pending review; các no-commit/UNCOMMITTED notes ngày 03/10 là implementation snapshots, kết quả publication theo Git log/remote. Không force-push, thêm feature, seed shared DB, schema hoặc migration.
- Pre-push recheck: Release build 0 warnings/errors; **92 unit +76 integration +40 Node =208 PASS /0 FAIL /0 SKIP**; 25 docs/schema/runtime checks, 23 JS syntax/27 source checks PASS; health/live, health/ready, Swagger/OpenAPI HTTP 200. Integration chạy trên DB kiểm thử riêng có sẵn, không reset shared DB; [publication evidence](docs/user-account-handoff.md#publication-addendum--04102026).
- Existing Development owner credential unchanged; exact-secret scan vẫn FAIL 1 finding cũ /UNRESOLVED, không phải security gate sạch.

## 03/10/2026 — Account control và role membership

### Added

- EP-008/009 Admin activate/disable/lock/unlock and fixed-role assignment/removal, last-Admin race protection, live transactional permission/token recheck, JWT revocation and audited rollback. [Contract and current results](docs/user-account-handoff.md).
- 12 new unit /14 focused API checks PASS. User-admin UI, role catalog APIs, password reset and workflow integration remain PLANNED.
- Full regression **92 unit +76 integration +40 Node =208 PASS /0 FAIL /0 SKIP**, Release 0 warnings/errors, 25 docs/schema/runtime-boundary checks PASS. Explicit shared catalog seed Added=4 (2 permissions +2 Admin links), repeat 0; 3 users/memberships unchanged. Safe shared smoke verifies 401/403/404 routes and health/Swagger/OpenAPI 200, not shared successful account mutations. Secret gate still FAIL 1 old finding.

### Changed

- Narrow AppDbContext runtime guard permits only explicitly approved audited UserRole link removal; no model/entity/schema/migration change or user/history deletion. Preservation scripts distinguish this authorized runtime change from immutable mappings/InitialM1; part 1's prior successful snapshot retained.
- Preserve existing 19-file part 1 WIP/evidence, original task rows and 10/10/2026. No commit/push; independent review and existing credential finding remain unresolved.

## 03/10/2026 — User management part 1

### Added

- Admin-only EP-003/005/006/007 list/create/get/update profiles, strict DTO/query validation, safe contact response, adaptive password hashing, active department references, identity uniqueness, concurrency and transactional audit.
- New users have no default role/login. Identity edits revoke old tokens; password/status/lock/role assignment and user-admin UI remain PLANNED. [Runtime contract and results](docs/user-management-handoff.md).
- 21 new unit +14 API cases; full regression **80 unit +62 integration +40 Node PASS /0 FAIL /0 SKIP**, Release 0 warnings/errors, 22 preservation/doc checks PASS. Shared catalog adds 3 permissions/3 Admin links, repeat adds 0; user count stays 3. Existing secret finding remains FAIL 1 /UNRESOLVED.

### Changed

- Add users.read/create/update to explicit Admin catalog only, no schema/migration change. Preserve EP-004 minimum lookup, Week 2 evidence, logical 18/41 and 10/10/2026; independent review/security gate PENDING. New work UNCOMMITTED / UNPUSHED after published main/4fc66bf.

## 03/10/2026 — Xuất bản follow-up đã kiểm thử

### Changed

- User requested commit/push of the existing 40-file Demo/Swagger/User Lookup/test/documentation changeset after tests pass. Pre-push recheck: Release build 0 warnings/errors, 59 unit +48 integration +40 Node PASS, 17 docs/schema checks PASS, live/ready/Swagger/OpenAPI HTTP 200. Target `main`, no force-push; see Git history/remote for actual publication commit.
- Preserve original phase/handoff/evidence snapshots and pending human/M1 gates. No new database/schema/migration/feature work; existing Development owner credential unchanged and secret finding still UNRESOLVED, not a clean security scan.

## 03/10/2026 — Prerequisite User Lookup cho cấp phát

### Added

- EP-004 `GET /api/v1/users/lookup`: active-only ID/displayName/departmentId, `users.lookup` policy, server-side display-name search, department filter, stable sorting and pagination; no contact/security fields.
- 15 unit + 9 API tests; full current regression **59 unit +48 integration +40 Node PASS**, no failure/skip with cloud opt-in. [Results and handoff](docs/user-lookup-handoff.md).

### Changed

- Register `users.lookup` in the existing permission catalog/read-role seed; explicit shared catalog update added 1 permission/3 role links, no new table, entity mapping, migration, user-admin or Assignment workflow. Scope approved separately after Week 2–3 completion; preserved old WIP/evidence, milestone and pending review gates. No commit/push in this task.

## 03/10/2026 — Hoàn thiện phần kỹ thuật Week 2–3

### Added

- Self-hosted Development Swagger UI 5.33.1, Bearer metadata, no persisted authorization/online validator/CDN; real health Try it out 200, Production UI disabled.
- Explicit audited/idempotent demo seed: 2 role accounts + 24 varied assets, private generated credentials outside Git, repeat adds zero and preserves old data. One separate UI-smoke asset created through real API.
- 14 master-service unit cases, 2 isolated seed tests, OpenAPI security host test and 2 Swagger frontend tests. Current 83 .NET + 40 Node tests PASS; exact evidence/limitations at [96-task completion matrix](docs/week-02-03-completion.md).

### Fixed

- Expired/missing UI session no longer incorrectly reports Authentication as unimplemented.

### Changed

- Current status/runbook and Week 3 addendum distinguish published M1 baseline `2c34671` on main from this uncommitted follow-up. Preserve Week 2 evidence, 18/41 design and InitialM1; no schema/new DB/Week 4 work.
- Independent review/joint rehearsal/formal 10/10/2026 acceptance PENDING. Existing owner credential security finding still unresolved; no new commit/push.

## 03/10/2026 — M1 backend and real UI integration

Publication addendum: sau bàn giao, người dùng yêu cầu commit/push changeset M1 và bổ sung merge vào `main` để Thiện làm tiếp/review. Publish implementation branch `codex/neon-connection-foundation` and fast-forward main when possible, no force-push. Earlier no-commit/UNCOMMITTED statements are implementation snapshots; inspect Git log/remote for publication outcome. No additional DB/feature work or fabricated independent approval. Real Development owner credential remains included by the user's explicit decision; secret finding unresolved.

### Added

- Auth/login/me, 15-minute JWT, DB-backed permission policies/account checks, generic credential errors, rate limiting/lockout and hashed idempotent development seed.
- Audited unit of work, safe audit snapshots, random 16-byte concurrency tokens/UTC stamps, master and Asset APIs with query/validation/unique-conflict/soft-archive/history protection; no new migration/table/database.
- Real HTTP/Neon tests on existing isolated DB, explicit opt-in and retained unique fixtures; manual shared-runtime/JS adapter/browser/restart verification. Actual results/files at [M1 handoff](docs/m1-backend-handoff.md).

### Changed

- Existing 8 screens now use same-origin M1 API by default; Development hosts built local assets. Explicit localhost mock remains, future workflows/history panels PLANNED. Preserve full 18-table/41-relationship design, 30 Week 2 tasks and earlier check evidence.
- ADR-023: user-directed owner connection in repository Development configuration; unresolved secret-publication/least-privilege risk recorded, not a clean security scan. No secrets printed in report/log; development login password remains outside repo.
- No startup migration/seed, no Week 4–7 implementation, no commit/push. Independent review/production readiness/M1 formal demo acceptance pending; 10/10/2026 unchanged.

Thay đổi quan trọng được nhóm theo tuần. Nhãn **DOCUMENTED** chỉ nói tài liệu đã được tạo/sửa; chức năng tương lai vẫn **PLANNED**. Tại audit ban đầu repository chưa có commit; xem Git history để biết trạng thái xuất bản mới nhất. Chưa có release ứng dụng.

## 02/10/2026 — Neon M1 physical foundation

### Added

- Authorized .NET 10 layered skeleton, Development health/OpenAPI, central verified package versions/local EF tool/6 package locks, masked setup-secret helper and read-only schema diagnostics.
- 10 M1 persistence shapes/mappings and `20261002151601_InitialM1`, applied first on new isolated validation database then shared `neondb` through direct TLS/change lock. Verified 10 tables/19 FKs/27 CHECKs/48 indexes and 0 business rows; EF history metadata not a business table.
- 25 xUnit tests PASS, 37 setup checkpoints PASS and preservation/secret-check scripts; [setup report](docs/neon-database-setup.md). Frontend regression 38/20/22 PASS. No Auth/JWT/CRUD/seed/runtime roles implemented.

### Changed

- ADR-022/current docs record separately authorized real M1 setup, preserve 30 Week 2 deliverables/evidence and full 18-table/41-relationship baseline. Full Week 3 approval/Thiện review/M1 integration still pending; no milestone change.
- Setup owner credential stays outside Git; HTTP DefaultConnection still unconfigured, business SaveChanges disabled. InitialM1 forward-only/custom indexes/triggers must be preserved in reviewed future migrations.
- UNCOMMITTED / UNPUSHED. Password shared in chat was not rotated per user instruction; recommendation/risk recorded without secret values.

## 02/10/2026 — Stitch Frontend Integration

Publication addendum: người dùng đã push frontend tại `cae3a31` (`push giao dien mock`). Lượt follow-up hoàn tất kiểm tra còn dở: build/38 tests/20 syntax/22 source checks PASS; menu điện thoại qua 4 destinations, ESC/focus và desktop navigation PASS trong phạm vi đã kiểm. Không sửa thêm runtime khi chưa tái hiện lỗi; bổ sung kết quả bàn giao/current status ở 4 tài liệu. Xem Git history/remote cho publication bổ sung. Các ghi chú no-commit/UNCOMMITTED dưới đây là snapshot lúc bàn giao trước yêu cầu publication, không cấm yêu cầu mới hoặc mở backend scope.

### Added

- **FRONTEND IMPLEMENTED WITH MOCK DATA:** một HTML/ES-module shell, Login, Dashboard, Asset List/Create/Edit/Detail, Department và Asset Type screens; reusable components, mock services/state, fail-closed future API adapter, local SVG icons/logo và Inter fonts. Không phải Auth/JWT/Asset API implementation.
- Node/Tailwind local build, GET/HEAD-only localhost preview, locked frontend dev dependencies, source checks và 38 Node tests. Build **PASS**, tests **38 PASS / 0 FAIL**, JS syntax **20 PASS**, source-boundary checks **22 PASS**. Browser mock flows/responsive 320/375/768/1280/1440px được kiểm; không xác nhận DB/API/WCAG đầy đủ.
- [Integration handoff](docs/stitch-ui-integration.md): audit ZIP/original preview, file manifest, screenshots, commands, limitations và next integration steps. Future module/history views ghi rõ **PLANNED**.

### Changed

- ADR-021 thay riêng Bootstrap/no-build choice của ADR-017 bằng HTML/Tailwind CSS 3.4.19 + self-hosted @fontsource/inter 5.3.0, giữ same-origin/hash/in-memory future auth và layered backend design.
- Cập nhật current frontend status/run instructions và các references styling còn hiệu lực; không sửa business requirements/API/database/ERD hoặc reset Week 2 evidence/task statuses. Previous **21 PASS** và Neon affected **24 PASS** vẫn là historical documentation evidence.
- **UNCOMMITTED / UNPUSHED:** không stage/commit/push; frontend-only task không tạo .NET skeleton/EF entities/migrations hoặc kết nối Neon. M1 **10/10/2026** vẫn PLANNED, chưa đạt.

## 01/10/2026 — Database Platform Decision Update

### Changed

- **DOCUMENTED — DESIGN ONLY:** planned database platform changed from SQL Server to PostgreSQL hosted on Neon before implementation/migrations (ADR-018); ORM remains Entity Framework Core, provider becomes `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Giữ Schema Baseline V1 **18 tables / 41 relationships**, 30 deliverable Thủy Week 2 và evidence/previous 21 PASS; handoff chỉ append addendum. Chuyển physical types/defaults, concurrency token, JSON/constraints/index/locking notes và shared-Neon secret/migration/test-isolation plans; không đổi API contract hoặc milestone **10/10/2026**.
- **PLANNED:** Neon setup; **NOT CONFIGURED / NOT VERIFIED:** Neon connection. Physical schema/migrations/business features chưa tạo; không code/stage/commit/push. Các mục SQL Server phía dưới là lịch sử, không phải database target hiện tại.

## 01/10/2026 — Bàn giao phần việc Tuần 2 của Thủy

- **DOCUMENTED — REVIEW PENDING:** đối chiếu 30 task W2-THUY với artifact/evidence và tạo `docs/week-02-thuy-handoff.md`; chưa có sign-off của Thiện/Mentor.
- **DOCUMENTED:** kiểm lại Git/.NET/SQL Server/Docker; HEAD và origin/main cùng baseline 0c99374, server collation Vietnamese_CI_AS; giữ audit ban đầu như lịch sử.
- **DOCUMENTED:** sửa Login/admin navigation thành một shell để giữ token in-memory; thống nhất stale write 409, archive precondition 428 và kiểm reload → re-login → persistence.
- **DOCUMENTED:** bổ sung UC-017/018 đầy đủ, M1 DTO/field/length/null/PUT/sort/error contract, assets.cost.read policy và readiness 10 bảng M1/minimal AuditWriter.
- **DOCUMENTED:** sửa fallback archive trái M1, tách quyết định schema theo tuần/module và đồng bộ README/status/ADR/consistency review.
- **PLANNED:** application code, Auth/JWT/Asset CRUD, migration và runtime tests; không stage/commit/push ở lượt bàn giao, dừng tại Week 2.

## 01/10/2026 — Replan hai thành viên, M1 có giao diện

- **DOCUMENTED:** audit lại repo/.NET; ghi nhận vẫn chưa có code, migration, UI, commit.
- **DOCUMENTED:** thêm UI/UX spec, team ownership, Git collaboration và task dependency/critical path.
- **DOCUMENTED:** viết lại roadmap/36 ngày Week 2–7 cho Thủy và Thiện; workload kế hoạch 60,9%/39,1%, task M/S, review/fallback/sync mỗi ngày.
- **DOCUMENTED:** đổi MVP từ backend-only sang UI + API + SQL Server demo ngày 10/10/2026; cập nhật requirements/scope/architecture/security/testing/README/status/ADR/consistency review.
- **PLANNED:** toàn bộ implementation, build/test/migration/UI demo và Week 3–7. Việc xuất bản tài liệu lên Git được người dùng yêu cầu riêng sau báo cáo; không phải phê duyệt bắt đầu code.

## Week 2 — Analysis & Technical Design (28–29/09/2026)

- **DOCUMENTED:** repository/environment audit; xác nhận repository rỗng, branch `main` chưa có commit, .NET SDK 10.0.400 và SQL Server 17 Developer cục bộ.
- **DOCUMENTED:** requirements, scope, actors, permission matrix, use cases, business rules và open questions.
- **DOCUMENTED:** thiết kế 18 entity SQL Server, Mermaid ERD, architecture, 94 endpoint REST **PLANNED**, security, audit log và testing strategy.
- **DOCUMENTED:** deployment design, risk register, roadmap Week 2–7 và daily plans Monday–Saturday cho sáu tuần.
- **DOCUMENTED:** ADR, README, project status và consistency review; sửa các mâu thuẫn về fixed roles, transfer, license seat/capacity, recommendation/budget, field permissions và lịch sử.
- **PLANNED:** implementation Week 3–7, migration, API runtime, build/test/benchmark và deployment. Chưa bắt đầu.

## Week 3 — M1 Identity, Asset Core & Web UI

- **PLANNED:** solution/Npgsql configuration/Neon PostgreSQL initial migration, authentication/JWT/RBAC, Department/Asset Type/Asset API và Login/Dashboard/Asset UI thật; M1 10/10/2026 sau review/approval.

## Week 4 — Assignment & Maintenance

- **PLANNED:** assignment/return/transfer, maintenance/history/audit và tests.

## Week 5 — Software, License & Lifecycle

- **PLANNED:** software/license allocation/key security, lifecycle rule/recommendation và tests.

## Week 6 — Dashboard, Reports & Budget

- **PLANNED:** dashboard/report/budget queries, authorization và performance review.

## Week 7 — Import/Export & Finalization

- **PLANNED:** Excel import/export, hardening, full regression, documentation và demo preparation.
