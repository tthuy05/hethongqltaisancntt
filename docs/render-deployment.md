# Đưa bản demo M1 lên Render

> 07/10/2026: chuẩn bị deploy **giao diện + ASP.NET Core API cùng domain**, database vẫn ở Neon. Có cấu hình deploy không đồng nghĩa website đã Live. Evidence cuối lượt ở mục 7; Render/secrets/HTTPS/DB public phải được kiểm tra riêng.

## 1. Phạm vi được giữ nguyên

- Không đổi endpoint/DTO, phân quyền, nghiệp vụ hoặc phân công Thủy/Thiện.
- Giữ 18 bảng / 41 quan hệ thiết kế, M1 vật lý 10 bảng / 19 FK và InitialM1 đã áp dụng.
- Startup/container/CI **không migration, seed, reset, drop hoặc truncate Neon**; Thủy vẫn điều phối migration riêng.
- `pnpm build` và API Development localhost dùng như trước. `pnpm start` chỉ là static preview local, không chạy backend.
- `artifacts/frontend-production` là bản API-only: không mock/Swagger, không bật demo qua `?demo=1`. Publish có marker riêng ngoài public root, không phục vụ source `wwwroot` trực tiếp.
- Giữ mốc **10/10/2026**, evidence Tuần 2–3 và review/rehearsal gates hiện hữu.

## 2. Sửa service Render đã tạo

Ảnh First Deploy 07/10 cho thấy `pnpm install` thất bại; dòng `approve-builds` gợi ý dependency script chưa được duyệt, chưa đủ log để xác định package. Docker/CI dùng `--frozen-lockfile --ignore-scripts`, giống cách cài toolchain frontend đã ghi trong repo; không bật toàn bộ dependency scripts.

Không cần tạo service thứ hai chỉ vì lần đầu bị lỗi:

