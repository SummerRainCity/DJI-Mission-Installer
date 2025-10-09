# DJI Mission Installer

![DJI Mission Installer Screenshot](https://github.com/Alos-no/DJI-Mission-Installer/blob/main/docs/preview.jpg?raw=true)

![Language](https://img.shields.io/badge/language-C%23-blue.svg)
![Framework](https://img.shields.io/badge/framework-WPF%20/.NET%208-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**DJI Mission Installer** is a Windows desktop application designed to simplify the process of transferring custom waypoint mission files (`.kmz`) to DJI devices that use the DJI Fly app, such as the DJI RC, RC2, Smart Controller, or any Android phone connected to a controller.

It provides a clear, two-pane interface to view your local mission files and the waypoint slots on your device, allowing you to replace missions and automatically generate new preview images.

---

## ✨ Key Features

*   **Dual Connection Modes**: Supports connecting to your device via both **ADB** (Android Debug Bridge) for robust control and **MTP** (Media Transfer Protocol) for standard file access. The app automatically falls back to MTP if ADB is not configured.
*   **Intuitive Two-Pane View**: Easily see your computer's local KMZ files in one list and the waypoint missions on your connected DJI device in another.
*   **Automatic Preview Generation**: When you transfer a mission, the app automatically generates a new preview image for the DJI Fly app. This image includes the mission name and modification date overlaid on a map background. *(Note: Map imagery is currently placeholder but can be extended).*
*   **Intelligent Device Detection**: Scans and lists available Android devices with the correct DJI Fly folder structure.
*   **File Management**: Both local and device file lists can be sorted by name, date, or size using a natural string comparison for intuitive ordering.
*   **Modern & Responsive UI**: Built with WPF, featuring a clean layout, modern controls, and loading indicators for a smooth user experience.
*   **Auto-Refreshes Local Files**: Automatically watches your local KMZ source folder for any changes and updates the list in real-time.

---

## ⚙️ How It Works

The DJI Fly app stores its waypoint missions in a specific folder on the Android device's storage: `Android/data/dji.go.v5/files/waypoint/`. Each mission has its own folder named with a unique ID (GUID). Inside that folder are the mission data (`<guid>.kmz`) and a preview image (`<guid>.jpg`) which is located in a separate `map_preview` folder.

This application allows you to:
1.  **Connect** to your device using either ADB or MTP.
2.  **Select** a local `.kmz` file from your computer.
3.  **Select** a target waypoint "slot" on the device that you want to replace.
4.  **Transfer** the file. The application will:
    *   Delete the old `.kmz` and `.jpg` files from the selected device slot.
    *   Upload your new `.kmz` file.
    *   Generate a new preview map image using **ESRI's World Imagery service**.
    *   Overlay the mission name and date onto the map image.
    *   Upload the new preview image.

This ensures that when you open the DJI Fly app, your new mission appears correctly with an informative preview.

---

## 🚀 Getting Started

### Prerequisites

*   Windows 10 or newer.
*   [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
*   A DJI controller or Android device that uses the DJI Fly App.

### Installation

1.  Go to the [**Releases**](https://github.com/Alos-no/DJI-Mission-Installer/releases) page of this repository.
2.  Download the latest release `.zip` file.
3.  Extract the contents to a folder on your computer.
4.  Run `DJI Mission Installer.exe`.

---

## 📋 Usage

1.  **Configure your KMZ folder**: By default, the app looks for KMZ files in `My Documents\DJI\KMZ`. You can change this path in the `DJI Mission Installer.exe.config` file.
2.  **Launch the application**.
3.  **Connect your device**: Connect your DJI controller or Android phone to your computer via USB.
4.  **Select your device**: Choose your device from the dropdown menu at the top right and click **Refresh**. The app will list the existing waypoint missions from the device.
5.  **Select a source file**: In the left-hand "KMZ Files" list, select the mission file you want to install.
6.  **Select a destination slot**: In the right-hand "Device Waypoints" list, select the mission you want to replace.
7.  **Click "Transfer Selected File"**. A confirmation message will appear upon success.
8.  Safely disconnect your device. Your new mission is now ready in the DJI Fly app!

---

## 🛠 Building from Source

If you want to build the project yourself, follow these steps.

### Prerequisites

*   [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the ".NET desktop development" workload installed.
*   [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### Build Steps

1.  Clone the repository:
    ```sh
    git clone https://github.com/Alos-no/DJI-Mission-Installer.git
    ```
2.  Open the `DJI Mission Installer.sln` solution file in Visual Studio.
3.  Restore the NuGet packages (this should happen automatically).
4.  Build the solution (F6 or `Build > Build Solution`). The executable will be in `bin/Debug` or `bin/Release`.

---

## 🔧 Technical Details & Dependencies

This project is built with C# 12 and .NET 8, using the following key technologies and libraries:

*   **WPF**: For the graphical user interface.
*   **MVVM Pattern**: Using the `CommunityToolkit.Mvvm` library for a clean separation of UI and logic.
*   **[AdvancedSharpAdbClient](https://github.com/quamotion/madb)**: A .NET library for communicating with Android devices via the Android Debug Bridge (ADB).
*   **[MediaDevices](https://github.com/pvginkel/MediaDevices)**: For accessing device storage via the Media Transfer Protocol (MTP).
*   **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)**: A powerful, cross-platform 2D graphics library used for generating and watermarking the map preview images.
*   **ESRI ArcGIS REST Services**: Used to fetch satellite map tiles for the preview images.

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