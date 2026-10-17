# Thủy — query contract lifecycle metrics

Ngày 14/10/2026. W5-THUY-D4-01/03: **LOCAL / UNDER REVIEW**, chuẩn bị trước theo fallback metric fixtures; evaluator/rules/recommendations vẫn Thiện-owned và PLANNED.

## Source contract

AssetLifecycleMetricsReader.Query(assetId) là một SELECT trên non-archived asset, có correlated aggregates từ maintenance_tickets. Trước Query cần authenticated actor + replacement-recommendations.evaluate + assets.cost.read; permission Evaluate mới chỉ constant, chưa vào active catalog/seed/DI/API.

AssetLifecycleFacts là immutable record: ID/type/status, purchase/warranty/cost và raw ticket counts theo từng status, archived count, missing-cost count, known cost sum. Không đọc MaintenanceHistory hoặc license keys. Một SELECT cho counts/cost cùng PostgreSQL statement snapshot, không gọi nhiều query độc lập cho từng số liệu.

## Semantics rõ ràng, không tự chốt threshold

- AgeMonths là số tháng calendar tròn đến assessmentDate; month anniversary dùng DateOnly.AddMonths (clamp cuối tháng/năm nhuận). Purchase date thiếu/future trả null/reason, không lấy created_at thay thế.
- Warranty date bao gồm cả ngày kết thúc; expired khi date < assessmentDate. Thiếu warranty trả null.
- AssessmentDate là ngày business/UTC dùng cho tuổi/bảo hành; ticket counts/cost là **CURRENT facts**, không tái dựng lịch sử asOfDate. Historical evaluation semantics cần review trước endpoint.
- Cost từ actual_cost hiện hành của từng ticket, bao gồm ticket đã archive để không làm mất chi phí. Không SUM history snapshots/correction events; không dùng estimated_cost.
- Có ticket actual_cost null: KnownMaintenanceCost vẫn báo phần đã biết; MaintenanceCostTotal và ratio trả null với IncompleteMaintenanceCost. Không coi missing là chi phí 0 hoàn chỉnh.
- Purchase price missing/<=0: ratio null, reason rõ ràng; không chia zero hoặc suy đoán giá mua. Ratio giữ decimal, không làm tròn để so threshold.
- Raw counts gồm cả PENDING/IN_PROGRESS/RESOLVED/FAILED/CANCELLED và archived; không tự đặt MaintenanceCount mà rule phải dùng. Thiện/owner cần chốt count definition/version trước evaluator. Current status/type cho evaluator áp eligibility/rule scope riêng.
- InsufficientData mô tả từng input thiếu; không tự loại asset khỏi mọi rule khi một rule chỉ cần tuổi đã đủ dữ liệu. Không tạo recommendation, seed threshold, estimate/currency hoặc auto-retire.

## Verification

11 focused unit PASS: calendar/leap/month-end, warranty boundary, missing/future/zero-price, permission-before-repository, SQL translation không join history. Final immutable projection: **2 isolated PostgreSQL PASS /0 FAIL /0 SKIP**, 5 giây execution, known costs/archived cost/correction snapshots và incomplete values/missing asset. TRX `lifecycle-metrics-immutable_net10.0_20261014020413.trx` và `lifecycle-metrics-immutable-isolated_net10.0_20261014020430.trx` trong ignored `test-results/week5-lifecycle-metrics/`.

Trước checkpoint: full offline **539 unit +157 integration PASS**, 127 cloud SKIP/0 FAIL; Release0warning/error. TRX `lifecycle-metrics-checkpoint_net10.0_20261014020827.trx` / `...20261014020828.trx`. Không cộng focused tests lần hai; hai cloud cases nằm ngoài offline PASS. Frontend/source/package không đổi từ checkpoint trước.

Target `it_asset_management_full_schema_verify_fb_20261011_b21b`, MvpFixture opt-in/safety/model guards; không tạo/apply/reset DB, fixtures retained. Query không ghi dữ liệu; fixture writes chỉ isolated. Test actor cấp permission Evaluate giả lập cho reader, không seed permission hoặc chứng minh Replacement API auth.

Fixture có 3 tickets (RESOLVED100, FAILED200, archived CANCELLED25); các correction snapshots 900/500/100 có tổng1500 nhưng metric tổng ticket là325, ratio0.325 cho price1000. Fixture missing cost +price0 trả total/ratio null và reasons, không zero giả.

Lệnh:

```powershell
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~AssetLifecycleMetricsTests'
# Isolated opt-in/target/private config đã được kiểm tra trước khi chạy:
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~AssetLifecycleMetricsIntegrationTests'
```

## Còn cần review

MaintenanceCount inclusion, archived/cancelled history semantics của rule, historical asOfDate, currency/threshold/estimated unit cost. Chưa triển khai evaluator/recommendation hoặc API/UI mới. Không đổi schema/migration/connection/feature flags, không tác động shared Neon/Render.
