#!/usr/bin/env python3
import hid
import socket
import threading
import time
import json
import signal
import sys

# -----------------------------
# Device IDs
# -----------------------------
VENDOR_ID = 0x06a3
PRODUCT_ID = 0x0d06

# -----------------------------
# TCP Bridge
# -----------------------------
BRIDGE_HOST = "127.0.0.1"
BRIDGE_PORT = 5555

# State tracking to avoid command spam
last_button_state = {}
last_knob_time = {}
last_mode = None

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.settimeout(5.0)
try:
    sock.connect((BRIDGE_HOST, BRIDGE_PORT))
    sock.setblocking(False)
except Exception as e:
    print("TCP connect failed:", e)
    sys.exit(1)

def send_cmd(cmd, data=None):
    pkt = {"cmd": cmd}
    if data is not None:
        pkt["data"] = data
    try:
        sock.sendall((json.dumps(pkt) + "\n").encode("ascii"))
    except Exception as e:
        print("Send error:", e)

def send_cmd_debounced(cmd, cooldown_ms=50):
    """Debounced command send for rotary encoder"""
    now = time.time() * 1000
    last = last_knob_time.get(cmd, 0)
    if now - last < cooldown_ms:
        return
    last_knob_time[cmd] = now
    send_cmd(cmd)
    print(f"KNOB: {cmd}")

def send_if_changed(button_name, new_state, cmd):
    """Only send if button state changed"""
    old_state = last_button_state.get(button_name, None)
    if old_state != new_state and new_state:  # Only on press (not release)
        last_button_state[button_name] = new_state
        print(f"BUTTON: {button_name} -> {cmd}")
        send_cmd(cmd)

# -----------------------------
# HID Open
# -----------------------------
dev = hid.device()
try:
    dev.open(VENDOR_ID, PRODUCT_ID)
    dev.set_nonblocking(True)
except Exception as e:
    print("HID open failed:", e)
    sys.exit(1)

print("Multi Panel opened.")

running = True
current_mode = None
latest_ap = {}

def handle_exit(sig, frame):
    global running
    running = False

signal.signal(signal.SIGINT, handle_exit)
signal.signal(signal.SIGTERM, handle_exit)

# -----------------------------
# Mode maps
# -----------------------------
MODE_NAMES = {
    1: "ALT",
    2: "VS",
    4: "IAS",
    8: "HDG",
    16: "CRS"
}

BUTTON_NAMES = {
    1: "HDG",
    2: "NAV",
    4: "IAS",
    8: "ALT",
    16: "VS",
    32: "APR",
    64: "REV"
}

