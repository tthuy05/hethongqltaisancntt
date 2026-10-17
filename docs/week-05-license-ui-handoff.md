# Thủy — License read client/list/detail preparation

Ngày 14/10/2026, W5-THUY-D3-03/04 **PARTIAL / UNDER REVIEW**. Tiếp tục theo yêu cầu không dừng ở checkpoint nhỏ; không thay phần backend của Thiện.

## Đã có source

Prepared read adapter EP-051/053 dùng cùng authenticated/no-store transport; không auto request, mock fallback, retry hoặc mutation/reveal. License list/detail components mặc định `enabled=false`: trả PLANNED và không fetch dù có grants. App.js/menu/routes giữ nguyên, production License chưa mở. Explicit enabled trong isolated component tests không phải runtime feature approval.

List có keyword/vendor search, paging, detail link; key masked, không cost/notes/unknown fields. Thiếu usedQuantity hiển thị Chưa có dữ liệu, không 0 giả; availableQuantity chỉ derive khi used known/valid. Metadata có full key/ciphertext/password/token bất kỳ nesting bị reject502 generic không echo payload. Invalid page/resource identity/timestamps/capacity/mask bị reject, không render record khác. Error401 dùng session invalidation chung; 403/404/503 không đổi thành mock/empty.

## View contract proposal — OWNER REVIEW REQUIRED

View model dự kiến: id/licenseCode/software{id,name}/licenseType/totalQuantity/isActive, optional usedQuantity/availableQuantity/startsAt/expiresAt/maskedKey/vendor/status. IDs safe integer positive; type theo schema baseline; positive totalQuantity. Times ISO UTC Z; mask `****-` +4 Unicode scalars. Adapter deliberately chưa nhận sort/expiration filters để không giả định semantics khi owner DTO/query chưa freeze.

Đây là proposal cho projection UI, không sửa API spec/DTO backend để hợp thức hóa tên đoán. Thiện phải review actual response/query trước route activation; không spread EF entity vào JSON. Không dùng assets.cost.read như License cost authorization; UI này chưa hiển thị chi phí.

## Verification

**12 focused /142 full frontend PASS**, 0 FAIL/SKIP; **36 syntax /40 CSP/database-boundary checks PASS**. `node --test tests/frontend-license-client.test.mjs tests/frontend-license-pages.test.mjs`; `node --test tests/*.test.mjs`; `node scripts/frontend-check.mjs`.

Browser skill dùng isolated static preview localhost4176 (synthetic banner, không API/Neon). Desktop1366 và mobile375/390 actual layout observations: list/detail, missing capacity/key, search empty/reset, no page overflow; table scroll riêng. Phát hiện empty row message nằm ngoài mobile viewport, sửa standalone status; code/key/quantity nowrap dùng CSS có sẵn. Có lần screenshot full-page mobile lỗi; đã reobserve/reset viewport và lưu viewport screenshot, không gọi capture lỗi là PASS.

Ignored screenshots `test-results/week5-license-ui/`: license-detail-desktop.jpg, license-list-mobile.jpg, license-empty-mobile.jpg, license-detail-mobile.jpg. Preview HTML/JS ở artifacts/frontend, không tracked/production output. Đây không phải browser authenticated License API E2E hoặc live database proof. Backend/regression CI phải kiểm đúng new head riêng.

## Tiếp theo

Allocation target lookup/form/history, shared409 refresh UX, owner actual DTO/services và license secret decisions. Không main merge/deploy/seed/migration/module activation. Giữ cấu hình/connection string, InitialM1/S18 và3 report WIP cũ.
