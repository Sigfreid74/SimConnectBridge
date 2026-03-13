#!/usr/bin/env python3
import hid
import socket
import threading
import time
import json
import signal
import sys
import math
import traceback

# -----------------------------
# Device IDs
# -----------------------------
VENDOR_ID = 0x06a3
PRODUCT_ID = 0x0d05

# -----------------------------
# TCP Bridge
# -----------------------------
BRIDGE_HOST = "127.0.0.1"
BRIDGE_PORT = 5555

# -----------------------------
# Globals and state
# -----------------------------
running = True
latest_radio = {}
last_event_time = {}
last_swap_time = {}

# Modes (kept global so display refresher can read them)
upper_mode = "COM1"
lower_mode = "COM1"

# XPDR digit selection state (0..3 -> 1000,100,10,1)
xpdr_selected_digit_upper = 0
xpdr_selected_digit_lower = 0

# Knob stabilization / accumulation
knob_last_dir = {}
knob_last_time = {}
knob_cooldown = {}
knob_accumulator = {}
knob_last_accum_time = {}

# XPDR blink state
xpdr_blink_state = True
xpdr_blink_interval = 0.4   # seconds; change to taste

# -----------------------------
# Signal handling
# -----------------------------
def handle_exit(sig, frame):
    global running
    running = False

signal.signal(signal.SIGINT, handle_exit)
signal.signal(signal.SIGTERM, handle_exit)

# -----------------------------
# TCP connection to bridge
# -----------------------------
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
        # debug
        # print(f"SENT: {pkt}")
    except Exception as e:
        print("Send error:", e)

# -----------------------------
# Mode maps
# -----------------------------
MODES_UPPER = {
    0x01: "COM1",
    0x02: "COM2",
    0x04: "NAV1",
    0x08: "NAV2",
    0x10: "ADF",
    0x20: "DME",
    0x40: "XPDR"
}

MODES_LOWER = {
    0x00: "COM1",
    0x01: "COM2",
    0x02: "NAV1",
    0x04: "NAV2",
    0x08: "ADF",
    0x10: "DME",
    0x20: "XPDR"
}

# -----------------------------
# DME encoder constants & functions
# -----------------------------
DECIMAL_MARK = 0xD0
BLANK_BYTE = 0xFF
MAX_DME = 999.9

def dme_format_string(dist_nm):
    try:
        f = float(dist_nm)
    except Exception:
        return None
    # Allow 0.0 and small values - changed from f <= 0.0 to f < 0.0
    if not math.isfinite(f) or f < 0.0 or f > MAX_DME:
        return None
    f = round(f, 1)
    return f"{f:.1f}"

def encode_dme_field(dist_nm):
    s = dme_format_string(dist_nm)
    blank = [BLANK_BYTE] * 5
    if s is None:
        return None, blank
    int_part, frac = s.split('.')
    digits = list(int_part + frac)
    if len(digits) > 4:
        digits = digits[-4:]
    field = [BLANK_BYTE] * 5
    start = 1 + (4 - len(digits))
    for i, ch in enumerate(digits):
        idx = start + i
        if ch.isdigit():
            field[idx] = ord(ch) - ord('0')
    if len(digits) >= 2:
        dec_idx = start + len(digits) - 2
        if 0 <= dec_idx < 5 and field[dec_idx] != BLANK_BYTE:
            field[dec_idx] |= DECIMAL_MARK
    return s, field

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

print("Radio Panel opened.")