# -----------------------------
# Display writer - COMPLETE PROTOCOL
# -----------------------------
def set_multi_display(alt=0, vs=0, ias=0, hdg=0, crs=0, ap_on=False, mode_leds=0, current_mode=None):
    """
    Multi Panel HID output report structure (13 bytes):
    [0] = Report ID (0x00)
    [1-5] = UPPER display - MODE DEPENDENT (shows value for current mode)
    [6-10] = LOWER display - ALWAYS shows VS (vertical speed, with leading zero suppressed)
    [11] = LED byte - controls AP MODE BUTTON LEDs (lower row)
    [12] = Unknown/unused
    
    Upper display shows different values based on mode selector:
    - ALT mode: altitude (e.g., "17000")
    - VS mode: altitude (e.g., "17000") - same as ALT mode!
    - IAS mode: airspeed (e.g., "  150")
    - HDG mode: heading (e.g., "  210")
    - CRS mode: course (e.g., "  090")
    
    Lower display ALWAYS shows VS (e.g., " -100" not "-0100" for < 1000).
    Note: In both ALT and VS modes, upper=ALT and lower=VS (matches panel labels)
    
    LED byte (byte 11) - CONFIRMED SCRAMBLED HARDWARE MAPPING:
    Bit 0 (0x01) → AP master LED ✓
    Bit 1 (0x02) → HDG LED ✓
    Bit 2 (0x04) → NAV LED ✓
    Bit 3 (0x08) → IAS LED ✓ (FLC mode)
    Bit 4 (0x10) → ALT LED ✓
    Bit 5 (0x20) → VS LED ✓
    Bit 6 (0x40) → APR LED ✓
    Bit 7 (0x80) → REV LED ✓
    BC mode: Sets bits 0x40 AND 0x80 (lights both APR and REV)
    
    VS minus sign: 0xDE (tested and confirmed)
    
    Command name corrections:
    - NAV button sends: AP_NAV1_HOLD (not AP_NAV_HOLD)
    - REV button sends: AP_BACKCOURSE_HOLD (not AP_REV_HOLD)
    - IAS button sends: AP_AIRSPEED_HOLD (not AP_IAS_HOLD)
    """
    buf = bytearray(13)
    buf[0] = 0x00  # Report ID
    
    # Initialize all display bytes to blank (0xFF)
    for i in range(1, 11):
        buf[i] = 0xFF
    
    # UPPER display (bytes 1-5) - MODE DEPENDENT
    if current_mode == 1:  # ALT mode
        alt_str = f"{int(abs(alt)):05d}"[-5:]
        for i, ch in enumerate(alt_str):
            buf[1 + i] = ord(ch) - ord('0')
            
    elif current_mode == 2:  # VS mode - show ALT on upper (same as ALT mode)
        alt_str = f"{int(abs(alt)):05d}"[-5:]
        for i, ch in enumerate(alt_str):
            buf[1 + i] = ord(ch) - ord('0')
            
    elif current_mode == 4:  # IAS mode
        ias_str = f"{int(abs(ias)):03d}"[-3:]
        buf[1] = 0xFF  # Blank
        buf[2] = 0xFF  # Blank
        for i, ch in enumerate(ias_str):
            buf[3 + i] = ord(ch) - ord('0')
            
    elif current_mode == 8:  # HDG mode
        hdg_val = int(abs(hdg)) % 360
        hdg_str = f"{hdg_val:03d}"
        buf[1] = 0xFF  # Blank
        buf[2] = 0xFF  # Blank
        for i, ch in enumerate(hdg_str):
            buf[3 + i] = ord(ch) - ord('0')
            
    elif current_mode == 16:  # CRS mode
        crs_val = int(abs(crs)) % 360
        crs_str = f"{crs_val:03d}"
        buf[1] = 0xFF  # Blank
        buf[2] = 0xFF  # Blank
        for i, ch in enumerate(crs_str):
            buf[3 + i] = ord(ch) - ord('0')
    else:
        # Default: show altitude if mode unknown
        alt_str = f"{int(abs(alt)):05d}"[-5:]
        for i, ch in enumerate(alt_str):
            buf[1 + i] = ord(ch) - ord('0')
    
    # LOWER display (bytes 6-10) - ALWAYS show VS
    if vs < 0:
        buf[6] = 0xDE  # Minus sign (tested: 0xDE works!)
    else:
        buf[6] = 0xFF  # Blank for positive
    
    # Format VS value and remove leading zero if < 1000
    abs_vs = int(abs(vs))
    vs_str = f"{abs_vs:04d}"[-4:]
    
    # Suppress leading zero for values < 1000 (e.g., "-100" not "-0100")
    if abs_vs < 1000:
        buf[7] = 0xFF  # Blank the leading zero
        for i, ch in enumerate(vs_str[1:]):  # Skip first digit
            buf[8 + i] = ord(ch) - ord('0')
    else:
        for i, ch in enumerate(vs_str):
            buf[7 + i] = ord(ch) - ord('0')
    
    # LED byte (byte 11) - controls AP mode button LEDs
    # These are the LOWER row buttons (HDG, NAV, IAS, ALT, VS, APR, REV)
    # NOT the mode selector LEDs (those might be hardware)
    buf[11] = mode_leds & 0xFF
    buf[12] = 0x00
    
    try:
        dev.write(list(buf))
    except Exception as e:
        print(f"Display write error: {e}")

# -----------------------------
# Bridge receiver
# -----------------------------
def bridge_receiver():
    global latest_ap
    buf = ""
    while running:
        try:
            data = sock.recv(4096)
            if not data:
                time.sleep(0.01)
                continue
            buf += data.decode("ascii", errors="ignore")
            while "\n" in buf:
                line, buf = buf.split("\n", 1)
                if not line.strip():
                    continue
                try:
                    obj = json.loads(line)
                    if obj.get("type") == "autopilot":
                        latest_ap = obj
                        # Debug: show AP state changes
                        # print(f"AP DATA: master={obj.get('ap_master', 0)} alt={obj.get('alt', 0)} vs={obj.get('vs', 0)}")
                except:
                    pass
        except BlockingIOError:
            time.sleep(0.01)
        except Exception as e:
            print("Bridge recv error:", e)
            time.sleep(0.1)

threading.Thread(target=bridge_receiver, daemon=True).start()

