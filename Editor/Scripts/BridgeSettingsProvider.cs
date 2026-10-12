using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VoyageForge.Bridge.Runtime;
using VoyageForge.Depot.Editor.Utilities;

namespace VoyageForge.Bridge.Editor
{
    /// <summary>
    /// Bridge 的 Project Settings 面板（UI Toolkit 实现）。
    /// 只负责界面构建与交互：配置数据源判定、创建与保存全部委托给 <see cref="BridgeConfigEditorAccess"/>，
    /// 数据增删改全部通过 <see cref="IBridgeConfig"/> 的编辑方法完成，不在界面层直接操作列表。
    /// </summary>
    public sealed class BridgeSettingsProvider : SettingsProvider
    {
        /// <summary>Project Settings 中的菜单位置。</summary>
        private const string SettingsPath = "Project/VoyageForge/Bridge";

        /// <summary>
        /// 面板布局文件。这里沿用历史写法（相对 Assets 的路径），
        /// 实际解析由 Depot 的 UxmlUtility 完成：它只取文件名，并在"调用方所属包"内搜索，
        /// 因此 Packages/Bridge 下的同名 UXML 也能被正确找到。
        /// </summary>
        private const string UxmlPath = "Assets/Bridge/Editor/Scripts/BridgeProjectSettingsView.uxml";

        /// <summary>
        /// 创建面板实例。
        /// </summary>
        public BridgeSettingsProvider() : base(SettingsPath, SettingsScope.Project)
        {
            label = "Bridge";
            activateHandler = (_, rootElement) => BuildUi(rootElement);
            keywords = new HashSet<string>(new[] { "Bridge", "网络", "环境", "端点" });
        }

        /// <summary>向 Unity 注册本面板。</summary>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => new BridgeSettingsProvider();

        /// <summary>打开 Bridge 的 Project Settings 面板。</summary>
        public static void OpenSettings() => SettingsService.OpenProjectSettings(SettingsPath);

        /// <summary>
        /// 兼容入口：获取（必要时创建）BridgeConfigAsset。
        /// 内部转发到 <see cref="BridgeConfigEditorAccess"/>；
        /// 若需要与运行时使用同一份数据源，请改用 <see cref="BridgeConfigEditorAccess.GetOrCreateConfig"/>。
        /// </summary>
        public static BridgeConfigAsset GetOrCreateConfigAsset() => BridgeConfigEditorAccess.GetOrCreateConfigAsset();

        /// <summary>
        /// 判断是否为保底环境（dev）。保底环境始终保留且不可删除。
        /// </summary>
        private static bool IsReservedEnvironment(string key) =>
            string.Equals(key, BridgeConfigAsset.ReservedEnvironmentKey, StringComparison.OrdinalIgnoreCase);

        // ============================================================
        // UI 构建
        // ============================================================

        /// <summary>
        /// 构建整个面板：加载 UXML → 加载配置 → 提供器下拉 → 环境工具栏 → 各环境卡片。
        /// 任何一步失败都会在面板上直接显示失败原因，而不是静默留白。
        /// </summary>
        /// <param name="rootElement">面板根元素。</param>
        private static void BuildUi(VisualElement rootElement)
        {
            rootElement.Clear();

            var visualTree = UxmlUtility.LoadVisualTreeAsset(UxmlPath);
            if (visualTree == null)
            {
                rootElement.Add(new Label("UXML 加载失败"));
                return;
            }

            visualTree.CloneTree(rootElement);

            var config = BridgeConfigEditorAccess.GetOrCreateConfig();
            if (config == null)
            {
                rootElement.Add(new Label("配置加载失败"));
                return;
            }

            BuildProviderPopup(rootElement);
            BuildEnvToolbar(rootElement, config);

            var cardContainer = rootElement.Q<VisualElement>("EnvironmentCardContainer");
            cardContainer?.Clear();
            foreach (string environmentKey in config.EnvironmentKeys)
            {
                cardContainer?.Add(BuildEnvCard(config, environmentKey, rootElement));
            }
        }

        // ---- 配置提供器下拉 ----

