using System.IO;
using UnityEditor;
using UnityEngine;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Editor
{
    /// <summary>
    /// Bridge 配置的编辑器统一访问入口。
    /// 职责：决定"当前编辑哪一份配置"（配置提供器数据源，或 ScriptableObject 资产），
    /// 并提供与该数据源匹配的保存逻辑。
    /// 所有编辑器入口（Project Settings 面板、菜单、环境快捷切换窗口）都必须通过本类读写配置，
    /// 否则会出现"面板改 JSON、菜单改 asset"这类数据源分裂的问题。
    /// </summary>
    public static class BridgeConfigEditorAccess
    {
        /// <summary>
        /// 自动创建配置资源时使用的目录（对应 Resources 加载路径 VoyageForge/Config）。
        /// </summary>
        private const string DefaultConfigDirectory = "Assets/Resources/VoyageForge/Config";

        /// <summary>
        /// 自动创建配置资源时使用的完整资源路径。
        /// </summary>
        private const string DefaultConfigAssetPath = DefaultConfigDirectory + "/BridgeConfig.asset";

        /// <summary>
        /// 获取当前应当编辑的配置对象。
        /// 优先使用 ProjectSettings 中选定的配置提供器；
        /// 未选择提供器、提供器创建失败或加载不到配置时，回退到 BridgeConfigAsset（必要时自动创建）。
        /// </summary>
        public static IBridgeConfig GetOrCreateConfig()
        {
            if (TryResolveProvider(out var provider))
            {
                var config = provider.LoadConfig();
                if (config != null)
                {
                    return config;
                }

                Debug.LogWarning(
                    $"[Bridge] 配置提供器 {BridgeSettings.instance.ConfigProviderTypeName} 未返回有效配置，已回退到 BridgeConfigAsset。");
            }

            return GetOrCreateConfigAsset();
        }

        /// <summary>
        /// 获取（必要时创建）BridgeConfigAsset 配置资源。
        /// 仅在确实需要 ScriptableObject 资产本身时调用；
        /// 需要与运行时使用同一份数据源时，请改用 <see cref="GetOrCreateConfig"/>。
        /// </summary>
        public static BridgeConfigAsset GetOrCreateConfigAsset()
        {
            var settings = BridgeSettings.instance;

            // 1. 已记录在 ProjectSettings 的资产引用优先。
            if (settings.ConfigAsset != null)
            {
                return settings.ConfigAsset;
            }

            // 2. 全项目搜索已有配置资源，避免重复创建。
            var found = FindBridgeConfigAsset();
            if (found != null)
            {
                settings.SetConfigAsset(found);
                return found;
            }

            // 3. 仍然没有，则在默认路径创建一份新资产。
            EnsureFolderExists("Assets/Resources");
            EnsureFolderExists(DefaultConfigDirectory);

            var created = ScriptableObject.CreateInstance<BridgeConfigAsset>();
            AssetDatabase.CreateAsset(created, DefaultConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // CreateAsset 之后需要重新加载，才能拿到 Unity 真正落盘后的实例。
            created = AssetDatabase.LoadAssetAtPath<BridgeConfigAsset>(DefaultConfigAssetPath);
            settings.SetConfigAsset(created);
            return created;
        }

        /// <summary>
        /// 保存配置。
        /// 数据源判定与 <see cref="GetOrCreateConfig"/> 完全一致，保证"读哪份就写哪份"；
        /// 若已选择提供器但提供器加载不出配置，则拒绝保存（宁可报错，也不要把资产数据写进 JSON 数据源）。
        /// </summary>
        /// <param name="config">要保存的配置对象。</param>
        public static void Save(IBridgeConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (TryResolveProvider(out var provider))
            {
                // 提供器可用性校验：加载不出配置说明数据源当前不可用，此时保存会造成数据源错位。
                if (provider.LoadConfig() == null)
                {
                    Debug.LogError(
                        $"[Bridge] 配置提供器 {BridgeSettings.instance.ConfigProviderTypeName} 无法加载配置，已取消保存。");
                    return;
                }

                provider.SaveConfig(config);
                return;
            }

            // 未选择提供器：数据源即 ScriptableObject 资产。
            if (config is BridgeConfigAsset asset)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 按 ProjectSettings 中的类型名创建配置提供器实例。
        /// </summary>
        /// <param name="provider">创建成功时返回提供器实例，否则为 null。</param>
        /// <returns>是否拿到了可用的提供器。</returns>
        private static bool TryResolveProvider(out IBridgeConfigProvider provider)
        {
            provider = null;

            string typeName = BridgeSettings.instance.ConfigProviderTypeName;
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            provider = BridgeConfigProviderFactory.CreateProvider(typeName);
            return provider != null;
        }

        /// <summary>
        /// 在全项目范围内搜索第一份 BridgeConfigAsset。
        /// </summary>
        /// <returns>找到的配置资源，未找到返回 null。</returns>
        private static BridgeConfigAsset FindBridgeConfigAsset()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(BridgeConfigAsset)}");
            if (guids == null || guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<BridgeConfigAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// 确保指定资源目录存在（逐级向上创建缺失的父目录）。
        /// </summary>
        /// <param name="folderPath">形如 "Assets/Resources/VoyageForge/Config" 的资源目录路径。</param>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string name = Path.GetFileName(folderPath);

            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
