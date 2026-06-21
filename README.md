# 智能穿搭助手 Smart Outfit Assistant

发布级本地优先 PWA Web App：根据心情、天气、场景和个人衣柜生成穿搭；衣柜不足时自动联网搜索并缓存参考图片。

## 亮点

- 心情/场景/天气/湿度/风力综合推荐。
- 衣柜优先匹配，显示匹配来源和理由。
- 缺少单品自动联网找图，并缓存到 `wwwroot/generated/reference-images/`，展示更稳定。
- 衣柜管理：上传/拍照、多图录入、颜色提取、标签、收藏、编辑、删除、搜索过滤。
- 历史记录：查看、收藏、清空。
- 个性化设置：偏好色、避雷色、风格、是否联网找图、是否优先衣柜。
- PWA：可安装为桌面/手机 App，具备基础离线 App Shell。
- 发布准备：健康检查、响应压缩、全局异常处理、Dockerfile、数据导出。

## 运行

```powershell
cd C:\Users\33625\Desktop\ConsoleApp2\ConsoleApp2
.\start.ps1
```

访问：`http://localhost:5187`

App 窗口模式：

```powershell
.\start-app.ps1
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

## 数据位置

- `App_Data/wardrobe.json`：衣柜
- `App_Data/history.json`：历史
- `App_Data/settings.json`：设置
- `wwwroot/uploads/`：用户上传图片
- `wwwroot/generated/reference-images/`：联网参考图缓存

## 可选配置

`appsettings.json`：

```json
{
  "ImageSearch": {
    "Enabled": true,
    "CacheRemoteImages": true,
    "ProviderOrder": ["DuckDuckGo", "Bing", "LoremFlickr"],
    "UnsplashAccessKey": ""
  }
}
```

如需更稳定的商用图片来源，可申请 Unsplash API Key 并填入 `UnsplashAccessKey`。

## 别人电脑访问

### 方式 1：同一个 Wi-Fi / 局域网

```powershell
.\start-lan.ps1
```

脚本会显示类似：

```text
http://192.168.x.x:5187
```

同一 Wi-Fi 下的其他电脑/手机打开这个地址即可访问。

### 方式 2：公网临时链接

```powershell
.\start-public-tunnel.ps1
```

脚本会下载 Cloudflare Tunnel 客户端，并生成类似：

```text
https://xxxx.trycloudflare.com
```

把这个链接发给别人即可访问。注意：临时链接在隧道进程停止后会失效。