# -----------------------------
# Display encoding for COM/NAV/ADF/XPDR
# -----------------------------
def encode_display(value, mode="COM1"):
    buf = bytearray([0xFF] * 5)

    def to_int_safe(x, default=None):
        if x is None:
            return default
        s = str(x).strip()
        if s == "" or s == "-" or s.lower() == "nan":
            return default
        digits = "".join(ch for ch in s if ch.isdigit() or ch == '.')
        if digits == "" or digits == ".":
            return default
        try:
            if '.' in digits:
                return float(digits)
            return int(digits)
        except:
            return default

    try:
        if mode in ["COM1", "COM2", "NAV1", "NAV2"]:
            try:
                f = float(value)
                # Format like 118.30 -> "11830" then take first 5 digits
                s = f"{f:06.2f}".replace(".", "")[:5]
            except:
                n = to_int_safe(value, default=0)
                s = str(n).rjust(5, "0")[:5]
            digits = [int(c) for c in s]
            for i in range(5):
                val = digits[i] & 0x0F
                if i == 2:
                    val |= DECIMAL_MARK
                buf[i] = val

        elif mode == "ADF":
            n = to_int_safe(value, default=None)
            if n is None:
                return buf
            if n > 10000:
                n = n / 1000.0
            n = float(n)
            if n >= 1000:
                s = f"{n:05.1f}".replace(".", "")
                buf[0] = int(s[0]) & 0x0F
                buf[1] = int(s[1]) & 0x0F
                buf[2] = int(s[2]) & 0x0F
                buf[3] = (int(s[3]) & 0x0F) | DECIMAL_MARK
                buf[4] = int(s[4]) & 0x0F
            else:
                s = f"{n:04.1f}".replace(".", "")
                buf[0] = 0xFF
                buf[1] = int(s[0]) & 0x0F
                buf[2] = int(s[1]) & 0x0F
                buf[3] = (int(s[2]) & 0x0F) | DECIMAL_MARK
                buf[4] = int(s[3]) & 0x0F

        elif mode == "XPDR":
            n = to_int_safe(value, default=None)
            if n is None:
                return buf
            n = int(n)
            s = f"{n:04d}"[-4:]
            buf[1] = int(s[0]) & 0x0F
            buf[2] = int(s[1]) & 0x0F
            buf[3] = int(s[2]) & 0x0F
            buf[4] = int(s[3]) & 0x0F

        elif mode == "DME":
            # DME displays distance and speed as decimals
            # Format as XX.X or _X.X (blank leading zero for < 10)
            try:
                f = float(value) if value is not None else 0.0
                
                # Allow 0 for speed display
                if f < 0:
                    f = 0.0
                if f > 99.9:
                    f = 99.9
                    
                # Format: "03.9" → "039"
                s = f"{f:04.1f}".replace(".", "")
                
                # Fill display: byte[1-4]
                for i in range(4):
                    if i < len(s):
                        buf[1 + i] = int(s[i]) & 0x0F
                    else:
                        buf[1 + i] = 0xFF
                
                # Blank leading zero for values < 10.0
                if f < 10.0:
                    buf[1] = 0xFF  # Blank/space instead of 0
                
                # Decimal point after 2nd digit
                buf[2] |= DECIMAL_MARK
                
            except Exception as e:
                print(f"DME encode error for value={value}: {e}")
                return buf

    except Exception as e:
        print(f"encode_display error for mode={mode} value={value}: {e}")
        traceback.print_exc()

    return buf

# -----------------------------
# Build report with DME handling and XPDR blanks
# -----------------------------
def build_report(tl, tr, bl, br, top_mode="COM1", bottom_mode="COM1"):
    report = bytearray(22)

    # TOP ROW - use simple encode_display for all modes including DME
    report[0:5] = encode_display(tl, top_mode)
    report[5:10] = encode_display(tr, top_mode) if top_mode != "XPDR" else bytearray([0xFF] * 5)

    # BOTTOM ROW - use simple encode_display for all modes including DME
    report[10:15] = encode_display(bl, bottom_mode)
    report[15:20] = encode_display(br, bottom_mode) if bottom_mode != "XPDR" else bytearray([0xFF] * 5)

    return report

