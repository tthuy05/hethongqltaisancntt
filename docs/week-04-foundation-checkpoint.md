# Checkpoint nền tích hợp Tuần 4 — Thủy

Ngày **14/10/2026**. Phạm vi: chuẩn bị shared integration cho module Thiện. Checkpoint trên nhánh `codex/lecturer-feedback`, hậu duệ `5cbedf1`; không merge/deploy main hoặc thay shared database.

## Nội dung bàn giao

1. [Status/history contract](week-04-shared-status-handoff.md): default-deny transition matrix, UTC/actor/correlation, retire giữ guard cũ.
2. [Scope/cost contract](week-04-maintenance-access-handoff.md): assigned-only fallback, cost field presence kể cả null/0, queue chưa chốt nên deny.
3. [Ticket/history audit](week-04-maintenance-audit-handoff.md): audit cả hai business entities, before snapshot bất biến, metadata có type checks và không số tiền/free text.
4. [Cross-module protocol](week-04-cross-module-verification.md): AssignmentService thật với shared Maintenance fixture/helpers, rollback và kết quả trạng thái.
5. [API client](week-04-api-client-handoff.md): bốn routes Assignment hiện có và user lookup dùng chung phiên/error transport.

Maintenance Service/Controller/DTO/UI, transfer/enriched Assignment/history API, queue/custom-group policy review và endpoint integration vẫn **PLANNED / phụ thuộc Thiện**. Không nhận cả Week 4/M2 DONE. Helper source không bật feature flag, đăng ký permission catalog mới hoặc seed.

## Kiểm tra trước checkpoint

- Release build: **0 warning /0 error**.
- Full offline .NET: **494 unit +157 integration PASS /0 FAIL**, 125 cloud/explicit-opt-in SKIP. TRX `test-results/week4-checkpoint/week4-checkpoint-offline_net10.0_20261014013844.trx` và `...20261014013845.trx`.
- Full frontend: **119 PASS /0 FAIL /0 SKIP**, gồm client, session, UI và production package tests.
- Source checks lượt client: 32 syntax /36 CSP-boundary PASS.
- PostgreSQL evidence trước checkpoint: 5 shared-status affected +3 ticket/history audit +5 cross-module cases PASS trên isolated target; không cộng vào offline PASS, không chạy lại cloud suite hoặc ghi shared khi publication.
- CI/Linux Docker của checkpoint mới cần xác minh theo đúng head SHA sau push; kết quả `5cbedf1` là checkpoint cũ.

## Git scope

Chỉ stage shared code/tests và tài liệu Tuần 4 đã review; không dùng `git add .`. Ba file báo cáo trước đó của người dùng nằm ngoài checkpoint: `Kich_Ban_Bao_Cao_Tien_Do_Backend.docx`, `docs/kich-ban-bao-cao-backend.md`, `scripts/generate_report_docx.py`. Không đưa test-results/artifacts hoặc thay đổi connection/configuration vào commit.

PR #1 giữ DRAFT. Nó còn chứa lecturer-feedback migration chưa apply shared; vì vậy **BLOCK MERGE/DEPLOY** cho đến khi hoàn tất coordinated rollout đã được duyệt. Việc publish code lên nhánh để review không phải quyết định chạy migration hoặc mở Assignment API.

Thiện có thể lấy checkpoint branch, review các shared interfaces và dùng vào module của mình; không tự chạy EF update/seed trên neondb hoặc copy scaffold complete permission trái API contract.
