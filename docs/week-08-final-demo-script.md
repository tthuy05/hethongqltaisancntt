# Kịch bản demo dự kiến và câu hỏi backend

Cập nhật20/10/2026. **PLANNED / REHEARSAL NOT VERIFIED.** Chỉ demo những module đã được approved/enabled trên target riêng có synthetic data; bỏ qua module chưa tích hợp, nêu rõ prepared thay vì giả kết quả.

## Thứ tự demo

1. Giới thiệu mục tiêu quản lý tài sản CNTT và phân công Thủy/Thiện.
2. Đăng nhập trên target demo; giải thích JWT ngắn hạn trong memory, RBAC kiểm lại backend.
3. Asset list/search/filter/paging/detail; create/edit synthetic record với sequence AST do server sinh.
4. Giải thích archive/retire là thao tác có audit/history/concurrency; không thay đổi dữ liệu thật chỉ để trình diễn.
5. User/permission matrix/Audit theo rollout đã duyệt; không đưa credentials/token/full key lên màn hình.
6. Assignment/Maintenance: chỉ chạy flow thực sau Thiện bàn giao và integration acceptance.
7. License/Replacement: hiện trình bày schema và helper tests; không gọi đó là module đã hoàn thiện.
8. Reporting và Excel: dùng isolated target/cờ explicit, known-count fixture; dry-run errors/valid preview, bounded export và cost masking. HTTP200 với committed=false là validation result.
9. Trình bày kết quả test, phân biệt offline PASS/cloud SKIP/isolated proof và phần chưa verified.
10. Nêu backlog, kế hoạch owner integration, production rollout và nghiệm thu chung.

## Câu hỏi & câu trả lời kỹ thuật

**Database thật đã đủ bảng chưa?** Baseline V1 đã có18tables/41relationships theo evidence riêng; phải kiểm target/revision/migration history trước deploy. Đủ schema không đồng nghĩa tất cả APIs/UI hoàn tất.

**Chống ghi đè thế nào?** row_version bytea16byte application-generated, EF concurrency token/If-Match. Khi phiên bản cũ thì409 và yêu cầu đọc lại; không dùng xmin hoặc tự ghi đè.

**Import có nguyên tử không?** Preview chỉ đọc. Commit validate lại refs/duplicates trong audited transaction và master-data lock; từng Asset/history có audit. Integration isolated đã kiểm failure sau INSERT rollback và concurrent duplicate. Sequence gaps được phép.

**Vì sao không tính chi phí từ lịch sử bảo trì?** History là snapshot, cộng lại có thể nhân đôi chi phí. Report dùng actual_cost của ticket/current policy và ghi rõ archived/date scope.

**Test xanh có nghĩa production sẵn sàng không?** Không. CI/build/fixture chỉ xác minh phạm vi đã chạy; còn owner integration, cloud cases, production revision/secret/grants và Mentor review.

**Vì sao API mới chưa gọi được?** Reporting/ImportExport mặc định disabled và constants chưa seed vào active grants. Cần rollout riêng sau review; không bật ngầm hoặc suy diễn từ liveness200.

**Khôi phục có chắc chắn không?** Dùng evidence logical backup/restore đúng thời điểm; không tuyên bố PITR sẵn sàng khi Console/restore window chưa xác minh.
