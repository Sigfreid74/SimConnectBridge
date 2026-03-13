# Linux Setup - udev Rules

## Install udev Rules

This allows non-root access to Logitech panels:
```bash
sudo cp 99-logitech-panels.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules
sudo udevadm trigger
```

## Unplug and replug panels

The panels should now be accessible without sudo.

## Verify
```bash
ls -l /dev/hidraw*
# Should show group ownership as 'plugdev' or your user group
```

## Supported Panels
- Multi Panel (PID: 0x0D06)
- Switch Panel (PID: 0x0D67)
- Radio Panel (PID: 0x0D05)