        /// <summary>
        /// 构建"配置提供器"下拉：列出所有 <see cref="IBridgeConfigProvider"/> 实现，
        /// 第一项表示不使用提供器、直接编辑 ScriptableObject 资产。
        /// </summary>
        /// <param name="rootElement">面板根元素。</param>
        private static void BuildProviderPopup(VisualElement rootElement)
        {
            var contentRoot = rootElement.Q<VisualElement>("ContentRoot");
            if (contentRoot == null) return;

            var settings = BridgeSettings.instance;
            var providerTypes = TypeCache.GetTypesDerivedFrom<IBridgeConfigProvider>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.FullName)
                .ToList();

            var section = new VisualElement();
            section.AddToClassList("section-card");
            section.Add(new Label("配置提供器") { name = "section-title" });

            var choices = new List<string> { "(未选择 — ScriptableObject)" };
            choices.AddRange(providerTypes.Select(t => t.FullName));

            // 当前选中的提供器类型决定下拉的初始选中项。
            string currentTypeName = settings.ConfigProviderTypeName;
            int selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(currentTypeName))
            {
                int found = choices.FindIndex(choice => string.Equals(choice, currentTypeName, StringComparison.Ordinal));
                if (found >= 0) selectedIndex = found;
            }

            var popup = new PopupField<string>("提供器类型", choices, selectedIndex);
            popup.AddToClassList("stretch-field");
            section.Add(popup);

            var hint = new TextElement();
            hint.AddToClassList("environment-subtitle");
            section.Add(hint);

            // 提示当前实际生效的数据源。
            void RefreshHint()
            {
                hint.text = popup.index == 0
                    ? "数据源: BridgeConfigAsset (ScriptableObject)"
                    : $"数据源: {choices[popup.index]}";
            }

            RefreshHint();

            popup.RegisterValueChangedCallback(evt =>
            {
                // index 0 表示不使用提供器（写入 null，清空选择）。
                settings.SetConfigProviderType(popup.index > 0 ? evt.newValue : null);
                RefreshHint();

                // 数据源切换后整块重建，保证面板展示的是新数据源的内容。
                BuildUi(rootElement);
            });

