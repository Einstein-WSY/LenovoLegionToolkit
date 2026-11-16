using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_DISCRETE_DATA 数据表封装：描述各离散型能力的可选值集合（用于生成下拉框/单选项）。
    /// </summary>
    public static class LenovoDiscreteData
    {
        /// <summary>
        /// 读取各离散能力的取值项。
        /// </summary>
        /// <returns>离散能力集合，每项为“能力 ID + 具体取值”。</returns>
        public static Task<IEnumerable<DiscreteCapability>> ReadAsync() => WMI.ReadAsync("root\\WMI",
            $"SELECT * FROM LENOVO_DISCRETE_DATA",
            pdc =>
            {
                var id = (CapabilityID)Convert.ToInt32(pdc["IDs"].Value);
                var value = Convert.ToInt32(pdc["Value"].Value);
                return new DiscreteCapability(id, value);
            });
    }
}
