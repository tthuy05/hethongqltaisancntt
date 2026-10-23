# Release email mời kích hoạt tài khoản

Release thu gọn từ nhánh review, không merge toàn bộ PR#1. Phạm vi: Admin tạo tài khoản pending kèm nhóm quyền hiện có, email qua Brevo HTTPS, gửi lại lời mời, trang kích hoạt/tự đặt mật khẩu, chặn đăng nhập trước kích hoạt; giữ lifecycle/authorization guards của giao diện.

Migration bắt buộc: `20261011103054_LecturerFeedbackIdentityAndAssetCodes`, giữ nguyên file đã review. Do migration này cũng tạo sequence và trigger cấm đổi AssetCode, release phải gồm generator/validation/form mã tài sản tự sinh để API cũ không xung đột trigger. Không thay mã hoặc password tài khoản cũ; không seed permissions hoặc dữ liệu mẫu.

Không bao gồm role-definition/matrix write API, Audit UI redesign, Maintenance/Reporting/Import/Export/License modules hoặc source WIP khác. Assignment vẫn disabled. Không chỉnh InitialM1/B/S18, database/JWT connection configuration hoặc `appsettings.Development.json`. API key chỉ cấu hình trên Render, không đưa vào commit.

Triển khai theo gate: backend cũ + cổng bảo trì → xác minh CI/SHA/gate/drain và fresh backup/restore → reviewed SQL có transaction/advisory lock/baseline guards/timeouts → verify database → deploy release dưới gate → mở lại traffic và smoke email. Không auto-migration/seed lúc startup, không Down/drop/reset hoặc retry mù khi commit status chưa rõ.

Kiểm tra local trước publication: Release build sạch; 329 unit, 148 offline integration PASS (110 cloud SKIP), 129 frontend PASS; 38 isolated HTTP integration PASS trên database mới riêng. Backup/actual restore + migration rehearsal PASS; kết quả này không chứng nhận production đã deploy hoặc email đã tới inbox. CI đúng SHA, database postflight, Render Live SHA và smoke production phải được ghi riêng sau rollout.
