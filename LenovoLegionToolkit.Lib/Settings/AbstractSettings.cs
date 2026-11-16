using System;
using System.IO;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 设置存储基类：负责把设置对象以 JSON 形式持久化到应用数据目录，并在读取失败时备份损坏文件。
/// 派生类通过 <see cref="Store"/> 懒加载设置，通过 <see cref="SynchronizeStore"/> 写回磁盘。
/// </summary>
/// <typeparam name="T">设置存储类型，需具备无参构造函数。</typeparam>
public abstract class AbstractSettings<T> where T : class, new()
{
    /// <summary>
    /// 序列化配置：缩进格式、自动写入类型名、反序列化时替换对象属性、枚举以字符串形式保存。
    /// </summary>
    protected readonly JsonSerializerSettings JsonSerializerSettings;
    private readonly string _settingsStorePath;
    private readonly string _fileName;

    /// <summary>
    /// 默认设置值。当配置文件不存在或无法解析时使用；派生类可重写以提供非默认初始值。
    /// </summary>
    protected virtual T Default => new();

    /// <summary>
    /// 当前内存中的设置对象。首次访问时从磁盘加载，加载失败则回退到 <see cref="Default"/>。
    /// </summary>
    public T Store => _store ??= LoadStore() ?? Default;

    private T? _store;

    /// <summary>
    /// 初始化设置存储。
    /// </summary>
    /// <param name="filename">设置文件名（不含路径），会拼接在应用数据目录下。</param>
    protected AbstractSettings(string filename)
    {
        JsonSerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            // 枚举以名称而非数字保存，便于阅读，也能在枚举项顺序调整后保持兼容
            Converters = { new StringEnumConverter() }
        };

        _fileName = filename;
        _settingsStorePath = Path.Combine(Folders.AppData, _fileName);
    }

    /// <summary>
    /// 将当前设置序列化并写入磁盘（覆盖原文件）。
    /// </summary>
    public void SynchronizeStore()
    {
        var settingsSerialized = JsonConvert.SerializeObject(_store, JsonSerializerSettings);
        File.WriteAllText(_settingsStorePath, settingsSerialized);
    }

    /// <summary>
    /// 从磁盘读取并反序列化设置。读取或解析失败时返回 null，并尝试备份损坏的配置文件。
    /// </summary>
    /// <returns>反序列化后的设置；失败时为 null。</returns>
    public virtual T? LoadStore()
    {
        T? store = null;
        try
        {
            var settingsSerialized = File.ReadAllText(_settingsStorePath);
            store = JsonConvert.DeserializeObject<T>(settingsSerialized, JsonSerializerSettings);

            // 文件为空或内容为 "null" 时反序列化结果为 null，同样视为损坏
            if (store is null)
                TryBackup();
        }
        catch
        {
            // 首次运行（文件不存在）或文件损坏都会进入这里；备份只对已存在的文件生效
            TryBackup();
        }

        return store;
    }

    /// <summary>
    /// 备份无法解析的配置文件，文件名附加 UTC 时间戳，避免直接覆盖用户数据。
    /// </summary>
    private void TryBackup()
    {
        try
        {
            if (!File.Exists(_settingsStorePath))
                return;

            var backupFileName = $"{Path.GetFileNameWithoutExtension(_fileName)}_backup_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(_fileName)}";
            var backupFilePath = Path.Combine(Folders.AppData, backupFileName);
            File.Copy(_settingsStorePath, backupFilePath);
        }
        catch (Exception ex)
        {
            // 备份失败不应阻止应用启动，仅记录日志
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unable to create backup for {_fileName}", ex);
        }
    }
}
