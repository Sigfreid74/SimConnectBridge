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
PRODUCT_ID = 0x0d67

# -----------------------------
# TCP Bridge
# -----------------------------
BRIDGE_HOST = "127.0.0.1"
BRIDGE_PORT = 5555

# State tracking - CRITICAL to avoid flooding
last_switch_state = {}

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

def send_if_changed(switch_name, new_state, on_cmd, off_cmd=None, data_on=None, data_off=None):
    """Only send command if switch state changed"""
    old_state = last_switch_state.get(switch_name, None)
    if old_state != new_state:
        last_switch_state[switch_name] = new_state
        print(f"{switch_name}: {old_state} -> {new_state}")
        if new_state:
            send_cmd(on_cmd, data=data_on)
        elif off_cmd:
            send_cmd(off_cmd, data=data_off)

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

print("Switch Panel opened.")

running = True
latest_gear = {}

def handle_exit(sig, frame):
    global running
    running = False

signal.signal(signal.SIGINT, handle_exit)
signal.signal(signal.SIGTERM, handle_exit)

# -----------------------------
# LED writer
# -----------------------------
def set_gear_leds(left_pos, center_pos, right_pos):
    mask = 0

    def on(v): return v >= 99.0
    def transit(v): return 1.0 < v < 99.0

    if on(left_pos): mask |= 0x01
    elif transit(left_pos): mask |= 0x08

    if on(center_pos): mask |= 0x02
    elif transit(center_pos): mask |= 0x10

    if on(right_pos): mask |= 0x04
    elif transit(right_pos): mask |= 0x20

    try:
        dev.send_feature_report(bytes([0x00, mask & 0xFF]))
    except Exception as e:
        print("LED write failed:", e)

# -----------------------------
# Bridge receiver
# -----------------------------
def bridge_receiver():
    global latest_gear
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
                except Exception as ex:
                    continue

                if obj.get("type") == "switch" and "gear" in obj:
                    latest_gear = obj["gear"]
                    try:
                        set_gear_leds(
                            float(latest_gear.get("left", 0.0)),
                            float(latest_gear.get("center", 0.0)),
                            float(latest_gear.get("right", 0.0))
                        )
                    except Exception as e:
                        print("Error applying gear LEDs:", e)

        except BlockingIOError:
            time.sleep(0.01)
        except Exception as e:
            print("Bridge recv error:", e)
            time.sleep(0.1)

threading.Thread(target=bridge_receiver, daemon=True).start()

# -----------------------------
# Main HID Loop
# -----------------------------
print("Switch Panel decoder running...")

while running:
    try:
        data = dev.read(5)
        if not data:
            time.sleep(0.01)
            continue

        if len(data) < 3:
            continue

        b0, b1, b2 = data[:3]

        # Gear lever
        gear_up = bool(b2 & 0x04)
        gear_down = bool(b2 & 0x08)
        if gear_up:
            send_if_changed("gear", "UP", "GEAR_UP")
        elif gear_down:
            send_if_changed("gear", "DOWN", "GEAR_DOWN")

        # Lights
        send_if_changed("landing", bool(b1 & 0x10), "LANDING_LIGHTS_ON", "LANDING_LIGHTS_OFF")
        send_if_changed("beacon", bool(b1 & 0x01), "BEACON_LIGHTS_ON", "BEACON_LIGHTS_OFF")
        send_if_changed("nav", bool(b1 & 0x02), "NAV_LIGHTS_ON", "NAV_LIGHTS_OFF")
        send_if_changed("strobes", bool(b1 & 0x04), "STROBES_ON", "STROBES_OFF")
        send_if_changed("taxi", bool(b1 & 0x08), "TAXI_LIGHTS_ON", "TAXI_LIGHTS_OFF")
        send_if_changed("panel", bool(b0 & 0x80), "PANEL_LIGHTS_ON", "PANEL_LIGHTS_OFF")

        # Magneto
        high = (b1 & 0xE0) >> 5
        low = b2 & 0x03
        if high == 1:
            send_if_changed("magneto", "OFF", "MAGNETO_OFF")
        elif high == 2:
            send_if_changed("magneto", "RIGHT", "MAGNETO_RIGHT")
        elif high == 4:
            send_if_changed("magneto", "LEFT", "MAGNETO_LEFT")
        elif low == 0x02:
            send_if_changed("magneto", "START", "MAGNETO_START")
        else:
            send_if_changed("magneto", "BOTH", "MAGNETO_BOTH")

        # Systems
        send_if_changed("battery", bool(b0 & 0x01), "MASTER_BATTERY_ON", "MASTER_BATTERY_OFF")
        send_if_changed("alternator", bool(b0 & 0x02), "ALTERNATOR_ON", "ALTERNATOR_OFF")
        send_if_changed("avionics", bool(b0 & 0x04), "AVIONICS_MASTER_1_ON", "AVIONICS_MASTER_1_OFF")
        
        # FIXED: Fuel pump reversed - invert the logic
        send_if_changed("fuel_pump", not bool(b0 & 0x08), "FUELSYSTEM_PUMP_ON", "FUELSYSTEM_PUMP_OFF")
        
        send_if_changed("anti_ice", bool(b0 & 0x10), "ANTI_ICE_ON", "ANTI_ICE_OFF")
        send_if_changed("pitot", bool(b0 & 0x20), "PITOT_HEAT_ON", "PITOT_HEAT_OFF")

        cowl_state = bool(b0 & 0x40)
        send_if_changed("cowl", cowl_state, "COWLFLAP_OPEN", "COWLFLAP_CLOSE", data_on=0, data_off=16383)

    except Exception as e:
        print("Read error:", e)
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

print("Switch Panel closed.")
