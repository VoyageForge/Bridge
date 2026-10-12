using UnityEditor;
using UnityEngine;

namespace VoyageForge.Bridge.Editor
{
    /// <summary>
    /// Bridge 环境快捷切换菜单入口。
    /// </summary>
    public static class BridgeEnvironmentMenu
    {
        /// <summary>打开 Bridge 配置面板的菜单项路径。</summary>
        private const string SettingsMenuPath = "VoyageForge/Bridge/Bridge Config";

        /// <summary>打开环境快捷切换窗口的菜单项路径。</summary>
        private const string EnvironmentMenuPath = "VoyageForge/Bridge/Bridge Environment/选择当前环境";

        /// <summary>
        /// 菜单项：打开 Project Settings 中的 Bridge 面板。
        /// </summary>
        [MenuItem(SettingsMenuPath, false, 100)]
        public static void OpenProjectSettings()
        {
            BridgeSettingsProvider.OpenSettings();
        }

        /// <summary>
        /// 菜单项：打开环境快捷切换窗口。
        /// 配置通过 <see cref="BridgeConfigEditorAccess.GetOrCreateConfig"/> 获取，
        /// 与运行时及 Project Settings 面板保持同一数据源（ScriptableObject 资产或配置提供器）。
        /// </summary>
        [MenuItem(EnvironmentMenuPath, false, 101)]
        private static void ShowEnvironmentMenu()
        {
            var config = BridgeConfigEditorAccess.GetOrCreateConfig();
            if (config == null)
            {
                Debug.LogError("未找到或无法创建 BridgeConfig 配置资源。请先打开 Project Settings 检查配置。");
                return;
            }

            BridgeEnvironmentQuickSwitchWindow.Open(config);
        }

        /// <summary>
        /// 菜单可用性校验：配置可获取（必要时会按既定规则创建默认配置）时才允许点击。
        /// </summary>
        [MenuItem(EnvironmentMenuPath, true)]
        private static bool ValidateEnvironmentMenu()
        {
            return BridgeConfigEditorAccess.GetOrCreateConfig() != null;
        }
    }
}
