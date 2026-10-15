# Thủy — allocation draft form preparation

Ngày 15/10/2026. W5-THUY-D3-03/04 **PARTIAL / UNDER REVIEW**; tiếp nối [target/conflict](week-05-license-target-conflict-handoff.md). Không viết AllocationService/Controller của Thiện.

## Phạm vi source

`licenseAllocationDraft` là internal UI draft, không phải POST DTO đã approved. Chỉ licenseId + userId XOR assetId + optional note<=1000; positive safe-integer IDs, strict allowed fields, normalized note và immutable output. Không quantity, key, client timestamps hoặc tự nhận seat/capacity đã được cấp.

`licenseAllocationForm` mặc định closed, cần explicit preparation flag/grants và callback local onPrepared. Không callback hoặc license inactive thì button disabled. Có1 outer form; lookup div/button của picker không nested form/implicit mutation submit. Metadata dùng sanitized licenseView, ghi chú/target giữ trong component memory, không storage/network mutation.

Submit rechecks current session/grants/selection. Single-flight callback; đổi bản nhập lúc pending phải chuẩn bị lại, session/dispose/grant change không hiện completion cũ. Callback failure giữ note/selection, generic error, không tự retry. Backend vẫn phải kiểm quyền/type/effective dates/eligibility/capacity/version; form này không thay validation đó.

License detail có opt-in allocationPreparation riêng. Reader thiếu assign permission vẫn xem metadata; không mở action hoặc biến cả detail thành403. Default detail không có form; root.dispose cho owner renderer/lifecycle integration. App.js/menu/runtime routes không đổi.

## Verification

- **163 full frontend PASS /0 FAIL/SKIP**, **40 syntax /44 CSP/database-boundary checks PASS**.
-7 draft/form cases +2 detail-composition cases mới, nằm trong163. `node --test tests/frontend-license-allocation-form.test.mjs tests/frontend-license-pages.test.mjs`; `node --test tests/*.test.mjs`; `node scripts/frontend-check.mjs`.
- Browser skill15/10: static synthetic localhost4176, actual outer form count1. Enter lookup chỉ populate options và giữ `Số bản chuẩn bị:0`; chọn user/note + explicit prepare mới thành1 và hiển thị chưa cấp seat/ghi DB. Screenshot ignored `test-results/week5-license-ui/license-allocation-draft-20261015.jpg`. Không phải authenticated Allocation HTTP E2E hoặc database capacity test.
- Không chạy lại .NET/Neon vì scope chỉ JS; CI đúng new head là gate riêng. Parent c9fbf60 CI SUCCESS với576 unit +158 offline integration PASS /130 cloud SKIP,154 frontend PASS, published/Linux Docker smoke OFF32/ON36; không lấy đó xác nhận source mới.

## Integration còn chờ owner

Thiện freeze actual Allocation request/response/errors/ETag/grants; owner review mapping từ internal draft. Không nối onPrepared vào POST thật hoặc đổi menu trước phê duyệt module/readiness/rollout. VOLUME/OTHER target contract không được đoán; revoke/transfer/history và actual service race/403/audit tests còn PLANNED. Shared Neon/Render/connection settings/migrations và3 report WIP giữ nguyên.
