# Thủy — service reveal license key và audit identity

Ngày 14/10/2026. W5-THUY-D2-02/03: **PREPARED / UNDER REVIEW**, không phải endpoint đã triển khai.

## Phạm vi

`LicenseKeyRevealService` kiểm read/reveal grants, user active/confirmed/unlocked/token-version và active Admin membership từ DB sau khi lấy advisory lock `users-identity`. Lock dùng cùng coordinator với account/role/grant writes. Reason bắt buộc 1..1000; license/key missing trả 404, partial key tuple trả DATA_INCONSISTENT. Decrypt qua protector đã chuẩn bị, không qua metadata/list/export.

Audit chỉ licenseId/keyVersion/keyRevealed/reasonProvided. Không lưu plaintext, ciphertext hoặc nội dung reason. Response chỉ ra khỏi service sau khi audited UoW commit thành công; audit/save/commit failure không trả response. ToString của response không chứa key. `LicenseKeyRevealResult` serialize camelCase và đặt Cache-Control: no-store, Pragma: no-cache.

Không đăng ký DI, map route, cấu hình key thật, seed grants hoặc bật module. Thiện tích hợp endpoint sau review DTO/state/scope và rollout được duyệt. Response chứa full key chỉ dành explicit reveal; owner không được dùng DTO này cho metadata hoặc log. Đọc key là snapshot tại lúc query; thay key sau đó không tự thay response, audit ghi version đã đọc. Cần giữ coordinator protocol khi thay grants; không coi helper thay thế authentication pipeline.

## Sửa audit reader đang hoạt động

Reader cũ chỉ chấp nhận roleCount <=3, không còn phù hợp nhóm quyền động. Reader nay nhận roleCount/permissionCount là Int32 không âm (không dùng current request/catalog limit để lọc lịch sử), positive permissionId, boolean isEmailConfirmed và emailStatus chỉ Sent/Sandboxed/NotConfigured/Failed. Không nới whitelist cho email/password/token/arbitrary strings. Validation request 50 roles và permission catalog ở service giữ nguyên.

## Verification thực tế

- Focused: **101 unit +1 response-adapter integration PASS**, 0 FAIL/SKIP; `test-results/week5-key-protection/license-reveal-audit_net10.0_20261014190818.trx` / `...1910820.trx`.
- PostgreSQL isolated: **1 PASS**, `license-reveal-persistence_net10.0_20261014191101.trx`. Target `it_asset_management_full_schema_verify_fb_20261011_b21b`, isolation guard/name + connection check. Real AuditedUnitOfWork/EfRepository/AuditWriter/AES-GCM; temporary license/grants saved inside transaction, reveal audit verified sanitized, injected exception after Save before commit; all fixture rows/grants/audits rolled back. No shared writes, no persistent license permission seed. Sequence values can advance on rollback; no reset.
- Full offline checkpoint: **569 unit +158 integration PASS /0 FAIL**, **128 cloud SKIP**; `reveal-checkpoint_net10.0_20261014191102.trx` / `...1911103.trx`. Focused tests là subset, không cộng lần hai. Release compilation thành công; trước đó test mới có lỗi compile tên NewValues, sửa đúng NewValuesJson rồi rerun.
- Không phải HTTP reveal/JWT E2E, không mô phỏng mất kết nối lúc commit, không chứng minh production key recovery/rotation. CI Linux và owner endpoint review là gate riêng.

## Còn PLANNED

Real key source/escrow/recovery, DI/endpoint/permissions coordinated rollout, DTO clear behavior, browser reveal UX và HTTP permission/no-store E2E. Assignment API, Neon shared schema/data và Render giữ nguyên.
