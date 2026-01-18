namespace PCL.Core.Account.OAuth;

public enum AuthStep
{
    Initializing,    // 初始化中
    PendingUser,     // 等待用户操作（已获得 OAuth URL）
    GettingCode,     // 用户已授权，获取授权码
    Success,         // 成功
    Error            // 出错（如过期、拒绝）
}