# -----------------------------
# Main HID Loop
# -----------------------------
print("Multi Panel decoder running...")
print("Controls:")
print("  Mode selector: ALT, VS, IAS, HDG, CRS")
print("  Rotary knob: Adjust selected value")
print("  AP button: Toggle autopilot master")
print("  Lower buttons: HDG, NAV, IAS, ALT, VS, APR, REV")
print("  Flaps lever: Up/Down")
print("  Pitch trim wheel: Up/Down")
print("  Auto throttle switch: ARM/OFF")
print()
print("Display behavior:")
print("  UPPER display: Shows value based on mode selector")
print("    ALT mode → altitude, VS mode → VS, IAS mode → airspeed")
print("    HDG mode → heading, CRS mode → course")
print("  LOWER display: Always shows VS (vertical speed)")
print()

while running:
    try:
        data = dev.read(3)
        
        # Process HID input if available
        if data and len(data) >= 3:
            b1, b2, b3 = data

            # Mode selector (lower bits of b1)
            mode_bits = b1 & 0x1F  # Mask out upper bits
            if mode_bits in MODE_NAMES:
                if current_mode != mode_bits:
                    current_mode = mode_bits
                    mode_name = MODE_NAMES[current_mode]
                    print(f"MODE changed to: {mode_name}")

            # Rotary knob (bits 5-6 of b1)
            rot = b1 & 0x60
            if rot in (0x20, 0x40) and current_mode in MODE_NAMES:
                cw = (rot == 0x20)
                mode = MODE_NAMES[current_mode]

                if mode == "ALT":
                    send_cmd_debounced("AP_ALT_VAR_INC" if cw else "AP_ALT_VAR_DEC")
                elif mode == "VS":
                    send_cmd_debounced("AP_VS_VAR_INC" if cw else "AP_VS_VAR_DEC")
                elif mode == "IAS":
                    send_cmd_debounced("AP_SPD_VAR_INC" if cw else "AP_SPD_VAR_DEC")
                elif mode == "HDG":
                    send_cmd_debounced("HEADING_BUG_INC" if cw else "HEADING_BUG_DEC")
                elif mode == "CRS":
                    send_cmd_debounced("VOR1_OBI_INC" if cw else "VOR1_OBI_DEC")

            # AP master button (bit 7 of b1) - TOGGLE button, needs debouncing
            ap_pressed = bool(b1 & 0x80)
            if ap_pressed and not last_button_state.get("ap_master", False):
                send_cmd("AP_MASTER")
                print("AP MASTER toggled")
            last_button_state["ap_master"] = ap_pressed

            # Auto throttle switch (b3=0, b2=0 or 128)
            # This is a 2-position switch that sends TOGGLE command on position change
            if b3 == 0 and b2 in (0, 128):
                current_at_pos = (b2 == 128)
                last_at_pos = last_button_state.get("auto_throttle_pos", None)
                
                # Send command on switch position change (edge detection)
                if last_at_pos is not None and current_at_pos != last_at_pos:
                    print(f"AUTO THROTTLE switch moved to: {'ARM' if current_at_pos else 'OFF'}")
                    send_cmd("AUTO_THROTTLE_ARM")
                
                last_button_state["auto_throttle_pos"] = current_at_pos

            # AP mode buttons (b3=0, b2 has button bit)
            # These are TOGGLE commands - send every press, not state-tracked
            if b3 == 0 and b2 != 0 and b2 != 128:
                btn = b2 & 0x7F
                if btn in BUTTON_NAMES:
                    name = BUTTON_NAMES[btn]
                    # Special cases for command names that don't match button names
                    if name == "IAS":
                        cmd = "AP_AIRSPEED_HOLD"
                        print(f"BUTTON PRESSED: {name} → Sending: {cmd}")
                        send_cmd(cmd)
                        # G1000 might use different event - try Flight Director if this doesn't work
                        # Alternative: "FLIGHT_LEVEL_CHANGE" or "FLIGHT_LEVEL_CHANGE_ON"
                    elif name == "NAV":
                        cmd = "AP_NAV1_HOLD"
                        print(f"BUTTON PRESSED: {name} → Sending: {cmd}")
                        send_cmd(cmd)
                    elif name == "REV":
                        cmd = "AP_BACKCOURSE_HOLD"
                        print(f"BUTTON PRESSED: {name} → Sending: {cmd}")
                        send_cmd(cmd)
                        # If this doesn't work, BC mode might need different command for G1000
                    elif name == "APR":
                        cmd = "AP_APR_HOLD"
                        print(f"BUTTON PRESSED: {name} → Sending: {cmd}")
                        send_cmd(cmd)
                    else:
                        cmd = f"AP_{name}_HOLD"
                        print(f"BUTTON PRESSED: {name} → Sending: {cmd}")
                        send_cmd(cmd)

            # Flaps lever - MOMENTARY control (send continuously while held)
            if b3 == 1:
                send_cmd("FLAPS_DECR")
            elif b3 == 2:
                send_cmd("FLAPS_INCR")

            # Pitch trim wheel - MOMENTARY control (send continuously while held)
            if b3 == 4:
                send_cmd("ELEV_TRIM_DN")
            elif b3 == 8:
                send_cmd("ELEV_TRIM_UP")

        # Display update - ALWAYS update every loop for instant feedback
        if latest_ap:
            ap_master = bool(latest_ap.get("ap_master", 0))
            
            # Check if any AP state has changed (for comprehensive debugging)
            current_ap_state = (
                latest_ap.get("ap_hdg_hold", 0),
                latest_ap.get("ap_nav1_hold", 0),
                latest_ap.get("ap_airspeed_hold_flag", 0),
                latest_ap.get("ap_alt_hold", 0),
                latest_ap.get("ap_vs_hold", 0),
                latest_ap.get("ap_approach_hold", 0),
                latest_ap.get("ap_backcourse_hold", 0),
            )
            
            last_ap_state = last_button_state.get("_last_ap_state", None)
            
            # Print debug when ANY AP state changes (not just LEDs)
            if current_ap_state != last_ap_state:
                print(f"\n=== AP STATE CHANGE ===")
                print(f"FULL AP DATA: {latest_ap}")
                print(f"AP Master: {ap_master}")
                print(f"HDG hold: {latest_ap.get('ap_hdg_hold',0)}")
                print(f"NAV hold: {latest_ap.get('ap_nav1_hold',0)}")
                print(f"IAS/Airspeed hold flag: {latest_ap.get('ap_airspeed_hold_flag',0)}")
                print(f"ALT hold: {latest_ap.get('ap_alt_hold',0)}")
                print(f"VS hold: {latest_ap.get('ap_vs_hold',0)}")
                print(f"APR hold: {latest_ap.get('ap_approach_hold',0)}")
                print(f"BC hold: {latest_ap.get('ap_backcourse_hold',0)}")
                last_button_state["_last_ap_state"] = current_ap_state
            
            # LED byte controls AP MODE BUTTONS (lower row), NOT mode selector
            # Hardware has scrambled bit mapping - need translation table
            # Based on testing:
            # - Bit 0x01 → AP LED ✓
            # - Bit 0x02 → HDG LED ✓
            # - Bit 0x04 → NAV LED ✓ (assumed)
            # - Bit 0x08 → IAS LED ✓
            # - Bit 0x10 → ALT LED ✓
            # - Bit 0x20 → VS LED ✓
            # - Bit 0x40 → APR LED ✓
            # - Bit 0x80 → REV LED ✓ (for backcourse)
            
            mode_leds = 0
            
            # AP mode engagement LEDs - CORRECTED BIT MAPPING
            if latest_ap.get("ap_hdg_hold", 0):
                mode_leds |= 0x02  # HDG LED
            if latest_ap.get("ap_nav1_hold", 0):
                mode_leds |= 0x04  # NAV LED
            if latest_ap.get("ap_airspeed_hold_flag", 0):
                mode_leds |= 0x08  # IAS LED (FLC mode)
            if latest_ap.get("ap_alt_hold", 0):
                mode_leds |= 0x10  # ALT LED
            if latest_ap.get("ap_vs_hold", 0):
                mode_leds |= 0x20  # VS LED
            
            # BC and APR handling - BC might set both flags in sim
            bc_active = latest_ap.get("ap_backcourse_hold", 0)
            apr_active = latest_ap.get("ap_approach_hold", 0)
            
            if bc_active:
                # BC mode - only light REV LED (even if APR flag also set)
                mode_leds |= 0x80  # REV LED only
            elif apr_active:
                # APR mode - only if BC not active
                mode_leds |= 0x40  # APR LED
            
            # AP master LED
            if ap_master:
                mode_leds |= 0x01  # AP LED
            
            set_multi_display(
                alt=int(latest_ap.get("alt", 0)),
                vs=int(latest_ap.get("vs", 0)),
                ias=int(latest_ap.get("ias", 0)),
                hdg=int(latest_ap.get("hdg", 0)),
                crs=int(latest_ap.get("crs", 0)),
                ap_on=ap_master,
                mode_leds=mode_leds,
                current_mode=current_mode
            )
        
        # Small sleep to prevent CPU hammering
        time.sleep(0.005)

    except Exception as e:
        print("Read error:", e)
        import traceback
        traceback.print_exc()
        time.sleep(0.1)

# Cleanup
try:
    dev.close()
except:
    pass
try:
    sock.close()
except:
    pass

print("Multi Panel closed.")