1. Đợi cấu hình deploy này được push lên `main`; kiểm tra GitHub Actions của đúng commit.
2. Trong service hiện hữu: **Settings → Build → Source → Edit**, giữ repository, Branch `main`, đổi Runtime thành **Docker**. [Đổi runtime](https://render.com/docs/native-runtimes).
3. Root Directory để trống; Dockerfile Path `./Dockerfile`, Build Context `.`. Docker Command và Pre-deploy Command để trống; không ghi đè bằng `pnpm start`, `dotnet run` hoặc migration. [Docker trên Render](https://render.com/docs/docker).
4. Health Check Path `/health/live`: chỉ liveness, **không** chứng minh Neon kết nối. Swagger/OpenAPI/readiness vẫn Development-only, public trả 404.
5. Nhập Environment ở mục 3, lưu rồi deploy. Nếu đổi Runtime kích hoạt deploy trước khi có đủ secret, app sẽ từ chối startup; bổ sung secret rồi deploy lại.
6. Chọn Auto-Deploy **After CI Checks Pass**. Phải push/merge lên nhánh liên kết; commit chỉ nằm trên máy không kích hoạt. [Automatic deploys](https://render.com/docs/deploys).
7. Manual Deploy → Deploy latest commit; không chỉ chạy lại `f6bfec4` cũ. Kiểm tra status và URL public trước khi ghi Live.

`render.yaml` là Blueprint tương đương; **push YAML không tự đổi Settings của service tạo thủ công**. Không tạo thêm Blueprint/service trùng nếu service hiện hữu dùng được. Template chọn Free, không tạo Render database hoặc chứa secret. Free ngủ sau 15 phút không truy cập, mở lại khoảng một phút; mở thử trước báo cáo hoặc tự chọn paid compute nếu cần. [Giới hạn Free](https://render.com/docs/free).

## 3. Environment và secret

Nhập trực tiếp **Render → Environment**, không Dockerfile/Git/log/chat. [Environment variables](https://render.com/docs/configure-environment-variables).

| Key | Giá trị |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DOTNET_ENVIRONMENT` | `Production` |
| `Frontend__Enabled` | `true` |
| `Swagger__Enabled` | `false` |
| `ConnectionStrings__DefaultConnection` | Secret thật của **đúng Neon demo branch/database và runtime role**, ưu tiên pooled endpoint. URI Neon hoặc Npgsql format được parser hiện hữu xử lý với TLS VerifyFull/Channel Binding Require. Không dùng connection synthetic của smoke. |
| `Jwt__SigningKey` | Secret ngẫu nhiên riêng, tối thiểu 32 byte, giữ ổn định qua deploy. Không dùng key test/development. Blueprint sinh bằng `generateValue: true`; service thủ công cần nhập riêng. |

Backend dùng `PORT` khi hosting cung cấp, mặc định container nghe `8080`. `RENDER_EXTERNAL_HOSTNAME` được kiểm tra và thêm chính xác vào AllowedHosts, giữ localhost cho health. Custom domain cần thêm hostname thật vào `AllowedHosts`, phân cách `;`; không mặc định `*`. [Biến Render](https://render.com/docs/environment-variables).

Tạo JWT key trên Windows và sao chép clipboard, không in vào chat/terminal:

```powershell
$deployJwtBytes = New-Object byte[] 32
$deployJwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $deployJwtRng.GetBytes($deployJwtBytes)
    Set-Clipboard -Value ([Convert]::ToBase64String($deployJwtBytes))
} finally {
    $deployJwtRng.Dispose()
    [Array]::Clear($deployJwtBytes, 0, $deployJwtBytes.Length)
}
```

Dán vào `Jwt__SigningKey` trên Render rồi xóa clipboard nếu cần. Không đổi key mỗi commit vì sẽ vô hiệu JWT đang dùng. Push code không tự đổi mật khẩu DB; reload/JWT hết hạn cần login lại theo thiết kế.

### HTTPS và proxy

Render xử lý HTTPS tại edge. Forwarded headers chỉ được tin từ proxy IP/CIDR **đã xác minh và cấu hình**: `Hosting__KnownProxies__0` hoặc `Hosting__KnownNetworks__0`, thêm index nếu cần. Không đoán CIDR Render. Không dùng `0.0.0.0/0` hoặc `::/0`; không bật `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` hoặc đọc header IP không kiểm trust. Middleware chạy trước login limiter; không mở thêm chức năng audit metadata. [Microsoft proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Chưa cấu hình trust thì header từ proxy ngoài danh sách bị bỏ qua: website vẫn có HTTPS của Render nhưng login limiter có thể thấy IP proxy chung. Audit metadata nghiệp vụ giữ hiện trạng, không tuyên bố đã bổ sung ghi client IP. Kiểm proxy thật **PENDING**; test CIDR synthetic không phải chứng nhận Render. Không bật HTTPS redirect dựa vào header chưa tin cậy gây loop.

### Rủi ro hiện hữu

Owner credential trong Git Development vẫn **FAIL1 / UNRESOLVED**. `.dockerignore` loại file đó khỏi mọi build layer, publish loại config Development, nhưng **không vô hiệu credential đã lộ hoặc xóa lịch sử Git**. Cần phối hợp đổi credential và runtime role hạn chế quyền trước public với dữ liệu thật; không tự đổi password làm Thủy/Thiện mất kết nối.

Manager công khai trong README là ngoại lệ Development, có quyền sửa tài sản. Nên chọn Neon demo branch/database riêng, không dùng dữ liệu doanh nghiệp. Nếu chủ dự án chủ động dùng shared dev, public demo cũng sửa dữ liệu hai thành viên đang dùng. Tạo/chọn branch, grants/rotation là thao tác thủ công **PENDING**; lượt chuẩn bị này không tạo account, database, schema hoặc migration.

## 4. Kiểm tra local

```powershell
$env:ITAM_RUN_NEON_TESTS = '0'
pnpm install --frozen-lockfile --ignore-scripts
pnpm check
pnpm test
pnpm run build:production
dotnet restore ItAssetManagement.slnx --locked-mode
dotnet build ItAssetManagement.slnx --configuration Release --no-restore
dotnet test ItAssetManagement.slnx --configuration Release --no-build --no-restore
dotnet publish src/ItAssetManagement.Api/ItAssetManagement.Api.csproj --configuration Release --no-restore --output artifacts/production-publish -p:FrontendPublishDirectory="$PWD/artifacts/frontend-production" -p:UseAppHost=false
node scripts/verify-production-publish.mjs artifacts/production-publish
node scripts/smoke-production.mjs --publish artifacts/production-publish
docker build --tag itam-production-check .
node scripts/smoke-production.mjs --image itam-production-check
```

Cloud tests được SKIP có chủ đích khi opt-out, không được báo PASS. Chạy Neon tests cần fixture database cô lập/runbook riêng, không owner secret trong GitHub Actions hoặc DML trên shared development. Mỗi smoke dùng config **synthetic, không kết nối DB**, chỉ kiểm static/liveness/CSP/HEAD/host filtering/anonymous401/private404; không dùng fixture để demo thật. Container smoke dừng sau test, không dừng local `5080`.

## 5. CI và tự cập nhật

Workflow `.github/workflows/ci.yml`: push `main`, PR vào `main`, hoặc thủ công; quyền `contents: read`, Actions pin SHA, không Neon secret/deploy token. Gate: frontend checks/tests → API-only Production package → Release/offline tests → published HTTP smoke → Docker Linux build/smoke. Không auto-migration. 320 PASS lịch sử không thay kết quả mới hoặc biến cloud tests SKIP thành PASS.

Auto-deploy cần Git Provider connection, không chỉ paste public repo URL. Render cập nhật public link sau deploy thành công, không tức thì khi commit. Rollback application không rollback/reset Neon. GitHub Actions/Render chưa quan sát thì ghi PENDING, không suy từ việc có YAML.

## 6. Public checklist — PENDING

- [ ] HTTPS URL vào login, assets/fonts/hash navigation không lỗi404/CSP.
- [ ] Login tài khoản đúng demo DB, `/auth/me`, Dashboard/Asset list/search/detail/edit hoạt động.
- [ ] Manager/Support/anonymous đúng quyền server; không kết luận chỉ từ UI ẩn nút.
- [ ] Proxy/IP limiter/audit đúng trên Render thật.
- [ ] Rotate owner credential/runtime least privilege/demo isolation đã được xử lý hoặc xác nhận rõ rủi ro.
- [ ] Push an toàn → GitHub CI pass → Render dùng đúng commit mới.
- [ ] Rehearsal với Thiện/Mentor trước10/10, độc lập với publication.

## 7. Evidence — 07/10/2026

- Locked restore/Release build: PASS,0warnings/0errors; pnpm11.25.0 và package locks giữ nguyên.
- Unit: **165 PASS**. Integration offline/hosting: **54 PASS,78 SKIP,0 FAIL**; tổng132cases, không bật Neon opt-in. Previous320PASS là evidence06/10 riêng, không dùng thay lượt này.
- Node: **80 PASS,0 FAIL,0 SKIP** (57cũ+4frontendProduction+19publishguards). **29syntax +33CSP/DB-boundary PASS**.
- Production frontend **28assets**; publish **58files/28publicassets**, API-only graph đầy đủ, không Development config/mock/Swagger/DB-secret markers. Gói Development mock bị từ chối theo negative gate: PASS.
- Published Production HTTP smoke **27 PASS**: static/CSP/HEAD/Render exacthost200/unknownhost400, anonymousAPI401/private404. Config synthetic, không DB query/seed/migration; không chứng nhận login/data trên Neon thật.
- Preservation/source script6checks PASS, gitdiffcheck PASS; schema/config/Thiện-owned masters và assignment task rows giữ nguyên.
- Full multi-stage `docker build` từ source: **PASS**; final Linux image: **27 HTTP smoke PASS**, non-root UID1654, có production marker và không có config Development/mock/Swagger trong package. Portable-publish Linux smoke trước đó cũng27PASS, nhưng evidence full build là lượt kiểm riêng. Initial network `ECONNRESET` được giữ như lượt lỗi; retry không hạ TLS hoặc bật lifecycle scripts. Initial publish guard false-positive và NodefetchHost test false-positive được sửa: lần lượt kiểm Exists và dùng rawHTTP; production package/negativegate/27HTTP rerun PASS.
- GitHub CI trên remote, Render Live/HTTPS/proxy/secrets/Neon public connection và human/security review **PENDING**. Không suy các trạng thái này từ file YAML hoặc local test.
