namespace ItAssetManagement.Application.Mvp;

public static class AuditActionCatalog
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Auth
        ["auth.login"] = "Đăng nhập hệ thống",

        // Users
        ["users.invite"] = "Mời người dùng",
        ["users.invitation.resend"] = "Gửi lại lời mời tham gia",
        ["users.invitation.email"] = "Gửi email lời mời",
        ["users.activate"] = "Kích hoạt tài khoản",
        ["users.update"] = "Cập nhật thông tin người dùng",
        ["users.status.change"] = "Thay đổi trạng thái tài khoản",
        ["users.roles.add"] = "Gán vai trò cho người dùng",
        ["users.roles.remove"] = "Gỡ vai trò của người dùng",
        ["users.roles.replace"] = "Thay đổi danh sách vai trò",
        ["users.permissions.assign"] = "Cấp quyền tùy chỉnh",
        ["users.permissions.reset"] = "Đặt lại quyền về mặc định",
        ["users.permissions.replace"] = "Cập nhật quyền tùy chỉnh",

        // Roles
        ["roles.create"] = "Tạo vai trò mới",
        ["roles.update"] = "Cập nhật thông tin vai trò",
        ["roles.status.change"] = "Thay đổi trạng thái vai trò",
        ["roles.permissions.add"] = "Thêm quyền vào vai trò",
        ["roles.permissions.remove"] = "Xóa quyền khỏi vai trò",
        ["roles.permissions.replace"] = "Cập nhật danh sách quyền của vai trò",

        // Departments
        ["departments.create"] = "Tạo phòng ban",
        ["departments.update"] = "Cập nhật phòng ban",
        ["departments.status.change"] = "Thay đổi trạng thái phòng ban",

        // Asset Types
        ["asset-types.create"] = "Tạo loại tài sản",
        ["asset-types.update"] = "Cập nhật loại tài sản",
        ["asset-types.status.change"] = "Thay đổi trạng thái loại tài sản",

        // Assets
        ["assets.create"] = "Tạo mới tài sản",
        ["assets.update"] = "Cập nhật thông tin tài sản",
        ["assets.archive"] = "Lưu trữ tài sản",
        ["assets.status.change"] = "Thay đổi trạng thái tài sản",
        ["assets.import"] = "Nhập dữ liệu tài sản từ Excel",
        ["asset.create"] = "Tạo mới tài sản",
        ["asset.update"] = "Cập nhật thông tin tài sản",
        ["asset.archive"] = "Lưu trữ tài sản",
        ["asset.status.change"] = "Thay đổi trạng thái tài sản",

        // Assignments
        ["assignments.assign"] = "Cấp phát tài sản",
        ["assignments.return"] = "Thu hồi tài sản",
        ["assignments.transfer"] = "Điều chuyển tài sản",

        // Maintenance
        ["maintenance.create"] = "Tạo phiếu bảo trì",
        ["maintenance.update"] = "Cập nhật phiếu bảo trì",
        ["maintenance.status.change"] = "Chuyển trạng thái bảo trì",
        ["maintenance.assign"] = "Phân công xử lý bảo trì",
        ["maintenance.comment"] = "Thêm bình luận bảo trì",
        ["maintenance.cost.update"] = "Cập nhật chi phí bảo trì",
        ["maintenance.resolve"] = "Hoàn thành xử lý bảo trì",
        ["maintenance.fail"] = "Đánh dấu bảo trì thất bại",
        ["maintenance.cancel"] = "Hủy phiếu bảo trì",
        ["maintenance.asset.status"] = "Cập nhật trạng thái tài sản khi bảo trì",

        // Software
        ["software.create"] = "Tạo phần mềm",
        ["software.update"] = "Cập nhật thông tin phần mềm",
        ["software.archive"] = "Lưu trữ phần mềm",

        // Licenses
        ["licenses.create"] = "Tạo bản quyền phần mềm",
        ["licenses.update"] = "Cập nhật bản quyền phần mềm",
        ["licenses.archive"] = "Lưu trữ bản quyền phần mềm",
        ["license.key.reveal"] = "Xem khóa bản quyền (Key)",

        // License Assignments (License Allocation)
        ["license-assignments.assign"] = "Phân bổ seat bản quyền",
        ["license-assignments.revoke"] = "Thu hồi seat bản quyền",

        // Audit
        ["audit.view"] = "Xem nhật ký thao tác",

        // Technical / Development actions
        ["development.seed"] = "Khởi tạo dữ liệu phát triển ban đầu",
        ["development.demo.seed"] = "Khởi tạo dữ liệu thử nghiệm (demo)",
        ["development.demo.retire"] = "Ngừng sử dụng tài sản thử nghiệm (demo)",
        ["development.maintenance-permissions.seed"] = "Khởi tạo quyền bảo trì (phát triển)",
        ["development.license-permissions.seed"] = "Khởi tạo quyền bản quyền (phát triển)",
        ["development.software-permissions.seed"] = "Khởi tạo quyền phần mềm (phát triển)",
        ["development.license-allocation-permissions.seed"] = "Khởi tạo quyền phân bổ bản quyền (phát triển)",
        ["development.assignment-permissions.seed"] = "Khởi tạo quyền cấp phát (phát triển)",
        ["development.audit-read.seed"] = "Khởi tạo quyền xem nhật ký (phát triển)",
        ["development.role-catalog.seed"] = "Khởi tạo danh mục vai trò (phát triển)",
        ["development.import-export-permissions.seed"] = "Khởi tạo quyền xuất nhập (phát triển)"
    };

    public static readonly string[] BusinessActions =
    [
        "auth.login",
        "users.invite", "users.invitation.resend", "users.invitation.email", "users.activate",
        "users.update", "users.status.change", "users.roles.add", "users.roles.remove", "users.roles.replace",
        "users.permissions.assign", "users.permissions.reset", "users.permissions.replace",
        "roles.create", "roles.update", "roles.status.change",
        "roles.permissions.add", "roles.permissions.remove", "roles.permissions.replace",
        "departments.create", "departments.update", "departments.status.change",
        "asset-types.create", "asset-types.update", "asset-types.status.change",
        "assets.create", "assets.update", "assets.archive", "assets.status.change", "assets.import",
        "asset.create", "asset.update", "asset.archive", "asset.status.change",
        "assignments.assign", "assignments.return", "assignments.transfer",
        "maintenance.create", "maintenance.update", "maintenance.status.change", "maintenance.assign",
        "maintenance.comment", "maintenance.cost.update", "maintenance.resolve", "maintenance.fail",
        "maintenance.cancel", "maintenance.asset.status",
        "software.create", "software.update", "software.archive",
        "licenses.create", "licenses.update", "licenses.archive", "license.key.reveal",
        "license-assignments.assign", "license-assignments.revoke",
        "audit.view"
    ];

    public static string? GetDescription(string? action)
    {
        if (action == null) return null;
        return Descriptions.TryGetValue(action, out var desc) ? desc : null;
    }

    public static bool IsBusinessAction(string? action)
    {
        if (action == null) return false;
        return BusinessActions.Contains(action, StringComparer.OrdinalIgnoreCase);
    }
}
