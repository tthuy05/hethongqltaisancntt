# Thủy — review transaction/capacity License

Ngày 14/10/2026. W5-THUY-D3-01: **DESIGN REVIEW PREPARED / OWNER ACCEPTANCE PENDING**. Không implement allocation service/API hoặc thay schema.

## Findings từ source hiện có

S18 có target XOR, FK, revoke time/actor checks, closed-before-archive, partial UNIQUE active user/asset và index license/revoked_at. AppDbContext bảo vệ target/owner/start fields bất biến và row đã revoked. Không có CHECK aggregate/trigger tự giới hạn COUNT allocations <= total_quantity. Unique target không ngăn hai target khác nhau chiếm seat cuối. Chưa có LicenseAllocationService để chứng minh tất cả writer tuân thủ capacity protocol; không coi schema 18 bảng là business workflow đã hoàn thiện.

## Protocol đề xuất cho Thiện review

Trong audited transaction, lấy row lock `software_licenses ... FOR UPDATE` rồi đọc lại license và COUNT active rows (`revoked_at_utc IS NULL AND NOT is_archived`). Tại isolation READ COMMITTED, chỉ tiếp tục insert sau lock/reread/capacity check. Mọi assign/revoke/transfer/quantity-update/archive phải dùng cùng lock; chỉ lock allocation row hoặc COUNT trước lock không đủ. Khi writer bỏ qua protocol, DB hiện tại không có aggregate guard cứu được over-allocation.

- Assign: validate quyền/target XOR và type/contract, target active và asset không archived/retired, license active/effective, count < total_quantity; insert + audit + Save/commit cùng transaction.
- Revoke: lock/reread, expected row-version, chỉ row active; giữ lịch sử, update revoked metadata + audit; không delete allocation.
- Transfer cùng license: lock/reread, expected version; close row cũ rồi insert target mới trong cùng transaction. COUNT sau close phải phản ánh seat được giải phóng, không tính row cũ lần hai. Duplicate target hoặc audit failure rollback cả close/insert. Không tuyên bố chuyển cross-license khi API contract chưa yêu cầu.
- Quantity decrease/archive: cùng capacity lock; quantity mới không thấp hơn active count, archive bị chặn khi còn active rows. Metadata/key update cũng cần version/transaction và review lock-order tương thích.
- Mixed User/Asset allocation cùng license đều tính 1 seat/row. Không loại row theo target type khỏi COUNT. VOLUME không bỏ qua capacity.
- Chốt global lock ordering trước code: identity coordinator nếu cần refresh grants/target user; asset locks nếu kiểm/ghi target; license locks và allocation rows. Rà tất cả asset/archive/user writers để tránh lock inversion. Đây là proposal, chưa có owner acceptance hoặc deadlock stress proof.
- Bounded lock/statement/cancellation timeouts; không retry mutation mù sau lỗi/mất kết nối. 23505/concurrency mapping cần mã domain thống nhất, không echo SQL/secret. Serialization retries/idempotency chưa có thì không nhận như feature đã implement.

## Tests cần có khi owner service sẵn sàng — PLANNED

Hai connection/context độc lập, barrier thật (không Task.Delay như bằng chứng race), 1 seat/2 targets: một commit thành công, một 409, active count=1, audit đúng outcome. Mixed targets, duplicate target UNIQUE, revoke→assign, transfer-at-full, quantity-decrease-vs-assign, expiry boundary, invalid/inactive target, stale version, rollback sau Save/audit failure; lock timeout/cancellation không làm mất seat/lịch sử. Chỉ isolated PostgreSQL, không reset/truncate shared Neon.

Existing schema/constraint tests không thay thế service concurrency tests. W5-D3-02/04/05 và capacity acceptance còn PENDING; chưa seed permission mới, chưa đổi active catalog. License key primitive/reveal tests dùng scope riêng, không chứng minh allocation capacity.

## Isolated protocol verification — 14/10

Release build 0 warnings/errors. **2 PostgreSQL integration PASS /0 FAIL/SKIP**, `test-results/week5-license-capacity/license-capacity-protocol_net10.0_20261014192114.trx`. Target `it_asset_management_full_schema_verify_fb_20261011_b21b`, guards name/connection; fixtures unique và retained, không migration/seed/shared write.

Final bounded rerun **2 PASS**, `license-capacity-bounded_net10.0_20261014192527.trx`: mỗi DB command timeout15s, contention scenario có cancellation budget45s. Không dùng timeout như bằng chứng contention; PostgreSQL 55P03 là assertion thực tế. Hai lần chạy là cùng2 scenarios, không cộng thành4 tests.

Hai DbContext/connection độc lập: transaction đầu lock/save user allocation ở license VOLUME một seat và giữ bằng TaskCompletionSource; transaction thứ hai FOR UPDATE NOWAIT trả PostgreSQL 55P03 trong khi lock còn giữ. Sau commit, thử asset target với lock+reread bị 409 fixture capacity, count=1 và chỉ1 assign audit. Đây chứng minh contention và reread, không phải stress test endpoint/automatic retry.

Transfer-at-full: close user row/save, insert asset row/save+audit, inject abort trước commit; active row/count/audit baseline giữ nguyên. Rerun transaction thành công giữ1 active seat/2 history rows và thêm đúng2 audits. Không chứng minh quyền, type/expiry/target validation hoặc allocation API; test fixture protocol không được đưa thành service của Thiện. Owner service race/HTTP/lock-order stress vẫn PLANNED.
