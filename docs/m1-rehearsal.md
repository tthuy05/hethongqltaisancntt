# Thủy — kiểm tra hồi quy và chuẩn bị demo M1

Ngày kiểm tra: **08/10/2026**, Asia/Saigon. Mốc demo **10/10/2026** giữ nguyên.

**Kết luận:** phần code M1 của Thủy đã có; lượt này sửa lỗi vòng đời phiên đăng nhập, kiểm tra bản Render đang chạy và chuẩn bị checklist diễn tập. Không mở Assignment/Maintenance hoặc làm lại Department/Asset Type của Thiện. Kiểm chứng kỹ thuật không thay thế review của Thiện, diễn tập chung hoặc nghiệm thu Mentor.

## 1. Đối chiếu nhiệm vụ

Các dòng kế hoạch gốc, ID, người phụ trách, estimate và evidence cũ được giữ nguyên. Bảng này là execution addendum, không đánh dấu toàn bộ Tuần 3 DONE.

| Nhiệm vụ Thủy | Kết quả / phần còn lại |
|---|---|
| W3-THUY-D5-03 | Regression offline và browser đã kiểm tra; sửa blocker phiên đăng nhập. Rehearsal chung với Thiện **PENDING**. Không chạy lại toàn bộ flow ghi dữ liệu trên Render. |
| W3-THUY-D5-04 | Có ảnh browser Render/fixture và kết quả test thật của lượt 08/10. Không thay ảnh hoặc TRX lịch sử. |
| W3-THUY-D5-05 | Known issue, README, trạng thái và checklist được cập nhật ở lượt này. |
| W3-THUY-D6-01 | Locked restore, Release build/test/publish local PASS. Clean migration/live isolated Neon suite không chạy lại; reviewed PR/human integration gate **PENDING**. CI của commit mới cần kiểm riêng sau push. |
| W3-THUY-D6-02 | Smoke Production local PASS; public Manager/Support reads và UI guard đã kiểm. Public create/edit/archive, interactive Swagger và kiểm DB rows mới không chạy. Evidence rộng hơn ngày 03/10 vẫn giữ trong [báo cáo Tuần 2–3](week-02-03-completion.md). |
| W3-THUY-D6-03 | Diễn tập chung và Mentor demo/acceptance ngày 10/10 **PENDING**. |
| W3-THUY-D6-04 | Tài liệu và bằng chứng cập nhật; không tự ghi nhận nghiệm thu. |
| W3-THUY-D6-05 | Kiểm diff/preservation và secret scan thực tế; owner credential finding **FAIL — 1 / UNRESOLVED**. Post-demo audit **PENDING**. |

Week 2 vẫn có **30 deliverable Thủy DOCUMENTED — REVIEW PENDING**; previous **21 documentation checks PASS** là evidence lịch sử, không chạy lại hoặc xóa. Logical baseline **18 tables / 41 relationships**, M1 physical baseline **10 tables / 19 FKs**, migration `20261002151601_InitialM1` không thay đổi.

## 2. Lỗi đã sửa

- Khi form Create/Edit hoặc phần tải detail trong Asset List tự xử lý lỗi 401, token đã bị xóa nhưng shell đăng nhập cũ có thể còn trên màn hình. Adapter nay phát sự kiện invalidation; app hủy render cũ, đóng dialog và hiện Login, giữ đường dẫn/query cần trở lại.
- Phiên hết hạn khi người dùng để trang đứng yên nay cũng được xử lý bằng timer. Login/logout chủ động không phát thông báo invalidation; timer cũ được hủy khi đổi phiên.
- Request `/auth/me` hoặc lỗi mạng thuộc lần đăng nhập cũ không được xóa phiên đăng nhập mới. Callback cũ của trang Người dùng không được chuyển trang mới về Login hoặc hiển thị feedback vào trang đã bị tháo.
- 403 và lỗi mạng của phiên hiện tại vẫn giữ phiên; feedback lỗi/concurrency vẫn hoạt động. Không lưu JWT vào localStorage/sessionStorage, không thêm refresh-token API hoặc đổi Auth contract.

