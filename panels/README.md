# Panel Python Scripts

## Requirements
```bash
pip install hidapi
```

## Running Individual Panels
```bash
python3 switchpanel_complete.py &
python3 radiopanel_with_xpdr_blink.py &
python3 multipanel_improved.py &
```

## Running All Panels (Convenient)
```bash
./launch_all_panels.sh
```

## Configuration
All panels connect to TCP port 5555 (localhost).
Ensure SimConnectBridge is running first.

## Troubleshooting
- **Permission denied**: Install udev rules (see [linux/](../linux/))
- **Connection refused**: Start SimConnectBridge first
- **No response**: Check MSFS is running and SimConnect is enabled
