# SimConnectBridge - C# SimConnect Bridge

C# application that bridges SimConnect (MSFS) with Python panel scripts via TCP.

## 📋 Prerequisites

### Required Software

1. **.NET 8.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Linux: `sudo dnf install dotnet-sdk-8.0` (Fedora) or `sudo apt install dotnet-sdk-8.0` (Ubuntu)
   - Windows: Download and run the installer

2. **SimConnect SDK**
   - Included with MSFS 2020 SDK
   - Or extract from your MSFS installation

3. **Newtonsoft.Json** (installed automatically via NuGet)

## 🔍 Getting SimConnect.dll

SimConnect.dll is located in your MSFS installation. Find it at:

**Steam + Proton Installation:**
```bash
~/.steam/steam/steamapps/common/MicrosoftFlightSimulator/FlightSimulator.exe_Data/Plugins/x64/SimConnect.dll
```

**Alternative Locations:**
- MSFS SDK installation
- `C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll` (Windows SDK install)

### Copy SimConnect.dll to Project

```bash
# Navigate to bridge directory
cd bridge

# Copy SimConnect.dll here
cp ~/.steam/steam/steamapps/common/MicrosoftFlightSimulator/FlightSimulator.exe_Data/Plugins/x64/SimConnect.dll .
```

## 🛠️ Building on Linux

### Method 1: Using dotnet CLI (Recommended)

```bash
cd bridge

# Restore dependencies (downloads Newtonsoft.Json)
dotnet restore

# Build the project
dotnet build -c Release

# Output will be in:
# bin/Release/net8.0/SimConnectBridge.exe
```

### Method 2: Visual Studio Code

1. Install **C# Dev Kit** extension
2. Open `bridge/` folder in VS Code
3. Press F5 to build and debug
4. Or use Terminal → Run Build Task

## 🛠️ Building on Windows

### Method 1: Visual Studio 2022

1. Open `SimConnectBridge.csproj` in Visual Studio
2. Build → Build Solution (Ctrl+Shift+B)
3. Output: `bin\Release\net8.0\SimConnectBridge.exe`

### Method 2: Command Line

```cmd
cd bridge
dotnet restore
dotnet build -c Release
```

## 📦 Project Structure

```
bridge/
├── Program.cs              - Main bridge logic
├── TcpCommandServer.cs     - TCP server class
├── SimConnectBridge.csproj - Project configuration
└── SimConnect.dll          - SimConnect library (you provide)
```

## ⚙️ Configuration

### SimConnect Settings (Hardcoded)

- **SimConnect Port**: 50000 (MSFS default)
- **TCP Server Port**: 5555 (panels connect here)

If you need different ports, edit `Program.cs`:

```csharp
// Line ~350 - SimConnect port
sender.Open("SimConnectBridge", null, 50000, false);

// TcpCommandServer.cs - TCP port
server = new TcpCommandServer(5555, HandleJsonCommand);
```

### MSFS SimConnect Configuration

Create or edit: `%APPDATA%\Microsoft Flight Simulator\SimConnect.cfg`

```ini
[SimConnect]
Protocol=IPv4
Address=127.0.0.1
Port=50000
MaxClients=64
MaxRecvSize=8192
DisableNagle=0
```

## 🚀 Running the Bridge

### On Linux (via Wine/Proton)

```bash
cd bridge

# Using your Steam Proton prefix
WINEPREFIX=~/.steam/steam/steamapps/compatdata/1250410/pfx \
wine bin/Release/net8.0/SimConnectBridge.exe
```

### Auto-start with MSFS

1. **Copy the .exe to your MSFS Proton prefix:**

```bash
cp bin/Release/net8.0/SimConnectBridge.exe \
   ~/.steam/steam/steamapps/compatdata/1250410/pfx/drive_c/
```

2. **Edit exe.xml:**

Location: `~/.steam/steam/steamapps/compatdata/1250410/pfx/drive_c/users/steamuser/AppData/Roaming/Microsoft Flight Simulator/exe.xml`

Add this section:

```xml
<Launch.Addon>
    <Name>SimConnectBridge</Name>
    <Disabled>False</Disabled>
    <Path>C:\SimConnectBridge.exe</Path>
</Launch.Addon>
```

Now the bridge starts automatically when MSFS launches!

### On Windows (Native)

```cmd
cd bridge
bin\Release\net8.0\SimConnectBridge.exe
```

## 🐛 Troubleshooting

### "SimConnect.dll not found"

**Solution**: Copy SimConnect.dll to the same folder as SimConnectBridge.exe

```bash
cp ~/.steam/steam/steamapps/common/MicrosoftFlightSimulator/FlightSimulator.exe_Data/Plugins/x64/SimConnect.dll \
   bin/Release/net8.0/
```

### "Failed to connect to SimConnect"

**Possible causes:**
1. MSFS not running
2. SimConnect port blocked
3. Wrong Wine prefix path

**Test**: Start MSFS first, then run the bridge. Console should show:
```
SimConnect initialized
TCP server listening on port 5555
```

### "Could not load file or assembly 'Newtonsoft.Json'"

**Solution**: Restore NuGet packages

```bash
dotnet restore
dotnet build -c Release
```

### "Platform not supported" error on Linux

**Solution**: Ensure you're running via Wine, not native .NET:

```bash
# WRONG (tries to run natively)
dotnet run

# CORRECT (runs via Wine)
WINEPREFIX=~/.steam/steam/steamapps/compatdata/1250410/pfx \
wine bin/Release/net8.0/SimConnectBridge.exe
```

## 📊 Testing the Bridge

### 1. Start MSFS

Load into any aircraft

### 2. Run the bridge

Console output should show:
```
SimConnect initialized
TCP server listening on port 5555
```

### 3. Test TCP connection

From another terminal:
```bash
nc localhost 5555
# You should see JSON data streaming
```

### 4. Run panel scripts

```bash
cd ../panels
python3 switchpanel_complete.py
```

Panel should now respond to sim state!

## 🔧 Advanced Configuration

### Build for Different Platforms

```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained

# Linux x64 (native, not Wine)
dotnet publish -c Release -r linux-x64 --self-contained
```

### Debug Build

```bash
# Build with debugging symbols
dotnet build -c Debug

# Run with verbose output
WINEPREFIX=~/.steam/steam/steamapps/compatdata/1250410/pfx \
wine bin/Debug/net8.0/SimConnectBridge.exe
```

## 📝 Notes

- The bridge must run in the **same Wine prefix** as MSFS for SimConnect to work
- TCP port 5555 must be free (check with `netstat -tulpn | grep 5555`)
- SimConnect.dll version should match your MSFS installation
- Newtonsoft.Json is automatically downloaded via NuGet restore

## 🎯 Next Steps

After building successfully:

1. Copy .exe to Wine prefix (optional but recommended)
2. Add to exe.xml for auto-start
3. Test with a panel script
4. See [panels/README.md](../panels/README.md) for running the panel scripts

## 📚 Related Documentation

- [Main README](../README.md) - Project overview
- [Panel Scripts](../panels/README.md) - How to run the Python panels
- [Protocol Docs](../docs/PROTOCOL.md) - Technical details

---

Need help? [Open an issue](https://github.com/Sigfreid74/SimConnectBridge/issues)!
