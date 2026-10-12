using System;
using UnityEditor;
using UnityEngine;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Editor
{
    /// <summary>
    /// Bridge 环境快速切换窗口。
    /// 面向 <see cref="IBridgeConfig"/> 编程，因此无论当前数据源是 ScriptableObject 资产还是
    /// 配置提供器（如 StreamingAssets JSON），切换结果都会写入同一份数据源。
    /// </summary>
    internal sealed class BridgeEnvironmentQuickSwitchWindow : EditorWindow
    {
        /// <summary>当前窗口编辑的配置对象。</summary>
        private IBridgeConfig config;

        /// <summary>
        /// 打开窗口并绑定配置对象。
        /// </summary>
        /// <param name="config">要编辑的配置对象。</param>
        public static void Open(IBridgeConfig config)
        {
            var window = CreateInstance<BridgeEnvironmentQuickSwitchWindow>();
            window.config = config;
            window.titleContent = new GUIContent("快速切换环境");
            window.minSize = new Vector2(260f, 120f);
            window.maxSize = new Vector2(360f, 480f);
            window.ShowUtility();
        }

        /// <summary>
        /// 绘制窗口内容：按环境列表生成按钮，点击即切换当前环境并保存。
        /// </summary>
        private void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("未找到 BridgeConfig 配置资源。", MessageType.Warning);
                if (GUILayout.Button("打开配置"))
                {
                    BridgeSettingsProvider.OpenSettings();
                    Close();
                }

                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("快速切换环境", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("点击下面的环境名称即可立即切换。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8f);

            if (config.EnvironmentKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有可用环境，请先到 Project Settings 中配置。", MessageType.Info);
            }
            else
            {
                foreach (string environmentKey in config.EnvironmentKeys)
                {
                    bool isCurrent = string.Equals(config.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase);

                    // 当前环境按钮置灰，避免重复点击；文本上标明"当前环境"。
                    using (new EditorGUI.DisabledScope(isCurrent))
                    {
                        string buttonText = isCurrent ? $"当前环境：{environmentKey}" : environmentKey;

                        if (GUILayout.Button(buttonText, GUILayout.Height(28f)))
                        {
                            // 通过 IBridgeConfig 切换并由统一入口保存，保证写入正确的数据源。
                            config.SetEnvironment(environmentKey);
                            BridgeConfigEditorAccess.Save(config);

                            Debug.Log($"Bridge 当前环境已切换为：{environmentKey}");
                            Close();
                        }
                    }
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("打开配置面板"))
            {
                BridgeSettingsProvider.OpenSettings();
                Close();
            }
        }
    }
}
