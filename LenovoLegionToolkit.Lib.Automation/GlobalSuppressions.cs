// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// 本项目仅面向 Windows（WPF）运行，无需逐处标注平台兼容性；
// 同时部分成员刻意保留实例形式以便通过 DI 解析或后续扩展，故集中屏蔽对应分析器告警。
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
