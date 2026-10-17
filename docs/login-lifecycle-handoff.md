# Thủy — vòng đời đăng nhập, 16/10/2026

## Phạm vi

Trang đăng nhập dùng validator chung, giới hạn mật khẩu 256 ký tự như backend; không áp minimum kích hoạt tài khoản lên mật khẩu đăng nhập cũ và không trim mật khẩu. Chỉ nhận return path nội bộ. Khóa sửa input/toggle khi đang gửi, single-flight; xóa password input và bỏ reference của trang trong finally, kể cả khi trang đã detach. Không tuyên bố xóa bảo đảm immutable strings khỏi bộ nhớ.

Phản hồi đến muộn không chuyển trang khi hash/DOM đã đổi. UI không tự logout trong catch: adapter vẫn chịu trách nhiệm invalidation khi 401 hoặc /me sai contract. Lỗi mạng/503 của /me không được dùng để đăng xuất nhầm phiên mới hoặc kết luận token bị thu hồi; trang báo chưa hoàn tất xác minh và không tự chuyển tiếp. Không sửa backend Auth/JWT, session storage hoặc transport revision protocol.

## Kiểm chứng

- 11 tests mới: legacy password/max length, single-flight/exact DTO, validation trước network, detach/hash change, lỗi cũ không logout phiên mới, thông báo lỗi generic, internal return path, actual adapter 401/malformed me, transient me503 và explicit demo role.
- Full frontend: **226 PASS / 0 FAIL / 0 SKIP**; **43 syntax / 47 CSP/database-boundary checks PASS**.
- Browser static synthetic fixture đã kiểm ngày 15/10: lỗi xác minh hiển thị rõ, hash vẫn login, password length 0/type password, không echo internal error. Ảnh local ignored: `test-results/week4-asset-cost-access/login-verification-error-20261015.jpg`, đã xem. Không gọi API, gửi email hoặc ghi database; không phải production login E2E.
- Backend không thay đổi; kết quả parent 8fa84c1 là 583 unit/158 offline integration PASS, 140 cloud SKIP. CI/Linux của checkpoint mới cần xác minh riêng, không suy từ parent.

## Gate

Source chỉ trên review branch, PR vẫn DRAFT/BLOCK MERGE. Không sửa configuration/connection string, migrations, shared Neon, Render hoặc module của Thiện. Ba report WIP được giữ nguyên. Việc merge/deploy vẫn cần phê duyệt riêng.
