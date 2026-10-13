# Tuần 4 — hợp đồng audit ticket/history của Thủy

Ngày 12/10/2026. Phạm vi **W4-THUY-D5-01 — shared integration helper**, một phần D4-03 audit redaction/transaction verification. Trạng thái **LOCAL IMPLEMENTATION / UNDER REVIEW / NOT WIRED TO MAINTENANCE API**.

## Source và hành vi

`MaintenanceAuditState.Capture(ticket)` chụp dữ liệu trước command trong memory, không giữ tham chiếu tới entity tracking. Các giá trị cost chỉ có property internal để so sánh thay đổi, không serialize vào AuditLog.

`MaintenanceAudit.Record(audit, actor, ticket, history, before)` ghi audit cho **hai business rows**: MaintenanceTicket và MaintenanceHistory. MaintenanceHistory thuộc guard audit coverage bình thường; chỉ audit ticket sẽ chưa đủ để commit. Nếu command đổi Asset, caller vẫn phải audit Asset và thêm AssetStatusHistory theo [shared status contract](week-04-shared-status-handoff.md).

Helper kiểm trước khi ghi audit:

- Ticket/history đã có identity sau Save, ticket thuộc asset hợp lệ, history tham chiếu đúng ticket.
- Actor là user hợp lệ, actor/correlation history khớp cùng request, timestamp UTC.
- CREATED dùng before=null; mọi event khác cần snapshot trước command của đúng ticket/asset.
- From/to status nếu có khớp snapshot; ASSIGNED khớp assignee trước/sau; COST_UPDATED có history.cost bằng **actual_cost sau command**.
- Event lạ bị từ chối. Helper không thực hiện state machine hoặc authorization; service của Thiện kiểm những điều này dưới lock trước khi sửa entity.

| MaintenanceHistory event | Audit action |
|---|---|
| CREATED | maintenance.create |
| STATUS_CHANGED | maintenance.status.change |
| ASSIGNED | maintenance.assign |
| COMMENTED | maintenance.comment |
| COST_UPDATED | maintenance.cost.update |
| RESOLVED | maintenance.resolve |
| FAILED | maintenance.fail |
| CANCELLED | maintenance.cancel |

Audit action là tên sự kiện, không phải permission cần seed hoặc endpoint mới.

## Snapshot và dữ liệu nhạy cảm

Writer/reader allowlist được mở rộng đúng các metadata: maintenanceTicketId, assetId, maintenanceStatus, maintenancePriority, technicianId, isArchived, eventType, performedAtUtc, estimatedCostChanged, actualCostChanged, commentProvided. Reader kiểm lại loại dữ liệu, ID dương/null hợp lệ, status/priority/event catalog và thời gian UTC; metadata lịch sử không hợp lệ bị lọc.

Không thêm cost/estimatedCost/actualCost hoặc description/resolution/comment vào allowlist. Audit ghi dấu thay đổi, không ghi số tiền hoặc free text. MaintenanceHistory.cost vẫn là snapshot actual_cost phục vụ lịch sử nghiệp vụ; endpoint/history DTO phải áp dụng [cost/scope policy](week-04-maintenance-access-handoff.md) trước khi trả dữ liệu. Không cộng các snapshot history để tính tổng chi phí ticket.

## Cách tích hợp cho Thiện

Trong cùng `IUnitOfWork.RunAsync`:

1. Lock asset, đọc lại ticket, kiểm scope/version/state. Snapshot `before=MaintenanceAuditState.Capture(ticket)` trước khi đổi fields.
2. Sửa ticket, tạo history với cùng scoped actor/correlation và UTC của command. Khi đổi asset, thêm status history và audit asset theo contract đã bàn giao.
3. Save ticket/history để có IDs. Không commit tại bước này.
4. `MaintenanceAudit.Record(writer, actor, ticket, history, before)`; CREATED dùng before=null.
5. Save audit, để audited unit of work kiểm coverage rồi commit. Nếu Record/Save/coverage lỗi, để exception thoát và rollback toàn transaction.

Không dùng snapshot sau thay đổi thay cho before; không audit ticket rồi bỏ history; không tạo correlation mới. AuditWriter phải dùng cùng scoped actor với helper. Không đặt helper vào singleton hoặc commit history/audit ở transaction khác.

## Verification

- Release build **0 warning /0 error**.
- Focused **110 unit PASS /0 FAIL /0 SKIP**: gồm 18 MaintenanceAudit cases mới cùng regression AuditLog/cost-scope. Capture bất biến, metadata writer→reader giữ lại, cost/free text bị loại, mismatch context không tạo audit, historical payload lạ bị lọc.
- PostgreSQL integration **3 PASS /0 FAIL /0 SKIP**, 7 giây execution, trên database riêng `it_asset_management_full_schema_verify_fb_20261011_b21b`: commit ticket/history/audit có context khớp và snapshot an toàn; thiếu audit history rollback hai business rows; lỗi sau audit rollback ticket/history/audit. Fixture đã kiểm schema/model sẵn sàng, không create/migrate/reset DB. Một ticket/history thành công và các fixture identity/asset được giữ trong isolated DB; không dùng delete để cleanup hoặc ghi shared neondb. Email provider được mock.
- 476 unit +157 integration PASS ở handoff scope/cost là checkpoint trước, không tuyên bố đã chạy lại toàn suite cho audit helper mới. Không test frontend/Docker vì không đổi UI/hosting/container.

```powershell
$env:ITAM_RUN_NEON_TESTS='0'
$env:ITAM_FULL_SCHEMA_TESTS='0'
dotnet build ItAssetManagement.slnx -c Release --no-restore
dotnet test tests/ItAssetManagement.UnitTests/ItAssetManagement.UnitTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MaintenanceAuditTests|FullyQualifiedName~AuditLogTests|FullyQualifiedName~MaintenanceAccessPolicyTests' --logger 'trx;LogFilePrefix=maintenance-audit-focused' --results-directory test-results/week4-maintenance-audit --verbosity quiet
# PostgreSQL run: ITAM_RUN_NEON_TESTS=1, target isolated và private config path đã kiểm tra.
dotnet test tests/ItAssetManagement.IntegrationTests/ItAssetManagement.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MaintenanceAuditIntegrationTests' --logger 'trx;LogFilePrefix=maintenance-audit-isolated' --results-directory test-results/week4-maintenance-audit --verbosity quiet
```

Evidence TRX ở ignored `test-results/week4-maintenance-audit/`: `maintenance-audit-focused_net10.0_20261012230836.trx` (110 unit PASS) và `maintenance-audit-isolated_net10.0_20261012230914.trx` (3 PostgreSQL PASS). Đây là test helper/persistence, không phải E2E Maintenance API.

## Trạng thái và phần còn lại

D5-01 đã chuẩn bị shared audit helper; **endpoint integration với Thiện vẫn PLANNED**. Không viết Controller/Service của Thiện, không seed quyền hoặc bật Maintenance/Assignment API. Full Assignment→Maintenance→resolve flow D5-02, UI integration D4-04/D5-03 và nghiệm thu reviewer còn PENDING.

Trong lượt này chưa commit/push, không sửa migrations/configuration hoặc shared Neon/Render. WIP shared-status/cost-scope và ba file báo cáo cũ giữ nguyên. Trạng thái source trên GitHub/production không được suy từ helper local này.
