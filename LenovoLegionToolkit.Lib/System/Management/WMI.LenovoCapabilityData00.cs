using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_CAPABILITY_DATA_00 数据表封装：列出机型支持的能力 ID 集合。
    /// </summary>
    public static class LenovoCapabilityData00
    {
        /// <summary>
        /// 读取机型支持的全部能力 ID。
        /// </summary>
        /// <returns>能力 ID 枚举集合，每个 WMI 实例对应一项能力。</returns>
        public static Task<IEnumerable<CapabilityID>> ReadAsync() => WMI.ReadAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CAPABILITY_DATA_00",
            pdc => (CapabilityID)Convert.ToInt32(pdc["IDs"].Value));
    }
}
