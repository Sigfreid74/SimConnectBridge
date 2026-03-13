# SimConnectBridge - Logitech Flight Panels for MSFS on Linux

Native Linux support for Logitech Flight Instrument Panels with Microsoft Flight Simulator 2020 running via Proton/Wine.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Linux-orange.svg)
![MSFS](https://img.shields.io/badge/MSFS-2020-green.svg)

## ✈️ Supported Hardware

- ✅ **Logitech Multi Panel** - Full autopilot control with G1000 FLC/BC support
- ✅ **Logitech Switch Panel** - All switches, lights, and gear LEDs
- ✅ **Logitech Radio Panel** - Com/Nav/ADF/DME/Transponder with proper display formatting

## 🎯 Features

- **Full Bidirectional Communication** - Read and write to all panels
- **All LEDs Working** - Correct LED mappings for all panel indicators
- **Proper Display Formatting** - Leading zero suppression, minus signs, frequency formatting
- **G1000 Support** - FLC (Flight Level Change) and BC (Backcourse) modes
- **Dual Systems** - Battery 1+2, Alternator 1+2 support
- **Native Linux USB** - Rock-solid hidapi integration
- **Proton Compatible** - Works with Steam MSFS via Wine/Proton

## 🏗️ Architecture

```
┌─────────────────────┐
│  Logitech Panels    │
│  (USB HID)          │
└──────────┬──────────┘
           │
    ┌──────▼──────────┐
    │  Python Scripts │  ← Native Linux (hidapi)
    │  (panels/)      │
    └──────┬──────────┘
           │
      TCP :5555
           │
    ┌──────▼──────────┐
    │  C# Bridge      │  ← Wine/Proton
    │  SimConnect     │
    └──────┬──────────┘
           │
    ┌──────▼──────────┐
    │  MSFS 2020      │
    └─────────────────┘
```

## 📋 Requirements

### System
- **Linux** (tested on Fedora, should work on Ubuntu/Debian/Arch)
- **MSFS 2020** (Steam version)
- **Proton** (GE-Proton recommended)

### Software
- **Python 3.x** with hidapi (`pip install hidapi`)
- **.NET 8.0 SDK** (for building the C# bridge)
- **Wine** (included with Proton)

### Hardware
- One or more Logitech Flight Panels:
  - Multi Panel (PID: 0x0D06)
  - Switch Panel (PID: 0x0D67)
  - Radio Panel (PID: 0x0D05)

## 🚀 Quick Start

### 1. Install udev Rules (Linux USB Access)

```bash
sudo cp linux/99-logitech-panels.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules
sudo udevadm trigger
```

Unplug and replug your panels.

### 2. Build the C# Bridge

See [bridge/README.md](bridge/README.md) for detailed build instructions.

Unfortunately this must be done on a windows computer or virtual environment using Visual Studio
```
copy SimConnectBridge.exe into your MSFS2020 Proton Prefix directory.

### 3. Install Python Dependencies

```bash
pip install hidapi
```

### 4. Run Everything

**Start SimConnectBridge with MSFS2020**
edit exe.xml inside:

YOUR STEAM LIBARY FOLDER /steamapps/compatdata/1250410/pfx/drive_c/users/steamuser/AppData/Roaming/Microsoft Flight Simulator/

add a 'Launch.Addon' section:

<Launch.Addon>
        <Name>SimConnectBridge</Name>
        <Disabled>False</Disabled>
        <Path>C:\SimConnectBridge.exe</Path>
        <CommandLine></CommandLine>
</Launch.Addon>


**Start the Panel Reader(s)**
```bash
cd panels
./launch_all_panels.sh
```

Or manually:
```bash
python3 switchpanel_bridge.py &
python3 radiopanel_bridge.py &
python3 multipanel_bridge.py &
```

### 5. Start MSFS and Fly!

The panels should now be fully functional.

## 📖 Documentation

- **[Protocol Documentation](docs/PROTOCOL.md)** - HID protocol reverse engineering
- **[LED Mappings](docs/LED_MAPPING.md)** - Complete LED bit mapping reference
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues and solutions
- **[Changelog](docs/CHANGELOG.md)** - Version history

### Component-Specific Docs
- [C# Bridge Build Guide](bridge/README.md)
- [Panel Scripts Guide](panels/README.md)
- [Linux Setup Guide](linux/README.md)

## 🎮 Panel Features

### Multi Panel
- ✅ All autopilot modes (HDG, NAV, IAS, ALT, VS, APR, REV)
- ✅ Autopilot master toggle
- ✅ Autothrottle arm/off
- ✅ Altitude, VS, IAS, HDG, CRS adjustment knobs
- ✅ Flaps and trim controls
- ✅ All mode LEDs working correctly
- ✅ G1000 FLC (Flight Level Change) support
- ✅ G1000 BC (Backcourse) support
- ✅ Proper display formatting (VS with minus sign, no leading zeros)

### Switch Panel
- ✅ All switches (Battery, Alternator, Avionics, Fuel Pump, etc.)
- ✅ Magneto positions (OFF, R, L, BOTH, START)
- ✅ Gear up/down with proper LED feedback
- ✅ All light switches with LED feedback
- ✅ Dual battery (Battery 1 + Battery 2)
- ✅ Dual alternator (Alternator 1 + Alternator 2)
- ✅ Cowl flaps with correct data range

### Radio Panel
- ✅ COM1, COM2, NAV1, NAV2 frequency selection
- ✅ ADF frequency with proper formatting
- ✅ DME display with speed, distance, time
- ✅ Transponder with ident blink
- ✅ Swap buttons for all radios
- ✅ Leading zero suppression where appropriate
- ✅ Proper decimal point placement

## 🐛 Troubleshooting

### Panels not detected
```bash
# Check if panels are visible
lsusb | grep Logitech

# Check hidraw permissions
ls -l /dev/hidraw*

# Verify udev rules are installed
ls -l /etc/udev/rules.d/99-logitech-panels.rules
```

### Bridge won't connect
- Ensure MSFS is running
- Check SimConnect port (default: 50000)
- Verify Wine prefix path is correct
- Check bridge console for errors

### Panels not responding
- Check bridge is running (`nc -z localhost 5555`)
- Verify Python scripts can connect to TCP port 5555
- Check for permission errors in panel script output

See [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) for more help.

## 🤝 Contributing

Contributions welcome! Areas of interest:
- Additional panel support (e.g., Yoke LCD)
- Support for other aircraft/autopilot systems
- Performance optimizations
- Documentation improvements
- Bug reports and fixes

Please open an issue or pull request!

## 📜 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🙏 Credits

- **Reverse Engineering & Development**: [Your Name/GitHub Handle]
- **SimConnect**: Microsoft Flight Simulator SDK
- **hidapi**: Signal 11 Software
- **Logitech**: For creating these awesome panels!

## ⭐ Acknowledgments

Special thanks to:
- The Linux flight sim community
- Proton/Wine developers for making MSFS possible on Linux
- Everyone who tested and provided feedback

---

**Enjoy your flights!** ✈️

If this project helped you, please consider giving it a star ⭐
