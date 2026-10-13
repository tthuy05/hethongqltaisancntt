# Thủy — nền bảo vệ license key, chuẩn bị trước Tuần 5

Ngày 14/10/2026. W5-THUY-D1-01/03 và D2-01: **LOCAL / UNDER REVIEW**, fallback protector/policy trước endpoint. M2/License API chưa nghiệm thu.

## Interface/persistence

Application: ILicenseKeyProtector, ILicenseEncryptionKeyRing, LicenseKeyEnvelope. Infrastructure: ConfiguredLicenseKeyRing/AesGcmLicenseKeyProtector. Không thêm package, DI, endpoint, migration hoặc startup configuration.

AES-256-GCM của .NET; nonce ngẫu nhiên 12 byte, tag 16 byte. Envelope gồm header `ITK + format-version 1` (4 bytes), nonce, tag, ciphertext. Associated data gắn purpose/format/license ID/key-version/last4. Tráo license hoặc sửa metadata/data bị từ chối. Schema S18 bytea/key_version/last4 đã đủ, giữ 18 bảng/41 FK.

Active version dùng encrypt, giữ version cũ để decrypt khi rotate. Không re-encrypt DB/xóa key cũ/random fallback. Operation dùng key copy 32 byte, xóa copy và plaintext byte buffer trong finally. Managed strings không bảo đảm xóa vật lý: không log/cache/giữ lâu. Ciphertext không serialize từ Envelope, ToString an toàn; metadata dùng MaskedKey, không decrypt.

Nguồn API: [AesGcm.Decrypt/associated data](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.decrypt?view=net-10.0), [tag size](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0053), [ZeroMemory](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory).

## Configuration chưa đặt secret thật

FromConfiguration đọc `LicenseKeyEncryption:ActiveVersion` và `LicenseKeyEncryption:Keys:<version>`. Version `[A-Za-z0-9_-]` 1..50, key Base64 đúng 32 bytes. Developer dùng user-secrets, deployment dùng environment/managed secret store; env naming `LicenseKeyEncryption__ActiveVersion` / `LicenseKeyEncryption__Keys__<version>`.

Không thêm secret/placeholder vào appsettings, không đổi Neon/JWT settings hoặc dùng password/JWT secret làm encryption key. Production source/escrow/recovery/rotation **PLANNED**, protector chưa đăng ký Program; startup hiện tại không cần secret mới.

## Validation và review cần thiết

Primitive giữ nguyên plaintext, strict UTF-8, không control characters, tối đa 4096 bytes, ít nhất 5 Unicode scalars. Last4 theo scalar khớp PostgreSQL char_length. Min5 tránh last4 lộ toàn key ngắn; **DTO format/length cần Thiện/owner review**, chưa đổi API/DB constraint.

Create allocate ID thật với key tuple null trong audited transaction; Protect theo ID, set ciphertext/last4/version đồng bộ, save/audit rồi commit. Không dùng ID tạm hoặc persist partial tuple. Update omitted key unchanged; explicit-null/clear behavior **PENDING REVIEW**.

## Policy và tích hợp

LicenseKeyAccessPolicy nhận CurrentUser refresh phía server. Read/manage/reveal là grants riêng, constants chưa vào active catalog/seed. Manage/reveal cần Admin membership; misgrant cho Manager/Support/custom group vẫn 403. Encrypted persistence fields không nhận từ client kể cả Admin. Reveal cần reason 1..1000 sau authorization.

Thiện tích hợp state/scope, decrypt sau policy, sanitized audit/no-store response. Primitive không triển khai endpoint; metadata chỉ maskedKey, không entity/envelope. Chưa có ciphertext DB persistence hoặc production reveal proof. Schema đã có từ S18, không tạo migration Week5 trùng.

## Verification

**34 focused unit PASS /0 FAIL /0 SKIP**, 19 protector +15 policy. Release 0 warning/error. TRX `test-results/week5-key-protection/license-security_net10.0_20261014014904.trx`.

Trước checkpoint: full offline **528 unit +157 integration PASS /0 FAIL**, 125 cloud SKIP; Release 0 warning/error. TRX `week5-security-checkpoint_net10.0_20261014015416.trx` và `...20261014015418.trx` cùng folder. 34 focused cases nằm trong tổng unit, không cộng hai lần. Frontend 119 PASS ở checkpoint c40db25 không chạy lại vì không đổi UI.

Roundtrip/nonce diversity/tamper/substitution/rotation/copy-disposal-zeroing/config/Unicode/limits và permission/reason/mass-assignment boundaries. Chỉ synthetic keys, không Neon/email/Render writes, không coi unit proof là reveal E2E.

Rerun trước có 1 FAIL do InlineData serialization thay unpaired surrogate; test sửa tạo surrogate runtime. Giữ TRX fail `license-key-protector-rerun_net10.0_20261014014632.trx`; final protector 19 PASS. Không xóa evidence lỗi.
