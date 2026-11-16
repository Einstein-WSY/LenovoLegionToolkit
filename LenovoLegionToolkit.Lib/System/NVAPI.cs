using System.Collections.Generic;
using System.Linq;
using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.GPU;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// NVIDIA API（NVAPI）封装，用于查询笔记本独立显卡信息与显示连接状态。
/// 所有查询在 NVAPI 不可用（无 NVIDIA 显卡或驱动异常）时均降级返回空值。
/// </summary>
internal static class NVAPI
{
    /// <summary>
    /// 初始化 NVAPI。必须在调用其他成员前执行一次。
    /// </summary>
    public static void Initialize() => NVIDIA.Initialize();

    /// <summary>
    /// 释放 NVAPI 资源。
    /// </summary>
    public static void Unload() => NVIDIA.Unload();

    /// <summary>
    /// 获取第一块笔记本型（Laptop）NVIDIA 物理显卡。
    /// </summary>
    /// <returns>物理显卡对象；无 NVIDIA 显卡或查询失败时返回 null。</returns>
    public static PhysicalGPU? GetGPU()
    {
        try
        {
            // 仅取 SystemType 为 Laptop 的 GPU，排除台式机/虚拟 GPU
            return PhysicalGPU.GetPhysicalGPUs().FirstOrDefault(gpu => gpu.SystemType == SystemType.Laptop);
        }
        catch (NVIDIAApiException)
        {
            // 驱动未安装或被系统禁用时 NVAPI 会抛异常，此处按"无独显"处理
            return null;
        }
    }

    /// <summary>
    /// 判断指定 GPU 当前是否有显示器连接（即是否在驱动显示器）。
    /// </summary>
    /// <param name="gpu">待检查的物理显卡。</param>
    /// <returns>有显示器连接返回 true；查询失败返回 false。</returns>
    public static bool IsDisplayConnected(PhysicalGPU gpu)
    {
        try
        {
            return Display.GetDisplays().Any(d => d.PhysicalGPUs.Contains(gpu, PhysicalGPUEqualityComparer.Instance));
        }
        catch (NVIDIAApiException)
        {
            return false;
        }
    }

    /// <summary>
    /// 获取 GPU 的 PCI 标识字符串（如 PCI\VEN_10DE&amp;DEV_...）。
    /// </summary>
    /// <param name="gpu">物理显卡。</param>
    /// <returns>PCI 标识字符串；查询失败返回 null。</returns>
    public static string? GetGPUId(PhysicalGPU gpu)
    {
        try
        {
            return gpu.BusInformation.PCIIdentifiers.ToString();
        }
        catch (NVIDIAApiException)
        {
            return null;
        }
    }

    /// <summary>
    /// 物理显卡比较器：NVAPI 返回的 PhysicalGPU 实例并不保证引用相等，
    /// 因此以 GPUId 作为逻辑相等依据。
    /// </summary>
    private class PhysicalGPUEqualityComparer : IEqualityComparer<PhysicalGPU>
    {
        /// <summary>单例实例，避免重复创建。</summary>
        public static readonly PhysicalGPUEqualityComparer Instance = new();

        private PhysicalGPUEqualityComparer() { }

        /// <summary>
        /// 比较两块 GPU 是否为同一设备。
        /// </summary>
        /// <param name="x">左值。</param>
        /// <param name="y">右值。</param>
        /// <returns>GPUId 相同返回 true。</returns>
        public bool Equals(PhysicalGPU? x, PhysicalGPU? y) => x?.GPUId == y?.GPUId;

        /// <summary>
        /// 获取哈希值。
        /// </summary>
        /// <param name="obj">目标 GPU。</param>
        /// <returns>基于 GPUId 的哈希值。</returns>
        public int GetHashCode(PhysicalGPU obj) => obj.GPUId.GetHashCode();
    }
}
