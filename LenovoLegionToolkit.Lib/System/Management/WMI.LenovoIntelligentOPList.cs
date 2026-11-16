using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_INTELLIGENT_OP_LIST 数据表封装：智能模式（Intelligent Mode）下按进程名配置的专属模式列表。
    /// </summary>
    public static class LenovoIntelligentOPList
    {
        /// <summary>
        /// 读取进程名到模式的映射。
        /// </summary>
        /// <returns>键为进程名、值为模式编号的字典；重复项只保留首条。</returns>
        public static async Task<Dictionary<string, int>> ReadAsync()
        {
            var result = await WMI.ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_INTELLIGENT_OP_LIST",
                pdc =>
                {
                    var processName = Convert.ToString(pdc["processname"].Value);
                    var mode = Convert.ToInt32(pdc["mode"].Value);
                    return (processName, mode);
                }).ConfigureAwait(false);
            // 同一进程名可能被固件重复配置，DistinctBy 保证转换为字典时不会抛重复键异常
            return result
                .OfType<(string, int)>()
                .DistinctBy(sm => sm.Item1)
                .ToDictionary(sm => sm.Item1, sm => sm.Item2);
        }
    }
}