def set_radio_displays(tl, tr, bl, br, top_mode="COM1", bottom_mode="COM1"):
    report = build_report(tl, tr, bl, br, top_mode, bottom_mode)
    try:
        dev.send_feature_report([0x00] + list(report))
    except Exception as e:
        print("Display write failed:", e)

# -----------------------------
# Bridge receiver thread
# -----------------------------
def bridge_receiver():
    global latest_radio
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
                except Exception:
                    continue
                if obj.get("type") == "radio":
                    latest_radio = obj
        except BlockingIOError:
            time.sleep(0.01)
        except Exception as e:
            print("Bridge recv error:", e)
            time.sleep(0.1)

threading.Thread(target=bridge_receiver, daemon=True).start()

# -----------------------------
# Knob stabilization, accumulation, cooldown
# -----------------------------
def stable_knob(which, part, direction):
    key = f"{which}_{part}"
    now = time.time() * 1000
    last_d = knob_last_dir.get(key)
    last_t = knob_last_time.get(key, 0)
    if last_d and last_d != direction and (now - last_t) < 10:
        return last_d
    knob_last_dir[key] = direction
    knob_last_time[key] = now
    return direction

def knob_allowed(which, part, cooldown_ms=12):
    key = f"{which}_{part}"
    now = time.time() * 1000
    last = knob_cooldown.get(key, 0)
    if now - last < cooldown_ms:
        return False
    knob_cooldown[key] = now
    return True

def send_cmd_limited(cmd, cooldown_ms=25):
    now = time.time() * 1000
    t = last_event_time.get(cmd, 0)
    if now - t < cooldown_ms:
        return
    last_event_time[cmd] = now
    send_cmd(cmd)

def send_knob_accumulated(cmd):
    """
    Accumulate identical knob events within a short window and flush as a single command with data=count.
    """
    now = time.time() * 1000
    acc = knob_accumulator.get(cmd, 0)
    last = knob_last_accum_time.get(cmd, 0)
    # If last event older than 40ms, flush previous
    if now - last > 40 and acc > 0:
        # flush
        send_cmd(cmd, data=acc)
        knob_accumulator[cmd] = 0
        acc = 0
    # accumulate this event
    acc = acc + 1
    knob_accumulator[cmd] = acc
    knob_last_accum_time[cmd] = now
    # schedule a flush slightly later if not already scheduled
    def flush_later(c=cmd):
        time.sleep(0.05)
        a = knob_accumulator.get(c, 0)
        if a > 0:
            send_cmd(c, data=a)
            knob_accumulator[c] = 0
    # start a background flush thread
    threading.Thread(target=flush_later, daemon=True).start()

# -----------------------------
# XPDR helpers
# -----------------------------
idx_to_step = [1000, 100, 10, 1]

def xpdr_cycle_digit(is_upper=True):
    global xpdr_selected_digit_upper, xpdr_selected_digit_lower
    if is_upper:
        xpdr_selected_digit_upper = (xpdr_selected_digit_upper + 1) % 4
        print(f"XPDR upper selected digit -> {xpdr_selected_digit_upper} (step {idx_to_step[xpdr_selected_digit_upper]})")
    else:
        xpdr_selected_digit_lower = (xpdr_selected_digit_lower + 1) % 4
        print(f"XPDR lower selected digit -> {xpdr_selected_digit_lower} (step {idx_to_step[xpdr_selected_digit_lower]})")

def xpdr_cmd_for_selected(is_upper, direction):
    idx = xpdr_selected_digit_upper if is_upper else xpdr_selected_digit_lower
    step = idx_to_step[idx]
    # direction is "INC" or "DEC"
    return f"XPDR_{step}_{direction}"

# -----------------------------
# Debounced swap for non-XPDR modes
# -----------------------------
def send_swap_limited(cmd, cooldown_ms=250):
    now = time.time() * 1000
    t = last_swap_time.get(cmd, 0)
    if now - t < cooldown_ms:
        # print(f"Swap debounced: {cmd} (too soon)")
        return
    last_swap_time[cmd] = now
    # print(f"SWAP BUTTON -> {cmd}")
    send_cmd(cmd)

