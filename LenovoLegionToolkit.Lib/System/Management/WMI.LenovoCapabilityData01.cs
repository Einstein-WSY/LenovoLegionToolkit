using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_CAPABILITY_DATA_01 数据表封装：描述各数值型能力的取值范围（用于生成滑块等控件）。
    /// </summary>
    public static class LenovoCapabilityData01
    {
        /// <summary>
        /// 读取各能力的取值范围信息。
        /// </summary>
        /// <returns>范围能力集合，含默认值、最小值、最大值与步进。</returns>
        public static Task<IEnumerable<RangeCapability>> ReadAsync() => WMI.ReadAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CAPABILITY_DATA_01",
            pdc =>
            {
                var id = Convert.ToInt32(pdc["IDs"].Value);
                var defaultValue = Convert.ToInt32(pdc["DefaultValue"].Value);
                var min = Convert.ToInt32(pdc["MinValue"].Value);
                var max = Convert.ToInt32(pdc["MaxValue"].Value);
                var step = Convert.ToInt32(pdc["Step"].Value);
                return new RangeCapability((CapabilityID)id, defaultValue, min, max, step);
            });
    }
}
