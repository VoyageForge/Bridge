# Changelog

本文件由开发者手动维护，发布流程（`.github/workflows/publish.yml`）**不会**自动生成或修改它。

## 维护规范

- 发版前在文件顶部新增一段 `## vX.Y.Z`，用 `### Added` / `### Changed` / `### Fixed` 分组，
  写**面向使用者**的说明（不要直接粘贴 commit 标题）。
- 版本号要与发布时使用的版本一致：在 Actions 里手动运行 **Publish Bridge** 时，把 `release_version`
  输入填成与本文档标题相同的 `X.Y.Z`；若留空，工作流会按 patch 自动递增版本号，此时记得回填本文档标题。
- 发布工作流只做三件事：更新 `package.json` 的版本号与 `com.voyageforge.depot` 依赖、打 tag、
  创建 GitHub Release；OpenUPM 会在 tag 推送后自动构建新版本。

## v0.0.16

### Changed
- **破坏性变更**：`Response<T>` 的公共成员由字段改为属性（PascalCase）——
  `data` → `Data`、`statusCode` → `StatusCode`、`statusText` → `StatusText`、`headers` → `Headers`
  （`RawText`、`IsSuccessStatusCode` 名称不变）。升级后请把 `response.data` 之类的访问改成 `response.Data`；
  这是封装形式与命名的调整，行为不变。
- 补全 `Response<T>` 全部成员的 XML 注释，并写明两处容易踩的语义：
  网络层失败（超时 / 取消 / 连接错误）时 `StatusCode` 为 0；
  `Headers` 目前在成功路径下也是空字典（尚未从 `UnityWebRequest` 回填真实响应头）。
- README 新增 `Response<T>` 成员表，并同步「目录说明」与响应拦截器示例里的成员名。
- 示例（`Samples~/Basic`、`Samples~/BridgeHub`）与测试同步改用新的属性名。

### Fixed
- 修正 `Response.cs` 缺少的文件结尾换行，以及 `Samples~/Basic/Scripts/Test.cs` 意外的 UTF-8 BOM
  （仅编码与格式，无行为变化）。

## v0.0.15

### Added
- 新增 `BridgeHub` 静态门面：依赖 Bridge 的第三方包只需写静态封装函数即可调用 Web API，
  不必也不应在自己的包内声明 `BridgeClient<T>` 子类（那会与宿主的客户端重复并各自加载一份配置）。
- 新增 `IBridgeClient`（非泛型客户端契约）、`BridgeNotInitializedException`（未登记客户端时的明确异常，
  带处理指引）。
- 新增 `Samples~/BridgeHub` 示例：演示「包侧静态封装 + 应用侧客户端自动登记」的完整用法，
  自带示例配置与演示入口；`Samples~/Basic` 增加 `BridgeBootstrap`，导入示例即可开箱使用。

### Changed
- `BridgeClient<T>` 实现 `IBridgeClient`：发送管线改为实例方法，客户端首次实例化时自动登记到 `BridgeHub`
  （先到先得，可用 `BridgeHub.SetDefaultClient` 显式指定）；公开静态 API 保持向后兼容。
- 编辑器的配置读写统一走 `BridgeConfigEditorAccess`，保证面板 / 菜单 / 快捷切换窗口改的就是运行时读的那一份；
  环境与端点的增删改统一经 `IBridgeConfig` 的编辑方法，不再绕过实现类里的数据规则。
- `com.voyageforge.depot` 依赖更新到 `0.0.25`（由发布工作流自动同步）。
- README 增补「作为依赖库使用 Bridge（第三方包）」章节与 `BridgeHub` API 表。

### Fixed
- 修复请求未指定 `endpointKey` 时误把**环境键**当成端点键、导致真实配置下抛「未找到端点」的问题；
  默认端点键现在只由 `BridgeClient<T>.urlKey` 决定。
- 修复响应拦截器的修改被丢弃的问题：现在会回写到最终响应，响应文本被改写时会重新反序列化并同步 `RawText`。
- 修正两个超时用例的误报断言（只断言「成功必须越过 60 秒」，无法判定时标 Inconclusive）。

## 历史记录（v0.0.14 及更早）

> 以下条目是当年由发布工作流从 `git log` 自动生成的原始提交标题（含少量没有信息量的 merge / 版本提交），
> 这里只修复了「段落标题与上一条粘连在同一行」的换行问题，未重新撰写，保留原貌以便对照。

### v0.0.14
- chore(bridge): set version to 0.0.14
- feat(bridge): 适配 Depot UxmlUtility 命名空间并新增请求超时测试
- Merge branch 'dev'
- feat(BridgeClient): 升级至 UniTask 并修复多项关键问题

### v0.0.13
- chore(bridge): set version to 0.0.13
- feat(BridgeClient): 升级至 UniTask 并修复多项关键问题

### v0.0.12
- chore(bridge): set version to 0.0.12

### v0.0.11
- chore(bridge): set version to 0.0.11

### v0.0.10
- chore(bridge): set version to 0.0.10

### v0.0.9
- chore(bridge): set version to 0.0.9
- Refactor publish workflow for versioning and changelog
- chore: sync main back to dev
- chore: merge dev into main
- revert: rollback publish-bridge.yml to before 0a8c4b8
- Rename publish-bridge.yml to publish.yml
- add publish
- remove publish-bridge.yml
- 改进 publish-bridge.yml 的工作流程步骤
- chore: sync main back to dev
- chore: update LICENSE
- Merge pull request #1 from VoyageForge/dev
- 为 GitHub Actions 添加版本控制工作流
- 通过版本控制和本地化增强发布流程
- feat: add JSON config support and editor provider selection

### v0.0.8
- chore(bridge): set version to 0.0.8

### v0.0.7 及更早
- chore(bridge): set version to 0.0.7
- Merge dev into main
- chore: bump dependencies and hideInEditor
- ci: add version guard to prevent rollback on main
- fix: restore version to 0.0.6 after accidental rollback
- chore(bridge): update changelog for v0.0.4
- chore(bridge): set version to 0.0.4
- Merge dev into main - resolve dependency version conflict
- chore: bump dependencies and hideInEditor
