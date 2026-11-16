namespace LenovoLegionToolkit.Lib;

/// <summary>
/// 表示实现类拥有对用户友好的显示名称，用于 UI 列表展示。
/// </summary>
public interface IDisplayName
{
    /// <summary>
    /// 获取用于界面展示的名称（与枚举内部标识符解耦，便于本地化）。
    /// </summary>
    string DisplayName { get; }
}
