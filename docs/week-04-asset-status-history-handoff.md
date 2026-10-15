# Thủy — tích hợp lịch sử trạng thái tài sản, 15/10/2026

## Phạm vi

Nối **chức năng đang có** thay vì mở module chờ Thiện. Asset detail đã có ba tab nhưng cả ba hiển thị PLANNED; endpoint `GET /api/v1/assets/{id}/status-history` đã hoạt động. Nay chỉ tab **Trạng thái** lazy-load read API, phân trang20, newest-first, trạng thái cũ/mới, lý do, nguồn, người thực hiện và thời điểm UTC. Phân bổ/Bảo trì giữ PLANNED, không gọi API hoặc kích hoạt module.

`HistoryDto` giữ nguyên positional constructor/deconstruction8 fields, bổ sung init-only nullable `changedByDisplayName`. `AssetService.HistoryAsync` dùng left join user **sau page selection**, giữ thứ tự UTC/ID và không trả cost/email/password. Tên là tên hiện tại (UI có ghi rõ), không snapshot lịch sử. Backend cũ không có tên vẫn đọc được, hiển thị thiếu tên thay vì bịa tên hoặc xin quyền users.read. Không thay Controller/Service của Thiện, entity, schema, migration, grants hoặc configuration.

## Guard và UX

- Endpoint runtime chỉ `page,pageSize`; `from,to,status,sortBy` chưa implement. Adapter từ chối trước fetch; docs tách rõ PLANNED/runtime. Giữ UTC ISO8601, API status casing, source và correlation ID thực tế.
- `assets.read` và `assets.history.read`, API mode/adapter bắt buộc; demo không sinh lịch sử giả. Client kiểm payload và chỉ giữ known nonfinancial fields.
- Chỉ fetch khi chọn tab; click/keyboard Left/Right/Home/End vẫn đúng ARIA/roving tabindex. Đổi tab, paging, detach, logout/relogin hoặc revoke quyền không được ghi response cũ vào panel. Transport session revision cũng chặn cùng user đăng nhập lại.
- Loading/error/empty riêng; không chuyển503/404 thành rỗng. Retry chỉ GET khi người dùng bấm, không mutation tự retry. Server message không được echo lên panel. Empty state nằm ngoài wide table.
- Browser QA synthetic tách biệt kiểm desktop/mobile; sau phát hiện reason co quá hẹp làm hàng quá cao, thêm bounded reason column và wrap chuỗi dài, giữ bảng cuộn ngang trong card. Không đưa preview/screenshot vào production/Git.

## Kiểm thử thực tế

| Phạm vi | Kết quả |
|---|---|
| Node full `node --test tests/*.test.mjs` |181 PASS /0 FAIL/SKIP (14 focused mới) |
| `node scripts/frontend-check.mjs` |42 syntax /46 CSP/database-boundary PASS |
| Release build `dotnet build ItAssetManagement.slnx -c Release --no-restore` |0 warning/error |
| Offline `dotnet test ItAssetManagement.slnx -c Release --no-build --no-restore` |576 unit +158 offline integration PASS;134 cloud SKIP |
| Isolated `--filter FullyQualifiedName~AssetStatusHistoryApiTests` |4 PASS /0 FAIL/SKIP |

Offline dùng `ITAM_RUN_NEON_TESTS=0`, `ITAM_FULL_SCHEMA_TESTS=0`. Isolated run dùng `ITAM_RUN_NEON_TESTS=1`, `ITAM_FULL_SCHEMA_TESTS=0`, `ITAM_TEST_DATABASE=it_asset_management_full_schema_verify_fb_20261011_b21b`; private config path cung cấp local, không ghi credentials ra log. Fixture kiểm đúng target và không apply migration/reset. Synthetic assets/role/user được giữ trên isolated DB, không phải dữ liệu shared.

4 HTTP/PostgreSQL cases: tạo fixture rồi retire và đọc stable pages/tên/UTC; Support đọc không cost/email/password; query invalid400/anonymous401/missing404; custom inventory-only fixture role không có history grant nhận403. Đây là endpoint thật trên PostgreSQL riêng, **không phải** browser fixture chứng minh backend hoặc shared acceptance.

TRX ignored: `test-results/week4-status-history-ui/asset-status-history-api_net10.0_20261015134355.trx`; offline `history-regression_net10.0_20261015134946.trx` và `...134947.trx`. Browser evidence cùng thư mục, synthetic/static no API/Neon.

Browser15/10: normal/empty states và keyboard Tab focus panel đã kiểm; mobile375 CSS px (override390, scrollbar làm client375), document không overflow ngang, chỉ table wrapper cuộn. Sau CSS fix2 fixture rows72/56px, thay vì lý do co hẹp tạo hàng cao. Desktop giữ default viewport; override đã reset. `history-desktop-20261015.jpg`, `history-mobile-20261015.jpg`, `history-empty-mobile-20261015.jpg` ignored; console không warning/error. Không gọi đó là authenticated browser/production E2E. Một lần native screenshot không capture được, chuyển sang documented tab screenshot rồi xem ảnh thực tế; không coi capture lỗi là PASS.

## Rollout / tiếp tục

Branch `codex/lecturer-feedback`, PR#1 DRAFT/block merge cho toàn bộ lecturer-feedback rollout. Không dựa vào additive DTO để bỏ qua gate LF migration/auth của **những checkpoint trước**. Không merge main/deploy/apply shared/seed hoặc gửi email thật. Assignment API vẫn disabled. CI checkpoint mới phải kiểm theo đúng SHA sau push; Linux Docker local unavailable, không lấy CI cũ chứng minh WIP mới.

Tiếp theo: kiểm tiếp phần Thủy và actual Asset UX/API còn thiếu trong khi chờ Thiện; không đánh dấu toàn bộ Tuần4/M2 DONE từ tab read này.
