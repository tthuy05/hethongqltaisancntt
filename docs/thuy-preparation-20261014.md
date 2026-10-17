# Thủy — tiến độ chuẩn bị 14/10/2026

Tiếp nối công việc hiện có, không làm lại Week2/Schema18. Người dùng chọn chờ Thiện và tiếp tục chuẩn bị/kiểm thử phần Thủy; chưa cấp quyền main merge/production rollout/module activation mới.

## Source/evidence đã chuẩn bị

| Task | Kết quả của Thủy | Giới hạn còn lại |
|---|---|---|
| W4 shared status/cost/audit, D4-05/D5-05 review | Helpers, isolated cross-module proof, [owner review](week-04-owner-integration-review.md) | Thiện MaintenanceService vẫn TODO; corrections/owner acceptance PENDING |
| W4 D4-04/D5-03 shared UI | Assignment current-route client/lookup và [permission hints](week-04-05-ui-permissions-handoff.md) | Actual page/menu/readiness integration chưa mở |
| W5 D1-01/03, D2-01 | [AES-GCM/key ring/access policy](week-05-license-security-handoff.md) | Secret source/escrow/rotation và DTO semantics chưa chốt |
| W5 D2-02/03 | [Audited reveal service/no-store adapter](week-05-license-reveal-handoff.md), 1 real PostgreSQL rollback PASS | No endpoint/DI/real key/production persistence; không gọi HTTP reveal E2E |
| W5 D2-04 | Shared key/cost flags, immutable/default-deny | Không secret state, chưa reveal UX hoặc module activation |
| W5 D3-01/02 fallback | [Capacity protocol review và2 actual PostgreSQL tests](week-05-license-capacity-review.md) | Chưa allocation service/HTTP/type/expiry/permissions/lock-order stress acceptance |
| W5 D4-01/03 | [Immutable lifecycle facts/query](week-05-lifecycle-metrics-handoff.md), 11 unit/2 PG cases | Count/asOf semantics/thresholds còn OPEN; không viết evaluator Thiện |
| W5 D4-02 | [Admin-only rule configuration policy](week-05-rule-policy-handoff.md), 7 unit | Owner RuleService/state/version/projection/evaluation chưa có |
| Shared audit/session corrections | Dynamic RBAC/invitation audit reader; [safe login/me session contract](frontend-session-contract-handoff.md) | Source branch only, production chưa rollout |

Không đóng M2/M3 hoặc biến original daily PLANNED rows thành blanket DONE. Tests/helper/fixture protocol không thay actual module acceptance.

## Checkpoints / kiểm thử

Branch `codex/lecturer-feedback`, PR#1 DRAFT; main vẫn `1bc2767`. Các checkpoint đã push theo phạm vi: c40db25 (Week4 foundation), 53b6056 (key primitives), 6693df4 (metrics), 23d4db8 (reveal/audit), 2fe5b49 (UI hints/owner review), e4efddd (session/capacity). CI từng checkpoint đã SUCCESS, kết quả mới nhất phải đọc đúng head, không dùng CI cũ xác nhận source mới.

Current local regression: **576 unit +158 offline integration PASS /0 FAIL**, **130 cloud SKIP**; frontend **130 PASS /0 FAIL/SKIP**. New PG protocol runs riêng: reveal rollback1 PASS và capacity2 PASS; không cộng nhiều lần rerun hoặc coi offline SKIP là verified cloud. Release0warnings/errors; 33 syntax/37 boundary checks PASS. TRX/checkpoint details trong handoffs.

## Điều kiện trước phần tích hợp thật

1. Thiện hoàn thiện Maintenance/Assignment còn thiếu, Software/License metadata/allocation và RuleService/DTO theo schedule; Thủy nối/review shared API client/UI/security/transactions sau đó. Kế hoạch có Thủy License UI/integration và Thiện review, không coi toàn bộ frontend thuộc một người để tự đổi phân công.
2. Owner chốt encryption key source/escrow/recovery và null/format contract; không bịa key production hoặc tái dùng JWT/Neon password.
3. Owner chốt rule count/asOf/threshold/currency/estimated cost trước evaluator/seed. Không thay raw facts thành giá trị đoán.
4. Production lecturer-feedback rollout cần approval riêng và fresh writer/window/backup/restore/baseline gate. Không dùng approval B/S18 cũ hoặc generic continue để apply migration/main deploy.

Three report WIP của người dùng được giữ ngoài commit; config/connection string, old migrations/InitialM1, shared Neon và Render không sửa. Không gửi email thật, không bật Assignment/Maintenance/License API.
