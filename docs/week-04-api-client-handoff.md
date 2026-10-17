# Tuần 4 — API client Assignment và user lookup của Thủy

Ngày **14/10/2026**. Phạm vi fallback của **W4-THUY-D4-04 / D5-03**: chuẩn bị client chung trước khi Thiện nối page module. Trạng thái **IMPLEMENTED LOCALLY / UNDER REVIEW**, chưa mở menu/page hoặc rollout API.

## Interface thực tế

`createApiServices()` trả thêm `services.assignments`. API transport hiện có giữ token trong memory và xử lý expiry/401/session revision cho tất cả request, không tạo client auth thứ hai.

| Method | Request |
|---|---|
| assignments.list(query) | GET /api/v1/asset-assignments |
| assignments.get(id) | GET /api/v1/asset-assignments/{id} |
| assignments.assign(dto) | POST /api/v1/asset-assignments |
| assignments.returnAsset(id, dto) | POST /api/v1/asset-assignments/{id}/return |
| users.lookup(query) | GET /api/v1/users/lookup |

Thiện import cùng `services` mà page khác đang dùng; không tạo `createApiServices()` mới trong từng page vì instance mới chưa có phiên đăng nhập. Không lấy token từ auth.session hoặc lưu localStorage. Client dùng same-origin, no-store và transport chung. Đây là API-mode adapter; không thêm Assignment mock/demo module.

## Contract khớp source Controller/Service hiện tại

- List chỉ nhận `page, pageSize, assetId, userId, departmentId, active`. `active` là boolean, false vẫn được gửi. ID là number nguyên dương an toàn; page >=1, size 1..100, offset <=Int32.MaxValue. Caller parse ID/query input trước khi gọi client.
- Assign nhận `assetId`, đúng một `userId/departmentId`, `assignedAt?`, `note?`. Không gửi expectedReturnAt hoặc tên cột `assignedAtUtc`. Timestamp ISO UTC kết thúc Z; ghi chú trim, tối đa 1000 ký tự.
- Return nhận `rowVersion`, `note` bắt buộc, `returnedAt?`. RowVersion là Base64 canonical đúng 16 byte từ detail response. Không gửi `condition` vì Service hiện tại chưa có field này.
- JSDoc shapes được bổ sung vào models/contracts.js theo DTO minimal hiện có, không giả định enriched detail đã hoàn thành.
- User lookup dùng đúng route EP-004, cùng session/transport. Backend vẫn kiểm lookup permission, paging/query/active visibility; client không dùng lookup thay technician eligibility check.
- Transfer, route assignment history riêng của asset, filter status/from/to/sort và Maintenance adapter vẫn **PLANNED**. Không gọi endpoint được mô tả ở design khi source thực tế chưa có.

Ví dụ tích hợp page Thiện:

```javascript
const page = await services.assignments.list({ assetId: Number(assetId), active: true });
const detail = await services.assignments.get(Number(assignmentId));
await services.assignments.returnAsset(detail.id, { rowVersion: detail.rowVersion, note: reason });
const users = await services.users.lookup({ keyword: search, page: 1, pageSize: 20 });
```

## Error/session và rollout

401 clear session theo shared transport; response muộn của phiên cũ bị SESSION_CHANGED và không xóa phiên đăng nhập mới. 403/409/404/503 giữ phiên hợp lệ, trả ServiceError với traceId/retryAfter khi server có cung cấp. Client không retry thao tác assign/return; sau 409 page tải lại detail/version rồi để người dùng quyết định.

Permission trong session không chứng minh Assignment rollout sẵn sàng. Thiện phải phối hợp gate/readiness và page integration trước khi Thủy thêm menu/link; client tồn tại không bật backend feature flag. Không thêm URL UI cho module chưa có page.

Client validation giúp UX nhưng backend vẫn có quyền quyết định auth/scope/state/time/concurrency. Request sai hoặc mất kết nối không được chuyển sang mock để báo thành công.

## Verification

- **45 frontend client/session/service tests PASS**, 0 FAIL/SKIP, gồm 5 test groups mới cho Assignment/lookup và regression session/M1 transport. Nhóm validation chứa 17 negative inputs (XOR, unsupported fields, UTC, query, ID và rowVersion).
- Source checks: **32 JavaScript syntax +36 CSP/database-boundary checks PASS**.
- **16 build/production packaging tests PASS /0 FAIL /0 SKIP**: module graph/imports local đầy đủ, production API-only gồm 31 local assets, không mock modules/Swagger và không fallback API sang HTML. Tổng hai nhóm kiểm thử lượt này: **61 PASS**; không cộng lần chạy lại 45 cases. Không dùng mocked fetch hoặc package checks làm bằng chứng backend public đã mở.
- Không chạy .NET/Neon/Docker trong lượt client này vì không sửa backend/schema/container. Kết quả các lượt trước giữ riêng; frontend requests chỉ dùng fetch fixture, không gửi traffic nghiệp vụ thật.

```powershell
node --test tests/frontend-assignment-client.test.mjs tests/frontend-session.test.mjs tests/frontend-services.test.mjs
node scripts/frontend-check.mjs
node --test tests/frontend-build.test.mjs tests/frontend-production.test.mjs
```

## Git và tiến độ

Đã fetch/review remote: `origin/feature/assignment-maintenance` vẫn `5dbef3a`; `origin/main` vẫn `1bc2767`. Không merge remote vào WIP. Client nằm trên working tree `codex/lecturer-feedback` cùng các helper local đã chuẩn bị, chưa commit/push/deploy.

Thủy đã có backend shared contracts và API-client fallback để bàn giao. D4-04/D5-03 còn **PARTIAL** trước page/readiness integration; Maintenance Service/API/UI của Thiện, queue/custom-group scope review và shared publication gates vẫn cần hoàn thiện theo checklist hiện có. WIP ba file báo cáo cũ, migrations/configuration/Neon/Render được giữ nguyên.

File của lượt client này: `wwwroot/js/services/assignment-service.js`, `wwwroot/js/services/api-services.js`, `wwwroot/js/models/contracts.js`, `tests/frontend-assignment-client.test.mjs`, tài liệu bàn giao này và addendum trong PROJECT_STATUS.md/CHANGELOG.md/docs/weekly/week-04.md. Không gom WIP backend hoặc các file báo cáo vào publication trong lượt này.
