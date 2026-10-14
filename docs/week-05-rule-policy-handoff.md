# Thủy — rule configuration policy preparation

Ngày 14/10/2026, W5-THUY-D4-02: **PARTIAL / UNDER REVIEW**.

ReplacementRuleAccessPolicy yêu cầu server-refreshed CurrentUser. Create/update (bao gồm publish/deactivate) cần active Admin membership và respective explicit grant, có read dependency. Role Admin không tự bypass grants; misgrant create/update cho Manager/Support/custom group vẫn403. Constants replacement-rules.read/create/update chỉ là contract, không vào Permissions.All/seed/auth policies hoặc DI/endpoint.

RequireRead chỉ kiểm grant, không tự triển khai financial projection hoặc baseline grants. Module owner còn phải kiểm object/field policy, đặc biệt estimatedUnitCost và cost-derived evidence; không trả raw entity cho caller chỉ vì CanRead. Evaluate authorization/cost query đã chuẩn bị riêng trong [lifecycle facts contract](week-05-lifecycle-metrics-handoff.md); policy này không implement evaluate/threshold/rule algorithm.

Owner writes phải refresh identity/grants dưới coordinator lock, version check, versioned rule/history/audit trong transaction; helper snapshot không bảo vệ riêng against concurrent grant revocation. Không hard-code thresholds/price/currency hoặc seed rules.

Verification: **7 unit PASS /0 FAIL/SKIP**, `test-results/week5-rule-policy/rule-policy_net10.0_20261014192724.trx`; synthetic actors/grants. Không phải real HTTP403/DB-rule-state audit test. Owner RuleService/DTO chưa có để review deterministic ordering/projection; những phần đó và M3 acceptance vẫn PLANNED. Config/migrations/shared Neon/Render không đổi.

Full offline checkpoint: **576 unit +158 integration PASS /0 FAIL**, **130 cloud SKIP**, Release compilation thành công. TRX `rule-policy-checkpoint_net10.0_20261014192910.trx` / `...192914.trx` cùng folder. Focused7 là subset; 2 capacity protocol PostgreSQL PASS được lưu riêng, không biến cloud SKIP thành PASS. Frontend130 PASS của checkpoint e4efddd không chạy lại do policy này không sửa UI. Linux Docker/CI ở đúng head mới là gate publication riêng.
