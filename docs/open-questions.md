# Open Questions và Assumptions

> Trạng thái: **PLANNED — WEEK 2 DESIGN REVIEW**  
> Chỉ liệt kê câu hỏi còn ảnh hưởng thiết kế/scope/security/deployment. Mỗi câu có safe default để không chặn Week 2; default không được coi là Mentor đã xác nhận.

## 1. Quy ước trạng thái

| Status | Ý nghĩa |
|---|---|
| `OPEN` | Cần Product Owner/Mentor quyết định; đang dùng safe default tạm thời. |
| `DECIDED` | Baseline đã được chốt cho MVP trong yêu cầu hiện tại; chỉ đổi qua ADR/scope change. |
| `DEFERRED` | Không cần quyết định cho MVP; xem xét phase sau. |

## 2. Câu hỏi đang mở

| ID | Câu hỏi ảnh hưởng thiết kế | Safe default đang dùng **PLANNED** | Ảnh hưởng nếu đổi | Owner / cần trước | Status |
|---|---|---|---|---|---|
| OQ-001 | Chính sách password, lockout và JWT lifetime cụ thể là bao nhiêu? | Theo security design: đề xuất password tối thiểu 12 ký tự và JWT TTL 15 phút; lockout threshold/duration và rate limit cấu hình, chưa có giá trị được phê duyệt. PasswordHasher xử lý hash, không thay thế password validation; không refresh token. | Security config, UX, auth tests, runbook. | Mentor/Security / trước implementation auth Week 3 | OPEN |
| OQ-002 | Doanh nghiệp có trường hợp SerialNumber trùng hợp lệ (thiết bị không có serial, vendor tái dùng serial) không? | Serial nullable; khi có thì trim/normalize và unique bằng PostgreSQL partial unique index toàn hệ thống. AssetCode luôn unique. | Unique index, import validation và quy trình xử lý ngoại lệ. | Mentor/Asset owner / trước migration Asset Week 3 | OPEN |
| OQ-003 | Khi Asset đang cấp cho User/Department được đưa đi maintenance, active assignment có giữ nguyên không? | Giữ active assignment để phản ánh trách nhiệm; Asset chuyển `Maintenance`. Khi ticket kết thúc, về `InUse` nếu assignment còn active, nếu không về `InStock`. | BR-013, status flow, reporting và assignment UI. | Asset owner / trước maintenance Week 4 | OPEN |
| OQ-004 | Giá trị threshold replacement cho từng Asset Type là bao nhiêu? | Không đặt con số giả. Schema/rule hỗ trợ Age, MaintenanceCount, MaintenanceCost/PurchasePrice và Warranty+Failure; Admin IT cấu hình/publish trước khi evaluation. | Seed/config, unit tests biên, recommendation/budget. | Mentor/System Manager / trước lifecycle Week 5 | OPEN |
| OQ-005 | Window “expiring soon” cho warranty/license là bao nhiêu ngày và có khác nhau theo loại không? | Một giá trị cấu hình chung ban đầu, gợi ý 30 ngày nhưng **chưa chốt**; calculation dùng as-of UTC. | Alert query, dashboard/report, tests và notification sau này. | Mentor/System Manager / trước license/dashboard Week 5–6 | OPEN |
| OQ-006 | Annual replacement budget dùng năm dương lịch hay năm tài chính, và có bắt buộc breakdown/quota theo Department không? | Dùng năm dương lịch theo business timezone, `planned_replacement_year` mặc định là năm evaluation và có thể chỉnh khi ghi disposition; breakdown theo owning department hiện tại của Asset; không có quota/approval. Record thiếu estimate/năm được tách khỏi total. | Query/report contract, snapshot ownership, budget evidence. | Mentor/Finance stakeholder / trước budget Week 6 | OPEN |
| OQ-007 | Production sẽ dùng cơ chế nào để bảo vệ license key và JWT signing key? | Development dùng secret ngoài source; production dùng platform secret store/Data Protection hoặc KMS tương ứng. License key mã hóa/protect at-rest; key encryption tách khỏi database. | Deployment, key rotation, backup/restore, reveal availability. | Infra/Security / trước license Week 5 và deployment | OPEN |
| OQ-008 | Audit Log phải giữ bao lâu, có yêu cầu legal hold/anonymization và có cho export không? | Append-only, Admin IT read-only; không có delete/export công khai trong MVP nếu chưa chốt. Retention job không triển khai trước policy. | Storage/index/partition, privacy, API và operation runbook. | Mentor/Security / trước Audit production use Week 4–7 | OPEN |
| OQ-009 | Giới hạn file import (MB, số dòng), template version và encoding được chấp nhận là gì? | Chỉ `.xlsx` theo versioned template; limit cấu hình bảo thủ và dry-run/validation trước commit; giá trị cụ thể được load/performance test rồi chốt. | Upload config, timeout/memory, error UX và test dataset. | Mentor/Operator / trước import Week 7 | OPEN |
| OQ-010 | API hosting, Neon region/plan/actual PostgreSQL version, backup RPO/RTO và proxy topology là gì? | Một ASP.NET Core instance + PostgreSQL hosted on Neon; shared development platform đã DECIDED (ADR-018), actual setup/backup/restore chưa verified. HTTPS termination/proxy phải trust-config. Region/branch/database/roles/credentials thực tế cần setup trước Week 3; production RPO/RTO trước deployment. | Connection resiliency, forwarded IP audit, secret store, test isolation, backup và monitoring. | Người dùng/Thủy trước Neon setup Week 3; Infra/Mentor trước production | OPEN |
| OQ-011 | Currency cấu hình cho purchase/maintenance/license/replacement cost là gì? | Một currency duy nhất cho MVP; đề xuất VND nhưng không ghi cứng vào dữ liệu/logic trước xác nhận. | Precision/display/export và budget total; multi-currency vẫn out of scope. | Mentor/Finance stakeholder / trước cost implementation Week 3–6 | OPEN |
| OQ-012 | Technical Support có được tự nhận mọi Pending ticket hay chỉ ticket được System Manager/Admin phân công? | Support xem queue được phép và có thể self-claim atomically; không gán ticket cho người khác. Admin/Manager được assign/reassign. | Object-level authorization, concurrency và workload routing. | Support lead/Mentor / trước maintenance Week 4 | OPEN |
| OQ-013 | Nguồn giá ước tính thay thế được phê duyệt là bảng giá nào và ai cập nhật? | MVP chỉ dùng `replacement_rules.estimated_unit_cost` do Admin IT cấu hình; thiếu giá để null và báo thiếu dữ liệu, không suy diễn từ purchase cost. | Rule schema/configuration, cost estimation, ngân sách và kiểm thử. | Mentor/Finance stakeholder / trước lifecycle/budget Week 5–6 | OPEN |