# -----------------------------
# Main HID Loop (input only)
# -----------------------------
print("Radio Panel decoder running...")

# knob map: b2 -> (which, part, direction)
knob_map = {
    0x01: ("upper", "FRACT", "INC"),
    0x02: ("upper", "FRACT", "DEC"),
    0x04: ("upper", "WHOLE", "INC"),
    0x08: ("upper", "WHOLE", "DEC"),
    0x10: ("lower", "FRACT", "INC"),
    0x20: ("lower", "FRACT", "DEC"),
    0x40: ("lower", "WHOLE", "INC"),
    0x80: ("lower", "WHOLE", "DEC"),
}



def display_refresher():
    global upper_mode, lower_mode
    print("display_refresher started")
    while running:
        try:
            if not latest_radio:
                # helpful debug: show that we have no radio data yet
                # (comment out after verifying)
                # print("display_refresher: no latest_radio")
                time.sleep(0.05)
                continue

            r = latest_radio
            top_mode = upper_mode or "COM1"
            bottom_mode = lower_mode or "COM1"

            # TOP
            if top_mode == "COM1":
                tl = r.get("com1", {}).get("active", 0)
                tr = r.get("com1", {}).get("standby", 0)
            elif top_mode == "COM2":
                tl = r.get("com2", {}).get("active", 0)
                tr = r.get("com2", {}).get("standby", 0)
            elif top_mode == "NAV1":
                tl = r.get("nav1", {}).get("active", 0)
                tr = r.get("nav1", {}).get("standby", 0)
            elif top_mode == "NAV2":
                tl = r.get("nav2", {}).get("active", 0)
                tr = r.get("nav2", {}).get("standby", 0)
            elif top_mode == "ADF":
                tl = r.get("adf", {}).get("active", 0)
                tr = r.get("adf", {}).get("standby", 0)
            elif top_mode == "XPDR":
                tl = r.get("xpdr", 0)
                tr = 0
            elif top_mode == "DME":
                tl = r.get("dme", {}).get("dist", 0)
                tr = r.get("dme", {}).get("speed", 0)  # Show speed instead of blank
            else:
                tl, tr = 0, 0

            # BOTTOM
            if bottom_mode == "COM1":
                bl = r.get("com1", {}).get("active", 0)
                br = r.get("com1", {}).get("standby", 0)
            elif bottom_mode == "COM2":
                bl = r.get("com2", {}).get("active", 0)
                br = r.get("com2", {}).get("standby", 0)
            elif bottom_mode == "NAV1":
                bl = r.get("nav1", {}).get("active", 0)
                br = r.get("nav1", {}).get("standby", 0)
            elif bottom_mode == "NAV2":
                bl = r.get("nav2", {}).get("active", 0)
                br = r.get("nav2", {}).get("standby", 0)
            elif bottom_mode == "ADF":
                bl = r.get("adf", {}).get("active", 0)
                br = r.get("adf", {}).get("standby", 0)
            elif bottom_mode == "XPDR":
                bl = r.get("xpdr", 0)
                br = 0
            elif bottom_mode == "DME":
                bl = r.get("dme", {}).get("dist", 0)
                br = r.get("dme", {}).get("speed", 0)  # Show speed instead of blank
            else:
                bl, br = 0, 0

            # --- prepare top row bytes with XPDR blink support ---
            if top_mode == "XPDR":
                top_left_bytes = encode_display(tl, "XPDR")
                top_right_bytes = bytearray([0xFF] * 5)
                if not xpdr_blink_state:
                    sel = xpdr_selected_digit_upper
                    byte_idx = 1 + sel
                    top_left_bytes[byte_idx] = BLANK_BYTE
            else:
                top_left_bytes = encode_display(tl, top_mode)
                top_right_bytes = encode_display(tr, top_mode) if top_mode != "XPDR" else bytearray([0xFF] * 5)

            # --- prepare bottom row bytes with XPDR blink support ---
            if bottom_mode == "XPDR":
                bottom_left_bytes = encode_display(bl, "XPDR")
                bottom_right_bytes = bytearray([0xFF] * 5)
                if not xpdr_blink_state:
                    sel = xpdr_selected_digit_lower
                    byte_idx = 1 + sel
                    bottom_left_bytes[byte_idx] = BLANK_BYTE
            else:
                bottom_left_bytes = encode_display(bl, bottom_mode)
                bottom_right_bytes = encode_display(br, bottom_mode) if bottom_mode != "XPDR" else bytearray([0xFF] * 5)

            # --- assemble and send report ---
            report = bytearray(22)
            report[0:5]   = top_left_bytes
            report[5:10]  = top_right_bytes
            report[10:15] = bottom_left_bytes
            report[15:20] = bottom_right_bytes

            try:
                dev.send_feature_report([0x00] + list(report))
            except Exception as e:
                print("Display write failed:", e)

        except Exception as e:
            print("Display refresher error:", e)
            traceback.print_exc()

        time.sleep(0.05)

