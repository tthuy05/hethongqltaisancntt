# Thủy — chuẩn bị License capacity, alerts và lifecycle

Ngày rà soát: 17/10/2026. Trạng thái: **PREPARED / UNIT-VERIFIED; module integration PENDING**.

Đây là phần chuẩn bị của Thủy cho Tuần 5. Các helper không được đăng ký thành API mới, không thay Controller/Service của Thiện và không đóng toàn bộ task Tuần 5.

## Phạm vi source

- `LicenseCapacityPolicy`: target XOR/positive IDs, capacity floor, archive blocker, kiểm tra license active/effective khi cấp hoặc transfer. Transfer phải dùng allocation thuộc đúng license, đang mở và khác target; count âm bị từ chối. Biên expiry hiện dùng `now <= ExpiresAtUtc`; cần thống nhất với owner API trước integration.
- `LicenseAlertsReader`: authenticated `licenses.read`; projection không chứa full key/ciphertext, cost null nếu thiếu `assets.cost.read`; expiry/window, seat utilization và summary trên dữ liệu hiện tại. Danh sách/summary hiện chưa phân trang và còn aggregation trong memory; cần bounded-query/performance review trước expose endpoint. Không có benchmark PostgreSQL hoặc p95 mới cho reader này.
- `AssetLifecycleMetricsReader.QueryCandidates/EvaluateCandidatesAsync`: reuse current-snapshot facts và metric calculation, optional asset-type filter. Không cộng lại maintenance-history snapshots; không phải historical `asOf` reconstruction. Batch materialization cần giới hạn ở owner evaluator trước production.
- `ReplacementRecommendationPolicy`: kiểm ID/priority/disposition/JSON object, numeric(9,4), numeric(18,2), year 2000–2100; validate note trước khi sửa object. `ACTIVE` có thể chuyển `PLANNED`, `DISMISSED`, `SUPERSEDED`; self-transition bị chặn. Actor/time/note được ghi vào các field `Disposition*` thực tế. `Supersede` nhường current slot; việc giữ planned disposition của bản cũ cần owner review.
- Audit writer/read boundary: typed License/Replacement metadata; không trả full license key, password, token hoặc raw evaluation snapshot. Cost vẫn phụ thuộc quyền đọc chi phí.

## Giới hạn cần giữ

Policy C# không tự bảo đảm tranh seat cuối hoặc one-current khi có nhiều request. **PENDING**: owner service phải lock license/current recommendation trong transaction, count/validate/write cùng transaction, xử lý unique/concurrency errors và ghi audit. Chưa có concurrent allocation/transfer/reevaluation HTTP acceptance cho module thật từ phần chuẩn bị này.

Schema Baseline V1 đã có các bảng/constraints liên quan; không tạo migration mới. Không thay DB chung, seed permissions, key-ring production, Render hay Assignment rollout flag.

## Verification

- Năm regression mới tái hiện FAIL trước fix, PASS sau fix: foreign-license transfer, negative count, schema bounds, invalid-note object mutation, ACTIVE self-transition.
- Focused policy/alerts/lifecycle/audit suite: **155 PASS** trên working tree khi review.
- Build/full regression của đúng tập source publication được ghi tại [review ngày 17/10](thuy-review-2026-10-17.md). Cloud test bị skip phải báo riêng.
- Các số test cũ là checkpoint theo thời điểm; không thay thế evidence 30 task Tuần 2 hoặc nghiệm thu Thiện/Mentor.

## Task còn thiếu

W5-D3-01/02 mới có pure capacity rules và test đơn luồng; transaction/race integration **PENDING**. W5-D4 có candidate metrics/policy helpers, chưa phải evaluator/rule API hoàn chỉnh. W5-D5 có query proposal/audit metadata, chưa phải navigation/API integration. W5-D6 review độc lập, production smoke và acceptance **PENDING**.