            contentRoot.Insert(0, section);
        }

        // ---- 环境工具栏 ----

        /// <summary>
        /// 构建环境工具栏：当前环境下拉 + 新增环境输入框与按钮。
        /// </summary>
        /// <param name="rootElement">面板根元素。</param>
        /// <param name="config">当前配置对象。</param>
        private static void BuildEnvToolbar(VisualElement rootElement, IBridgeConfig config)
        {
            var container = rootElement.Q<VisualElement>("CurrentEnvironmentContainer");
            if (container == null) return;

            container.Clear();

            var environments = config.EnvironmentKeys.ToList();
            if (environments.Count == 0)
            {
                // 极端情况（配置被外部清空）：至少把当前环境键显示出来，避免下拉为空。
                environments.Add(config.EnvironmentKey);
            }

            int currentIndex = Mathf.Max(0, environments.FindIndex(key => key == config.EnvironmentKey));
            var popup = new PopupField<string>("当前环境", environments, currentIndex);
            popup.AddToClassList("stretch-field");
            popup.RegisterValueChangedCallback(evt =>
            {
                config.SetEnvironment(evt.newValue);
                BridgeConfigEditorAccess.Save(config);
                BuildUi(rootElement);
            });
            container.Add(popup);

            var input = rootElement.Q<TextField>("NewEnvironmentField");
            var addButton = rootElement.Q<Button>("AddEnvironmentButton");
            if (addButton != null)
            {
                addButton.clicked += () =>
                {
                    string key = input?.value?.Trim();
                    if (string.IsNullOrWhiteSpace(key)) return;

                    // AddEnvironment 内部负责去重、规范化，并在需要时把新环境设为当前环境。
                    if (config.AddEnvironment(key))
                    {
                        BridgeConfigEditorAccess.Save(config);
                    }

                    if (input != null) input.value = string.Empty;
                    BuildUi(rootElement);
                };
            }
        }

        // ---- 环境卡片 ----

        /// <summary>
        /// 构建单个环境卡片：卡片头（环境名 + 新增/删除按钮）与该环境下的端点行列表。
        /// </summary>
        /// <param name="config">当前配置对象。</param>
        /// <param name="environmentKey">卡片对应的环境键。</param>
        /// <param name="rootElement">面板根元素，用于操作后重建界面。</param>
        private static VisualElement BuildEnvCard(IBridgeConfig config, string environmentKey, VisualElement rootElement)
        {
            var card = new VisualElement();
            card.AddToClassList("environment-card");

            var header = new VisualElement();
            header.AddToClassList("environment-card-header");

            var titleGroup = new VisualElement();
            titleGroup.AddToClassList("environment-title-group");
            titleGroup.Add(new Label(environmentKey));
            header.Add(titleGroup);

            var actions = new VisualElement();
            actions.AddToClassList("environment-action-group");

            var addEndpointButton = new Button(() =>
            {
                config.AddEndpoint(environmentKey);
                BridgeConfigEditorAccess.Save(config);
                BuildUi(rootElement);
            })
            { text = "新增链接" };
            addEndpointButton.AddToClassList("primary-button");
            actions.Add(addEndpointButton);

            bool reserved = IsReservedEnvironment(environmentKey);
            var deleteEnvironmentButton = new Button(() =>
            {
                // RemoveEnvironment 内部同样会拒绝删除 dev；按钮置灰只是更直观的提示。
                if (config.RemoveEnvironment(environmentKey))
                {
                    BridgeConfigEditorAccess.Save(config);
                }

                BuildUi(rootElement);
            })
            { text = reserved ? "保留环境" : "删除环境" };
            deleteEnvironmentButton.AddToClassList("secondary-button");

            if (reserved)
            {
                deleteEnvironmentButton.tooltip = "dev 环境始终保留。";
                deleteEnvironmentButton.SetEnabled(false);
            }

            actions.Add(deleteEnvironmentButton);

            header.Add(actions);
            card.Add(header);

            // 只展示属于当前环境的端点；行序号使用"环境内序号"，与配置的编辑方法参数一致。
            var endpoints = config.Endpoints
                .Where(entry => entry != null &&
                                string.Equals(entry.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (endpoints.Count == 0)
            {
                var emptyHint = new TextElement { text = "暂无链接配置。" };
                emptyHint.AddToClassList("info-banner-text");
                card.Add(emptyHint);
            }
            else
            {
                for (int i = 0; i < endpoints.Count; i++)
                {
                    card.Add(BuildEndpointRow(config, environmentKey, i, endpoints[i], rootElement));
                }
            }

            return card;
        }

        // ---- 端点行 ----

        /// <summary>
        /// 构建单条端点行：键输入框 + 地址输入框 + 删除按钮。
        /// 输入框的修改通过 <see cref="IBridgeConfig.SetEndpoint"/> 落库，由配置实现统一做规范化。
        /// </summary>
        /// <param name="config">当前配置对象。</param>
        /// <param name="environmentKey">所属环境键。</param>
        /// <param name="localIndex">该环境内的端点序号（从 0 开始）。</param>
        /// <param name="entry">被编辑的端点（仅用于取当前值）。</param>
        /// <param name="rootElement">面板根元素，用于操作后重建界面。</param>
        private static VisualElement BuildEndpointRow(IBridgeConfig config, string environmentKey,
            int localIndex, EndpointConfig entry, VisualElement rootElement)
        {
            var row = new VisualElement();
            row.AddToClassList("endpoint-row");

            var keyField = new TextField("键") { value = entry.EndpointKey ?? "default" };
            keyField.AddToClassList("endpoint-field");
            keyField.RegisterValueChangedCallback(evt =>
            {
                // 端点键清理（空值回退 default）由配置实现负责，界面只投递原始输入。
                config.SetEndpoint(environmentKey, localIndex, evt.newValue, entry.Url);
                BridgeConfigEditorAccess.Save(config);
            });
            row.Add(keyField);

            var urlField = new TextField("地址") { value = entry.Url ?? string.Empty };
            urlField.AddToClassList("endpoint-field");
            urlField.RegisterValueChangedCallback(evt =>
            {
                config.SetEndpoint(environmentKey, localIndex, entry.EndpointKey, evt.newValue);
                BridgeConfigEditorAccess.Save(config);
            });
            row.Add(urlField);

            var deleteButton = new Button(() =>
            {
                // localIndex 在编辑期间可能因重建而变化，这里传环境内序号交给配置实现重新定位。
                config.RemoveEndpoint(environmentKey, localIndex);
                BridgeConfigEditorAccess.Save(config);
                BuildUi(rootElement);
            })
            { text = "删除链接" };
            deleteButton.AddToClassList("danger-button");
            row.Add(deleteButton);

            return row;
        }
    }
}