print("starting display_refresher thread now")
threading.Thread(target=display_refresher, daemon=True).start()

def xpdr_blinker():
    global xpdr_blink_state
    print("xpdr_blinker started")
    while running:
        xpdr_blink_state = not xpdr_blink_state
        time.sleep(xpdr_blink_interval)

threading.Thread(target=xpdr_blinker, daemon=True).start()


# HID input loop
while running:
    try:
        data = dev.read(5)
        if not data:
            time.sleep(0.01)
            continue
        if len(data) < 3:
            continue

        b0, b1, b2 = data[:3]

        # Update modes (strip button bits)
        new_upper = MODES_UPPER.get(b0 & 0x7F, None)
        new_lower = MODES_LOWER.get(b1 & 0x3F, None)
        if new_upper:
            upper_mode = new_upper
        if new_lower:
            lower_mode = new_lower

        # debug: show raw bytes and resolved modes when a swap bit is present
        if (b1 & 0x40) or (b1 & 0x80):
            print(f"SWAP PRESS raw b0=0x{b0:02X} b1=0x{b1:02X} -> upper_mode={upper_mode} lower_mode={lower_mode}")


        # -----------------------------
        # Swap buttons handling
        # -----------------------------
        # Upper swap (bit 6 of b1)
        if b1 & 0x40:
            if upper_mode == "XPDR":
                xpdr_cycle_digit(is_upper=True)
            else:
                send_swap_limited(f"{upper_mode}_STBY_SWAP")
        # Lower swap (bit 7 of b1)
        if b1 & 0x80:
            if lower_mode == "XPDR":
                xpdr_cycle_digit(is_upper=False)
            else:
                send_swap_limited(f"{lower_mode}_STBY_SWAP")

        # -----------------------------
        # Knobs (b2)
        # -----------------------------
        if b2 != 0:
            entry = knob_map.get(b2)
            if entry:
                which, part, direction = entry
                mode = upper_mode if which == "upper" else lower_mode

                # Stabilize direction to avoid cross-talk
                direction = stable_knob(which, part, direction)

                # Per-encoder cooldown to avoid double events
                if not knob_allowed(which, part):
                    continue

                # XPDR special handling: map to selected digit
                if mode == "XPDR":
                    # Map direction to XPDR step command
                    cmd = xpdr_cmd_for_selected(is_upper=(which == "upper"), direction=direction)
                    # Use accumulation for fast spins
                    send_knob_accumulated(cmd)
                else:
                    # Normal behavior for COM/NAV/ADF
                    cmd = f"{mode}_{part}_{direction}"
                    send_knob_accumulated(cmd)

    except Exception as e:
        print("Read error:", e)
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

print("Radio Panel closed.")
