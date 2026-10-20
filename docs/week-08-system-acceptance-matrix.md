# Ma trận nghiệm thu — bản nháp

Cập nhật20/10/2026. **UNDER REVIEW / NOT FULL PASS.** Tiêu chí ở đây là gate nghiệm thu; không coi test helper là module acceptance.

| Phạm vi | Evidence hiện có | Gate còn thiếu |
|---|---|---|
| Week2 thiết kế | handoff30task, schema baseline18/41 và tài liệu | Review độc lập theo handoff |
| Auth/Asset/User/RBAC/Audit | source/test/handoff, CI nhánh review | Shared lecturer-feedback migration/production rollout, email inbox E2E, independent browser acceptance |
| Assignment/Maintenance | mapping/schema/guard/selected isolated tests | Thiện owner module/review, full workflow HTTP/UI/race acceptance |
| Software/License/Replacement | crypto/capacity/metrics/policy/query helpers | Owner APIs/UI, allocation concurrency/transfer/key reveal/evaluator one-current transaction |
| Reporting | local aggregates/permission/query tests, isolated DB comparisons | Owner fixtures/broad accuracy, EXPLAIN/load, browser responsiveness, approved grants/rollout |
| Import/Export | bounded parser, typed row errors, read-only preview, isolated rollback/race, transport tests | Thiện owner review/UI, workbook interoperability/broad export-report parity, approved grants/rollout |
| Production | historical Render handoff và reviewed branch CI | Xác minh revision/deploy/smoke sau từng rollout được duyệt |
| Recovery | historical logical backup/restore evidence | Fresh backup theo maintenance plan; PITR chưa xác minh không được coi PASS |
| Security/performance | focused automated tests, release artifact checks | Remaining findings, real deployment configuration/proxy/access, load/capacity testing và review độc lập |
| Final handover | draft report/demo plan | Joint rehearsal, kết quả actual, Mentor sign-off |

Số test chạy mới nhất tại [review20/10](import-reporting-review-2026-10-20.md). Không có coverage percentage, SLA guarantee hoặc 100% project completion từ matrix này.