## 3. Baseline đã quyết định — không còn là câu hỏi mở

Các câu hỏi mẫu quan trọng đã có câu trả lời trong yêu cầu hiện tại. Chúng được giữ ở đây để reviewer thấy assumption và không hỏi lại điều repository/environment đã xác định.

| ID | Câu hỏi | Quyết định MVP **DECIDED** | Traceability |
|---|---|---|---|
| DQ-001 | User có thể thuộc nhiều Department không? | Không. User thuộc zero/one Department. | ASM-003, BR-044, user schema. |
| DQ-002 | Asset có thể assign trực tiếp cho Department thay vì User không? | Có. Mỗi active assignment target đúng một User **hoặc** Department. | ASM-004, BR-004, UC-003. |
| DQ-003 | License có thể assign đồng thời User và Asset không? | Không. Mỗi allocation target đúng một User **hoặc** Asset. | ASM-010, BR-016, UC-008. |
| DQ-004 | Transfer Asset có cần approval không? | Không có formal approval workflow MVP. Authorized action có hiệu lực ngay và được audit. | ASM-002, BR-005, BR-054, UC-005. |
| DQ-005 | User bị xóa hay deactivate? | Deactivate/lock; không hard delete history. | ASM-005, BR-039–BR-042, BR-055. |
| DQ-006 | Database là PostgreSQL hay SQL Server? | PostgreSQL hosted on Neon **PLANNED**, shared development cho Thủy/Thiện theo quyết định mới; SQL Server chỉ còn historical audit. | ADR-018, ASM-006, NFR-015. |
| DQ-007 | Target framework nào? | .NET 10 **PLANNED** theo SDK audit; chưa tạo project. | ASM-007, NFR-015. |
| DQ-008 | Có Refresh Token không? | Không trong MVP; access token only, client-side logout. | ASM-008, BR-027, UC-001. |
| DQ-009 | Import partial hay all-or-nothing? | All-or-nothing cho mọi import MVP. | ASM-009, BR-029, UC-011. |
| DQ-010 | Asset/business history delete hay archive? | Archive/deactivate, FK restrict và history append-only. | ASM-005, BR-006/014/037/039/040. |
| DQ-011 | Maintenance có `Cancelled` không? | Có; là terminal state có reason. Không có `OnHold`/`Closed` riêng trong MVP. | BR-011, UC-007. |
| DQ-012 | System Manager có xem cost/license/report toàn hệ thống không? | Có: operational/financial report và license metadata/allocation/cost toàn hệ thống; không reveal key, không identity admin/audit access. | Actors, permission matrix, BR-032. |
| DQ-013 | Technical Support xem gì? | Non-financial asset inventory cần cho support và scoped maintenance; không cost/license/master/admin/import/report tài chính. | Actors, permission matrix, BR-026/032. |
| DQ-014 | Ai được reveal full license key? | Chỉ Admin IT qua explicit action có reason/audit; mặc định luôn masked. | ASM-011, BR-018, UC-022. |
| DQ-015 | Role catalog có động không? | Không. Chỉ ba fixed role; Admin IT quản lý user-role assignments, không CRUD role definition. | ASM-001, FR-005, UC-015. |

## 4. Deferred questions

| ID | Câu hỏi | Lý do defer | Status |
|---|---|---|---|
| OQ-D01 | Có cần enterprise SSO/LDAP/SAML? | Out of scope MVP; chỉ xem xét khi có identity provider và security requirements cụ thể. | DEFERRED |
| OQ-D02 | Có cần multi-company tenancy? | Out of scope; sẽ thay đổi mọi ownership/unique/index/policy. | DEFERRED |
| OQ-D03 | Có cần advanced ML predictive maintenance? | Rule-based logic đủ cho MVP và có thể giải thích. | DEFERRED |
| OQ-D04 | Có cần real-time WebSocket/push notification? | Dashboard/query và optional scheduled notification đủ cho MVP. | DEFERRED |
| OQ-D05 | Có cần partial import? | Trái baseline toàn vẹn all-or-nothing; chỉ xem sau khi có use case rõ. | DEFERRED |

## 5. Decision handling

Khi một `OPEN` question được trả lời:

1. Ghi quyết định/ lý do/ hậu quả vào `DECISIONS.md`.
2. Cập nhật status thành `DECIDED` và bỏ safe-default wording.
3. Kiểm tra tác động theo chuỗi Requirements → Use Cases → Business Rules → Database/ERD → API → Permissions/Security → Tests/Roadmap.
4. Không retroactively ghi implementation/test là hoàn tất nếu chưa chạy thực tế.

Các câu hỏi này không cho phép tự chuyển sang Week 3. Sau tài liệu Week 2 vẫn phải dừng chờ `APPROVED`.
