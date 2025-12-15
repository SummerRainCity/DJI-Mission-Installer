# DJI Mission Installer

![DJI Mission Installer Screenshot](https://github.com/Alos-no/DJI-Mission-Installer/blob/main/docs/preview.jpg?raw=true)

[![Language C#](https://img.shields.io/badge/Language-C%23-blue?style=for-the-badge&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Framework .NET 8](https://img.shields.io/badge/Framework-.NET%209-purple?style=for-the-badge&logo=.net)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Platform Windows](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows)](https://www.microsoft.com/en-us/windows)
[![License Apache 2.0](https://img.shields.io/badge/license-Apache%20License%202.0-yellow?style=for-the-badge)](https://opensource.org/license/apache-2-0)

**DJI Mission Installer** 是一款 Windows 桌面应用程序，旨在简化将自定义航点任务文件（`.kmz`）传输到使用 DJI Fly 应用的 DJI 设备的过程，例如 DJI RC、RC2、智能控制器或任何连接到控制器的安卓手机。

它提供了一个清晰的两窗格界面，用于查看本地任务文件和设备上的航点槽位，允许您替换任务并自动生成新的预览图像。

---

## ✨ 主要功能

-   **双连接模式**：通过 **ADB**（Android Debug Bridge）进行快速、可靠的传输，或通过 **MTP**（Media Transfer Protocol）进行简单、免驱动的访问。
-   **直观的双窗格视图**：轻松并排管理计算机本地的 KMZ 文件和已连接的 DJI 设备上的航点任务。
-   **自动预览生成**：当任务被传输时，应用会从 **ESRI 的 World Imagery 服务**获取地图瓦片，叠加任务名称和日期，并将其作为新预览上传。
-   **智能设备检测**：自动扫描并列出具有所需 DJI Fly 文件夹结构的兼容安卓设备。
-   **实时文件监控**：您的本地 KMZ 源文件夹会被监视，文件列表会自动更新。
-   **排序**：本地和设备文件列表均可按名称（使用自然字符串比较）、日期或大小排序。
-   **现代化响应式 UI**：基于 WPF 构建，在 Windows 上提供简洁、异步且用户友好的体验。

---

## 🚀 快速开始

### 先决条件

-   Windows 10 或更高版本。
-   [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)（或更新版本）。
-   一台使用 DJI Fly 应用的 DJI 控制器（或安卓设备）。

### 安装

1.  前往 [**Releases**](https://github.com/Alos-no/DJI-Mission-Installer/releases) 页面。
2.  从最新版本下载 `.zip` 压缩包。
3.  将内容解压到计算机上的一个永久文件夹中。
4.  运行 `DJI Mission Installer.exe`。

---

## 🔌 连接指南：ADB 与 MTP

该应用程序支持两种与设备通信的协议。最佳选择取决于您的控制器型号。

-   **ADB（Android Debug Bridge）**：这是**推荐**的方法。它更快、更可靠，并提供更好的设备反馈。需要在您的控制器上进行一次性设置以启用“USB 调试”。
-   **MTP（Media Transfer Protocol）**：这是传输文件的标准模式，类似于数码相机。无需特殊设置，但有时可能较慢或不太可靠，偶尔需要您拔下并重新连接设备。

| 设备型号                               | 推荐模式 | 设置说明                                                                                                                                                                                                                                                                   |
| ------------------------------------------ | ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DJI RC**、**RC Pro**、**智能控制器** | **ADB**          | **1. 启用开发者选项：** 进入 `设置` > `关于`，点击 **Build Number** 七次，直到看到“您现在已成为开发者！”的消息。<br/> **2. 启用 USB 调试：** 返回 `设置`，找到新出现的 `开发者选项` 菜单，并启用 **USB 调试**。 |
| **安卓手机 / 平板**                 | **ADB**          | 按照上述相同的步骤，在设备的安卓设置中启用开发者选项和 USB 调试。                                                                                                                                                                    |
| **DJI RC2**                                | **MTP**          | DJI RC2 默认以 MTP 模式连接，并不正式支持 ADB。对于绝大多数用户，**MTP 是唯一选择**。只需使用 USB 线将控制器连接到电脑，并在应用中选择 MTP 单选按钮。                             |

---

## 📋 使用指南

1.  **启动应用程序**。
2.  **连接您的设备**：通过 USB 将您的 DJI 控制器或安卓手机连接到计算机。
3.  **选择连接类型**：在窗口顶部，根据上面的指南选择 **ADB** 或 **MTP** 单选按钮。
4.  **刷新并选择设备**：点击 **刷新** 按钮。您的已连接设备应出现在下拉菜单中。选择它。
5.  **选择一个源文件**：在左侧“KMZ 文件”列表中，选择要传输的任务文件。如果列表为空，请点击“选择文件夹”来选择存储 `.kmz` 文件的目录。
6.  **选择一个目标槽位**：在右侧“设备航点”列表中，选择要替换的任务。
7.  **传输**：点击 **传输所选文件** 按钮。过程完成后会出现成功消息。设备列表将刷新以显示更新后的文件。
8.  安全断开您的设备。您的新任务现在已在 DJI Fly 应用中准备就绪！

---

## 🛠 从源代码构建

如果您想自己构建项目，请按照以下步骤操作。

### 先决条件

-   [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)，安装了“.NET 桌面开发”工作负载。
-   [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)。

### 构建步骤
1.  克隆仓库：
    ```sh
    git clone https://github.com/Alos-no/DJI-Mission-Installer.git
    ```
2.  在 Visual Studio 中打开 `DJI Mission Installer.sln` 解决方案文件。
3.  还原 NuGet 包（这应该会自动发生）。
4.  构建解决方案（F6 或 `生成 > 生成解决方案`）。可执行文件将在 `src/bin/Debug` 或 `src/bin/Release` 中。

---

## 🔧 技术细节与依赖项

该项目使用 C# 12 和 .NET 8 构建，使用了以下关键技术和库：

-   **WPF**：用于图形用户界面。
-   **MVVM 模式**：使用 `CommunityToolkit.Mvvm` 库实现 UI 和逻辑的清晰分离。
-   **[AdvancedSharpAdbClient](https://github.com/quamotion/madb)**：一个用于通过 Android Debug Bridge (ADB) 与安卓设备通信的 .NET 库。
-   **[MediaDevices](https://github.com/pvginkel/MediaDevices)**：用于通过媒体传输协议 (MTP) 访问设备存储。
-   **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)**：一个功能强大的跨平台 2D 图形库，用于生成和处理地图预览图像的水印。
-   **ESRI ArcGIS REST Services**：用于获取预览图像的卫星地图瓦片。

---

## 🤝 贡献

贡献让开源社区成为一个学习、启发和创造的绝佳之地。任何您做出的贡献都**非常感谢**。

如果您有改进建议，请 Fork 该仓库并创建拉取请求。您也可以简单地打开一个带有“enhancement”标签的问题。

1.  Fork 该项目
2.  创建您的功能分支 (`git checkout -b feature/AmazingFeature`)
3.  提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4.  推送到该分支 (`git push origin feature/AmazingFeature`)
5.  开启一个 Pull Request

---

## 📄 许可证

根据 Apache 2.0 许可证分发。更多信息请参阅 `LICENSE.txt`。
