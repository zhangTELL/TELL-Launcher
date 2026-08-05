# TELL Launcher

A Steam-style dark-themed Windows application launcher — one-click access to IDEs, AI tools, and games.

## Features

- **Sidebar Navigation**: IDE / AI Tools / Games / Recently Launched
- **Game Cards**: Vertical capsule cards with Steam CDN covers; supports `.lnk` and `.url` shortcuts
- **Steam Game Library**: Auto-detect installed Steam games via `appmanifest` and `libraryfolders.vdf`
- **Search**: Real-time fuzzy search across all applications
- **Drag & Drop**: Reorder and move apps between groups
- **App Details**: Hero-style detail page with large icons, custom images, and descriptions
- **Local-First**: All data stored in `%LocalAppData%\TELL Launcher\config.json`, no cloud required

## Tech Stack

- **Framework**: .NET 8 + WPF
- **Language**: C#
- **Architecture**: MVVM (hand-rolled)
- **Storage**: JSON config with backup rotation

## Build

```bash
dotnet build
dotnet publish -c Release -o publish
```

## Acknowledgements

本项目由 AI 编程助手辅助开发，鸣谢以下 AI 模型提供的能力支持：

- **[DeepSeek](https://www.deepseek.com/)** — 主力代码生成、架构设计和功能迭代
- **[Kimi](https://kimi.moonshot.cn/)** — UI 设计优化、视觉方案建议

## License

MIT
