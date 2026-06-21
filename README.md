# 智能穿搭助手 Smart Outfit Assistant

[![.NET CI](https://github.com/Kyrie-Wang-demo/SmartOutfitAssistant/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Kyrie-Wang-demo/SmartOutfitAssistant/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

一个开源、本地优先、可安装为 PWA 的智能穿搭助手。它可以根据心情、天气、场景和你的个人衣柜生成完整穿搭；当衣柜缺少某件单品时，会自动联网搜索参考图并缓存到本地。

## 功能特性

- 心情穿搭：开心、忧郁、活力、慵懒、正式、浪漫、自定义心情。
- 天气调节：温度、晴雨雪、风力、湿度自动影响层数、材质、鞋履与配饰。
- 衣柜优先：上传/拍照录入衣物，推荐时优先使用已有衣物。
- 联网参考图：缺少单品时自动搜索参考图，并缓存到本地。
- 衣柜管理：搜索、过滤、编辑、删除、收藏、标签、颜色提取。
- 历史记录：保存、查看、收藏、清空。
- 个性化设置：偏好色、避雷色、风格、是否联网找图。
- PWA：可安装为桌面/手机 App。
- 发布友好：Dockerfile、GitHub Actions、健康检查、数据导出。

## 快速开始

```powershell
git clone https://github.com/Kyrie-Wang-demo/SmartOutfitAssistant.git
cd SmartOutfitAssistant
dotnet run --urls http://localhost:5187
```

打开：

```text
http://localhost:5187
```

Windows App 窗口模式：

```powershell
.\start-app.ps1
```

## 局域网/公网访问

同一 Wi-Fi：

```powershell
.\start-lan.ps1
```

公网临时链接：

```powershell
.\start-public-tunnel.ps1
```

## 发布

```powershell
dotnet publish -c Release -o .\publish
```

Docker：

```powershell
docker build -t smart-outfit-assistant .
docker run -p 8080:8080 smart-outfit-assistant
```

## 数据隐私

默认本地保存：

- `App_Data/wardrobe.json`
- `App_Data/history.json`
- `App_Data/settings.json`
- `wwwroot/uploads/`
- `wwwroot/generated/reference-images/`

这些私人数据已被 `.gitignore` 忽略，不会上传到仓库。

## License

MIT License. See [LICENSE](LICENSE).
