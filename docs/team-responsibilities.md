# Team Responsibilities — PLANNED

> Kế hoạch cộng tác ngày 01/10/2026 cho Nguyễn Đậu Thủy (primary contributor) và Thiện. Đây là phân công dự kiến, không xác nhận task đã làm. Mỗi workstream chỉ có **một Primary Owner và một Reviewer**; reviewer kiểm tra, không tự nhận quyền merge khi chưa review.

| Module / workstream | Primary Owner | Reviewer | Week | Dependency / ranh giới |
|---|---|---|---|---|
| Requirement, scope, actor, permission, architecture, ADR | Thủy | Thiện | 2 | Audit repo, phản biện độc lập của Thiện |
| Database contract, ERD, API conventions và consistency gate | Thủy | Thiện | 2–7 | Input schema từ owner module; Thủy là integrator duy nhất |
| UI/UX contract, navigation và API client | Thủy | Thiện | 2–3 | API v1 và role matrix |
| Project skeleton, Program.cs, shared middleware/ProblemDetails | Thủy | Thiện | 3 | .NET SDK, architecture đã review |
| DbContext, migration coordination, shared enums/response | Thủy | Thiện | 3–7 | Owner module gửi mapping proposal; không sửa song song |
| Identity, JWT, policy, user/role | Thủy | Thiện | 3 | DB foundation, permission catalog |
| Department | Thiện | Thủy | 3 | Schema đã freeze, policy `departments.*` |
| Asset Type | Thiện | Thủy | 3 | Schema đã freeze, policy `asset-types.*` |
| Asset core API, search/filter/page và asset tests | Thủy | Thiện | 3 | Department/Asset Type lookup tối thiểu |
| Frontend shell/Login/Dashboard M1/Asset screens | Thủy | Thiện | 3 | Auth + asset API; static Bootstrap 5 cùng origin |
| Assignment API/history và UI | Thiện | Thủy | 4 | User/Department/Asset + filtered index |
| Maintenance API/history và UI | Thiện | Thủy | 4 | Asset status transition contract + technician lookup |
| Audit framework/endpoint và cross-module integration | Thủy | Thiện | 3–4 | Auth/correlation, domain event hooks |
| Software, License/allocation, license UI contract | Thiện | Thủy | 5 | User/Asset, key protector; Thủy hỗ trợ viết License page theo contract Thiện sở hữu |
| Key protection contract, security review | Thủy | Thiện | 5–7 | Secret source quyết định trước key persistence |
| Lifecycle/replacement rule và evaluation | Thiện | Thủy | 5 | Asset, maintenance metrics và cost source |
| Dashboard/report/budget API và UI | Thủy | Thiện | 6 | Assignment, maintenance, license, recommendation ổn định |
| Import/export Excel, UI và row validation | Thiện | Thủy | 7 | Asset service/report query + package review |
| API/UI end-to-end integration, regression, release review | Thủy | Thiện | 3–7 | Module owner đã chạy test gần ngày code |
| Documentation sync, status, roadmap, final demo | Thủy | Thiện | 2–7 | Owner module cung cấp thay đổi/evidence thật |

## Quy tắc ownership

- Primary Owner thiết kế phần module, viết implementation/test/UI phần mình, cập nhật tài liệu module và xử lý comment review. Reviewer kiểm API/BR/permission/schema/test và thử negative case; reviewer không sửa shared files trực tiếp khi owner đang thao tác.
- Thủy giữ quyền điều phối kiến trúc/API, dependency, frontend core và integration; Thiện có workstream độc lập với acceptance rõ, không bị biến thành người chỉ test.
- Đề xuất thay đổi entity/enum/DbContext/API contract từ Thiện được ghi vào PR/issue hoặc sync cuối ngày; Thủy tích hợp shared file sau khi thống nhất. Nếu Thủy bận, Thiện làm DTO/service/test trên contract đã freeze và gửi mapping patch nhỏ; không tạo migration cạnh tranh.
- Workload dự kiến theo [roadmap](roadmap.md) và 36 ngày: mỗi ngày Thủy 3 task M + 2 task S (7 giờ đại diện), Thiện 2 task M + 1 task S (4,5 giờ đại diện). Tổng 252h/162h, tỷ lệ **60,9% / 39,1%**; đây là estimate kế hoạch, không phải timesheet thực tế. Mỗi task được chia đủ nhỏ để không có XL.
- Nếu task/giờ thực tế đổi, cập nhật workload và dependency; không đẩy một task L/XL sang một ngày bận để giữ tỷ lệ trên giấy.

## Quyền sửa shared files theo tuần

| Week | Program.cs / middleware / package config | DbContext / migrations / shared enums / response | Frontend layout / API client | Ghi chú sync |
|---|---|---|---|---|
| 2 | Không sửa code; Thủy giữ tài liệu contract | Không tạo migration | Thủy thiết kế UI/UX | Thiện review schema/UX |
| 3 | Thủy | Thủy; Thiện chỉ gửi entity mapping proposal Department/Type | Thủy | Freeze DTO/route mỗi sáng; Thiện không sửa file của Thủy |
| 4 | Thủy | Thủy merge migration từ mapping Assignment/Maintenance của Thiện | Thủy shell; Thiện tạo page module trong file riêng | Duy nhất Thủy tạo migration được merge |
| 5 | Thủy | Thủy merge migration từ mapping License/Replacement của Thiện | Thủy API client; Thiện page module riêng | Contract key/masking được review trước merge |
| 6 | Thủy | Thủy nếu index migration cần thiết; Thiện góp query review | Thủy | Không chỉnh schema chỉ để tối ưu suy đoán |
| 7 | Thủy | Thủy nếu migration fix được review; Thiện không tạo song song | Thủy shell; Thiện import/export page riêng | Freeze schema trước regression/demo |

File `appsettings*.json` chỉ Thủy sửa cấu trúc key/config và không chứa secret; Thiện đọc qua Options contract. Khi cần đổi shared file khẩn, cả hai sync và chốt một người thực hiện một lần, reviewer kiểm diff trước merge. Mọi việc code/commit/merge ở trên đều **PLANNED**, không được thực hiện trong task lập kế hoạch này.
