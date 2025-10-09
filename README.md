# DJI Mission Installer

![DJI Mission Installer Screenshot](https://github.com/Alos-no/DJI-Mission-Installer/blob/main/docs/preview.jpg?raw=true)

[![Language C#](https://img.shields.io/badge/Language-C%23-blue?style=for-the-badge&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Framework .NET 8](https://img.shields.io/badge/Framework-.NET%208-purple?style=for-the-badge&logo=.net)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Platform Windows](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows)](https://www.microsoft.com/en-us/windows)
[![License MIT](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)](https://opensource.org/licenses/MIT)

**DJI Mission Installer** is a Windows desktop application designed to simplify the process of transferring custom waypoint mission files (`.kmz`) to DJI devices that use the DJI Fly app, such as the DJI RC, RC2, Smart Controller, or any Android phone connected to a controller.

It provides a clear, two-pane interface to view your local mission files and the waypoint slots on your device, allowing you to replace missions and automatically generate new preview images.

---

## ✨ Key Features

-   **Dual Connection Modes**: Connect via **ADB** (Android Debug Bridge) for fast, reliable transfers, or **MTP** (Media Transfer Protocol) for simple, driver-free access.
-   **Intuitive Two-Pane View**: Easily manage your computer's local KMZ files and the waypoint missions on your connected DJI device side-by-side.
-   **Automatic Preview Generation**: When a mission is transferred, the app fetches a map tile from **ESRI's World Imagery service**, overlays it with the mission name and date, and uploads it as the new preview.
-   **Intelligent Device Detection**: Automatically scans and lists compatible Android devices that have the required DJI Fly folder structure.
-   **Real-time File Monitoring**: Your local KMZ source folder is watched for changes, and the file list updates automatically.
-   **Sorting**: Both local and device file lists can be sorted by name (using a natural string comparison), date, or size.
-   **Modern & Responsive UI**: Built with WPF for a clean, asynchronous, and user-friendly experience on Windows.

---

## 🚀 Getting Started

### Prerequisites

-   Windows 10 or newer.
-   [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (or newer).
-   A DJI controller (or Android device) that uses the DJI Fly App.

### Installation

1.  Navigate to the [**Releases**](https://github.com/Alos-no/DJI-Mission-Installer/releases) page.
2.  Download the `.zip` archive from the latest release.
3.  Extract the contents to a permanent folder on your computer.
4.  Run `DJI Mission Installer.exe`.

---

## 🔌 Connection Guide: ADB vs. MTP

This application supports two protocols for communicating with your device. The optimal choice depends on your controller model.

-   **ADB (Android Debug Bridge)**: This is the **preferred** method. It is faster, more reliable, and provides better device feedback. It requires a one-time setup on your controller to enable "USB Debugging."
-   **MTP (Media Transfer Protocol)**: This is the standard mode for transferring files, like a digital camera. It requires no special setup but can sometimes be slower or less reliable, occasionally requiring you to unplug and reconnect the device.

| Device Model                               | Recommended Mode | Setup Instructions                                                                                                                                                                                                                                                                   |
| ------------------------------------------ | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **DJI RC**, **RC Pro**, **Smart Controller** | **ADB**          | **1. Enable Developer Options:** Go to `Settings` > `About` and tap on the **Build Number** seven times until you see a "You are now a developer!" message. <br/> **2. Enable USB Debugging:** Go back to `Settings`, find the new `Developer Options` menu, and enable **USB Debugging**. |
| **Android Phone / Tablet**                 | **ADB**          | Follow the same steps as above for enabling Developer Options and USB Debugging in your device's Android settings.                                                                                                                                                                    |
| **DJI RC2**                                | **MTP**          | The DJI RC2 connects in MTP mode by default and does not officially support ADB. For the vast majority of users, **MTP is the only option**. Simply connect the controller to your PC with a USB cable and select the MTP radio button in the app.                             |

---

## 📋 Usage Guide

1.  **Launch the application**.
2.  **Connect your device**: Connect your DJI controller or Android phone to your computer via USB.
3.  **Select Connection Type**: At the top of the window, select the **ADB** or **MTP** radio button based on the guide above.
4.  **Refresh and Select Device**: Click the **Refresh** button. Your connected device should appear in the dropdown menu. Select it.
5.  **Choose a Source File**: In the left "KMZ Files" list, select the mission file you want to transfer. If the list is empty, click "Choose Folder" to select the directory where you store your `.kmz` files.
6.  **Choose a Destination Slot**: In the right "Device Waypoints" list, select the mission you wish to replace.
7.  **Transfer**: Click the **Transfer Selected File** button. A success message will appear once the process is complete. The device list will refresh to show the updated file.
8.  Safely disconnect your device. Your new mission is now ready in the DJI Fly app!

---

## 🛠 Building from Source

If you want to build the project yourself, follow these steps.

### Prerequisites

-   [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the ".NET desktop development" workload installed.
-   [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).

### Build Steps

1.  Clone the repository:
    ```sh
    git clone https://github.com/Alos-no/DJI-Mission-Installer.git
    ```
2.  Open the `DJI Mission Installer.sln` solution file in Visual Studio.
3.  Restore the NuGet packages (this should happen automatically).
4.  Build the solution (F6 or `Build > Build Solution`). The executable will be in `src/bin/Debug` or `src/bin/Release`.

---

## 🔧 Technical Details & Dependencies

This project is built with C# 12 and .NET 8, using the following key technologies and libraries:

-   **WPF**: For the graphical user interface.
-   **MVVM Pattern**: Using the `CommunityToolkit.Mvvm` library for a clean separation of UI and logic.
-   **[AdvancedSharpAdbClient](https://github.com/quamotion/madb)**: A .NET library for communicating with Android devices via the Android Debug Bridge (ADB).
-   **[MediaDevices](https://github.com/pvginkel/MediaDevices)**: For accessing device storage via the Media Transfer Protocol (MTP).
-   **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)**: A powerful, cross-platform 2D graphics library used for generating and watermarking the map preview images.
-   **ESRI ArcGIS REST Services**: Used to fetch satellite map tiles for the preview images.

---

## 🤝 Contributing

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".

1.  Fork the Project
2.  Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3.  Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4.  Push to the Branch (`git push origin feature/AmazingFeature`)
5.  Open a Pull Request

---

## 📄 License

Distributed under the MIT License. See `LICENSE.txt` for more information.
