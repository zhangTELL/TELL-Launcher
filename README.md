# TELL 启动器

一款 Steam 风格暗色主题的 Windows 应用启动器,把常用的 IDE、AI 工具和游戏集中在一个窗口中,点击即开喵。

> TELL 是我的网名,这个启动器因我而生喵。

## 功能特性

- **侧边栏导航**:IDE / AI 工具 / 游戏 / 最近启动 四个分组,一键切换喵
- **胶囊封面卡片**:Steam 风格竖版卡片,自动拉取 Steam 封面图喵
- **Steam 游戏库**:自动解析 `appmanifest` 与 `libraryfolders.vdf`,识别已安装的 Steam 游戏喵
- **快捷方式扫描**:自动扫描桌面 `.lnk` / `.url` 快捷方式,支持去重与隐藏记忆喵
- **实时模糊搜索**:跨全部分组按名称模糊匹配喵
- **拖拽管理**:支持拖动排序、跨分组移动喵
- **应用详情页**:大图封面、详细描述、自定义图片喵
- **自动填充**:添加应用时自动识别名称与图标,并生成默认描述喵
- **本地优先**:所有配置保存在本地,无需登录、无需联网喵

## 技术栈

| 项目 | 说明 |
| --- | --- |
| 框架 | .NET 8 + WPF |
| 语言 | C# |
| 架构 | MVVM(手写实现) |
| 存储 | JSON 配置文件,带备份轮转 |
| 测试 | xUnit |

## 目录结构

```text
TELLLauncher/
├── Models/        # 数据模型(AppEntry / AppGroup / LauncherConfig)
├── Services/      # 配置读写、程序定位、快捷方式扫描、图标提取、启动等
├── ViewModels/    # 界面状态与交互逻辑
├── Views/         # 窗口与对话框(主窗口 / 详情页 / 编辑对话框 / Steam 库)
└── TELLLauncher.Tests/   # 单元测试
```

## 构建与发布

```bash
# 构建
dotnet build

# 发布(单文件可执行程序,输出到 publish/)
dotnet publish -c Release -o publish
```

## 数据存储

配置保存在 `%LocalAppData%\TELL Launcher\config.json`,包括:

- 应用清单与自定义修改
- 手动添加的应用
- 游戏扫描结果与隐藏状态
- 分组归属与排序
- 自定义名称、图标、描述与程序路径

配置文件写入前会自动备份轮转,避免损坏丢失喵。

## SteamGridDB API

SteamGridDB 用于补充游戏封面:Steam 没有本地/CDN 图以及非 Steam 游戏会通过 API 搜索并缓存竖版高清封面喵。

- 服务地址:`https://www.steamgriddb.com/api/v2`
- API Key 读取优先级:环境变量 `STEAMGRIDDB_API_KEY` > `%LocalAppData%\TELL Launcher\steamgriddb.json`(格式为 `{"apiKey": "..."}`)
- API Key 只保存在本机配置或环境变量中,不会写入仓库
- 封面缓存:`%LocalAppData%\TELL Launcher\covers\steamgriddb`
- 查找顺序:SteamGridDB API → Steam 本地缓存 / Steam CDN / 本地高清图
- 未配置 API Key 时自动跳过 SteamGridDB,只使用本地缓存和本地高清图

## 测试

```bash
dotnet test
```

## 鸣谢

本项目由 AI 编程助手辅助开发,鸣谢以下 AI 模型提供的能力支持:

- **[DeepSeek](https://www.deepseek.com/)** — 主力代码生成、架构设计和功能迭代
- **[Kimi](https://kimi.moonshot.cn/)** — UI 设计优化、视觉方案建议
- **[Codex](https://openai.com/codex/)** — 代码生成与任务协作

## 许可证

[MIT](LICENSE)
