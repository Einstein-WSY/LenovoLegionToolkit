// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1822:Mark members as static")] // 本项目大量 WMI/WinAPI 封装类以实例形式组织，但成员多为无状态访问，统一关闭 static 建议
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")] // 本应用明确只面向 Windows 平台发布，无需平台兼容性告警