Các test mới chạy code app/page thật với DOM harness tối thiểu và fetch/timer giả lập, không chỉ kiểm chuỗi source. Browser fixture riêng dùng bản frontend Production mới, chỉ trả response synthetic và không kết nối Neon.

## 3. Bản Render đã kiểm tra

URL public: [hethongqltaisancntt.onrender.com](https://hethongqltaisancntt.onrender.com/).

Lượt browser trên Render được thực hiện **trước khi publish bản sửa phiên của lượt này**; không dùng kết quả đó để chứng nhận bản sửa mới đã deploy. Không lưu thay đổi tài sản, người dùng hoặc danh mục trên public database.

| Kiểm tra thực tế | Kết quả |
|---|---|
| HTTPS mở Login, assets/font local | PASS |
| Manager login → Dashboard API thật | PASS; 26 tài sản tại thời điểm kiểm tra (23 InStock, 3 Retired) |
| Asset List, tìm `M1-DEMO-20261003-001` | PASS; 1 kết quả; mở detail `Demo Laptop 01`, ID 4 |
| Manager detail và mở Edit | PASS; có chi phí và quyền sửa; chỉ tải form, không lưu |
| Edit ở viewport 320 × 900 | PASS; document/body rộng 305px, không tràn ngang document |
| Support login → Dashboard/List/Detail | PASS; không có nút tạo/sửa, không có nhãn chi phí |
| Support truy cập trực tiếp `/#/assets/4/edit` | PASS UI guard: “Bạn không có quyền sửa tài sản.”; không thay thế test HTTP 403 phía server |
| Logout | PASS; trở về Login |
| Bản sửa mới: Create submit nhận 401 synthetic | PASS trên browser loopback; Login thay shell cũ, giữ `#/assets/new`; không ghi DB |

Ảnh local nằm tại `artifacts/ui-evidence/m1-readiness-20261008/`:

- `render-manager-dashboard.jpg`
- `render-manager-search.jpg`
- `render-manager-edit-320.jpg`
- `render-support-detail.jpg`
- `render-support-edit-denied.jpg`
- `synthetic-create-401-login.jpg`

Artifacts, TRX và server fixture thử nghiệm được Git-ignore; không đưa secret/token vào tài liệu hoặc commit. Browser autocomplete có thể điền lại thông tin đã lưu ở Login; bằng chứng không khẳng định browser đã xóa dữ liệu trong password manager.

## 4. Kiểm chứng local 08/10

| Lệnh / nhóm kiểm tra | Kết quả thực tế |
|---|---|
| `dotnet restore ItAssetManagement.slnx --locked-mode` | PASS |
| `dotnet build ItAssetManagement.slnx --configuration Release --no-restore` | PASS; 0 warnings / 0 errors |
| Unit .NET Release | **165 PASS / 0 FAIL** |
| Integration .NET Release, `ITAM_RUN_NEON_TESTS=0` | **54 PASS / 0 FAIL / 78 SKIP**; cloud tests cố ý không chạy |
| `node --test tests/*.test.mjs` | **109 PASS / 0 FAIL / 0 SKIP**; baseline 80 + 29 regression tests mới |
| `node scripts/frontend-check.mjs` | **29 syntax + 33 CSP/database-boundary checks PASS** |
| Development và Production frontend build | PASS; Production có 28 API-only assets |
| Release Production publish | PASS |
| `node scripts/verify-production-publish.mjs artifacts/production-publish` | PASS; 58 files / 28 public assets, không config Development/mock/Swagger/DB-secret markers |
| `node scripts/smoke-production.mjs --publish artifacts/production-publish` | **27 HTTP checks PASS**; synthetic settings, không query DB |
| `scripts/check-week02-03.ps1` | **6 checks PASS**; 96 original task lines/matrix, links, schema/version và milestone/review gates preserved |
| `scripts/check-user-admin-ui.ps1` | **7 checks PASS**; contract/schema/config/catalog/user UI boundaries preserved |
| `scripts/check-neon-secrets.ps1` | **FAIL — 1 existing finding**: `src/ItAssetManagement.Api/appsettings.Development.json`; không phát sinh finding mới |
| Linux Docker smoke local | **NOT RUN**; Docker Desktop Linux engine không hoạt động. Không đổi cài đặt máy để bật; CI container check phải xác minh riêng |

Tổng automated test ở lượt này: **328 PASS / 0 FAIL / 78 SKIP** (165 + 54 + 109). Không cộng browser/source/package smoke vào tổng test, không dùng historical 320 PASS thay cho kết quả mới, không tuyên bố cloud suite PASS.

Lệnh test .NET lưu TRX riêng:

```powershell
$env:ITAM_RUN_NEON_TESTS = '0'
dotnet test ItAssetManagement.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=m1-readiness-20261008' --results-directory artifacts/ui-evidence/m1-readiness-20261008
```

Không tạo migration, seed, drop/reset/truncate, đổi schema, password, role/grants hoặc cấu hình Render trong lượt này. Browser login public dùng account demo đã công khai theo yêu cầu của chủ dự án; login/audit thông thường có thể được server ghi nhận.

## 5. Diễn tập với Thiện trước 10/10

Thủy dẫn flow/UI và ghi issue; Thiện review độc lập API/danh mục/phần được phân công. Chuẩn bị đúng commit/URL deploy và **test target được nhóm đồng ý** trước thao tác ghi. Không dùng shared development database để drop/reset hoặc chạy automated destructive tests.

1. Kiểm CI đúng commit, website vừa deploy và `/health/live`; xác nhận Neon target dùng demo data, không phải dữ liệu doanh nghiệp.
2. Manager: Login → Dashboard → List → search/filter/page → detail; đối chiếu đúng tài sản, tổng và quyền xem chi phí.
3. Trên test target đã thống nhất: tạo tài sản bằng mã demo riêng, mở detail, sửa một trường rồi tìm lại. Ghi ID/mã để nhóm quản lý, không archive bản ghi của người khác. Chụp kết quả thật, che token/secret.
4. Kiểm validation 400; hai phiên chỉnh cùng một demo record để kiểm 409 và không tự ghi đè. Kiểm 401 với phiên hết hạn/invalidated; login lại quay về đúng trang. Không sửa secret/JWT config thật để giả lập lỗi.
5. Support: không thấy chi phí, không tạo/sửa; kiểm HTTP 403 bằng công cụ API trên test target, không chỉ chụp nút ẩn.
6. Admin: kiểm tra reads của danh mục, Người dùng và Nhật ký thao tác nếu trình bày phần làm sớm. Không đổi quyền/password hoặc quản trị dữ liệu chung chỉ để chụp evidence. Department/Asset Type vẫn Thiện review/tiếp quản.
7. Kiểm desktop/tablet/mobile (1280/768/320), menu, lỗi mạng, refresh cần login lại theo thiết kế JWT in-memory. Scope màn hình của lượt 08/10 chỉ Edit 320px; matrix rộng hơn 03/10 là historical evidence, cần kiểm lại nếu UI đã đổi.
8. Ghi PASS/FAIL, issue/owner/ETA; freeze M1. Demo Mentor 10/10 và nghiệm thu là bước của nhóm, chưa được agent đánh dấu hoàn tất.

Nếu dùng bản public để demo, account demo đã công khai cho cả 3 role là một **rủi ro được chủ dự án chấp nhận, không phải security PASS**. Owner credential trong Git, runtime least privilege, demo database isolation và real proxy/client-IP verification còn **UNRESOLVED/PENDING**. Không gửi password/connection string vào chat để giải quyết các gate này.

## 6. Phần tiếp theo

Ưu tiên Thủy hiện tại: **review cùng Thiện → chạy checklist diễn tập → chốt evidence/issue → demo 10/10**. Không còn lý do làm lại code M1 đã có; status-history UI ngoài tiêu chí M1 và workflow Assignment/Maintenance vẫn PLANNED theo roadmap/gate riêng.

Publication của bản sửa được xác minh bằng Git/CI thực tế; push không đồng nghĩa Render đã deploy thành công hoặc Mentor đã nghiệm thu. [Kế hoạch gốc](weekly/week-03.md), [runbook Render](render-deployment.md), [báo cáo M1 cũ](m1-backend-handoff.md) và [completion matrix](week-02-03-completion.md) được giữ làm bằng chứng có ngày.
