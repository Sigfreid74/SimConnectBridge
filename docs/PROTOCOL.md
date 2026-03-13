# Logitech Flight Panels - HID Protocol Documentation

This document describes the reverse-engineered HID protocol for Logitech Flight Panels.

All panels communicate via USB HID with the following USB IDs:
- **Vendor ID**: 0x06A3 (Logitech/Saitek)

## Multi Panel (PID: 0x0D06)

### Input (Panel → Computer) - 3 bytes

```
Byte 0 (b0): Always 0x00 (report ID)
Byte 1 (b1): Mode selector + Rotary + AP button
Byte 2 (b2): AP mode buttons + Auto throttle
Byte 3 (b3): Flaps + Trim
```

#### Byte 1 Breakdown:
```
Bits 0-4: Mode selector
  0x01 = ALT mode
  0x02 = VS mode
  0x04 = IAS mode
  0x08 = HDG mode
  0x10 = CRS mode

Bits 5-6: Rotary encoder
  0x20 = Clockwise rotation
  0x40 = Counter-clockwise rotation

Bit 7: AP master button
  0x80 = Button pressed
```

#### Byte 2 Breakdown:
```
When b3 == 0:
  If b2 == 0: Autothrottle OFF
  If b2 == 128: Autothrottle ARM
  
  Otherwise (b2 & 0x7F):
    0x01 = HDG button
    0x02 = NAV button
    0x04 = IAS button
    0x08 = ALT button
    0x10 = VS button
    0x20 = APR button
    0x40 = REV button
```

#### Byte 3 Breakdown:
```
Flaps:
  0x01 = Flaps DOWN
  0x02 = Flaps UP

Trim:
  0x04 = Pitch trim DOWN
  0x08 = Pitch trim UP
```

### Output (Computer → Panel) - 13 bytes

```
Byte 0:     Report ID (0x00)
Bytes 1-5:  Upper display (5 digits)
Bytes 6-10: Lower display (5 digits)
Byte 11:    LED byte (AP mode buttons)
Byte 12:    Unused (0x00)
```

#### Display Encoding:
```
Each digit: 0x00-0x09 for '0'-'9'
Blank: 0xFF
Minus sign (for VS): 0xDE
```

#### LED Byte (Byte 11) - **SCRAMBLED MAPPING**:
```
Bit 0 (0x01) = AP master LED
Bit 1 (0x02) = HDG LED
Bit 2 (0x04) = NAV LED
Bit 3 (0x08) = IAS LED
Bit 4 (0x10) = ALT LED
Bit 5 (0x20) = VS LED
Bit 6 (0x40) = APR LED
Bit 7 (0x80) = REV LED

NOTE: BC (backcourse) mode lights BOTH APR and REV in hardware,
but we only set bit 7 when BC is active.
```

---

## Switch Panel (PID: 0x0D67)

### Input (Panel → Computer) - 3 bytes

```
Byte 0 (b0): Switches (Battery, Alternator, Avionics, etc.)
Byte 1 (b1): Lights + Magneto
Byte 2 (b2): Gear + Magneto low bits
```

#### Byte 0 Breakdown:
```
Bit 0 (0x01) = Battery
Bit 1 (0x02) = Alternator
Bit 2 (0x04) = Avionics
Bit 3 (0x08) = Fuel Pump (INVERTED!)
Bit 4 (0x10) = De-Ice
Bit 5 (0x20) = Pitot Heat
Bit 6 (0x40) = Cowl Flaps
Bit 7 (0x80) = Panel Lights
```

**IMPORTANT**: Fuel pump is **INVERTED**:
- 0 (bit clear) = Pump ON
- 1 (bit set) = Pump OFF

#### Byte 1 Breakdown:
```
Bit 0 (0x01) = Beacon
Bit 1 (0x02) = Nav Lights
Bit 2 (0x04) = Strobe
Bit 3 (0x08) = Taxi
Bit 4 (0x10) = Landing

Bits 5-7 (0xE0 >> 5) = Magneto high bits
```

#### Byte 2 Breakdown:
```
Bits 0-1 (0x03) = Magneto low bits

Combined magneto value (high 3 bits + low 2 bits):
  0 = OFF
  1 = Right
  2 = Left
  3 = Both
  4 = Start

Bit 2 (0x04) = Gear UP
Bit 3 (0x08) = Gear DOWN
```

### Output (Computer → Panel) - 1 byte

```
Byte 0: LED byte for gear indicators

Bit 0 (0x01) = Left gear GREEN
Bit 1 (0x02) = Center gear GREEN
Bit 2 (0x04) = Right gear GREEN
Bit 3 (0x08) = Left gear RED
Bit 4 (0x10) = Center gear RED
Bit 5 (0x20) = Right gear RED
Bits 6-7: Unused
```

---

## Radio Panel (PID: 0x0D05)

### Input (Panel → Computer) - 3 bytes

```
Byte 0: Upper selector
Byte 1: Lower selector + Swap bits
Byte 2: Knob rotation
```

#### Upper Selector (Byte 0):
```
0x01 = COM1
0x02 = COM2
0x04 = NAV1
0x08 = NAV2
0x10 = ADF
0x20 = DME
0x40 = XPDR
```

#### Lower Selector (Byte 1):
```
Bits 0-5: Mode
  0x00 = COM1
  0x01 = COM2
  0x02 = NAV1
  0x04 = NAV2
  0x08 = ADF
  0x10 = DME
  0x20 = XPDR

Bit 6 (0x40) = Upper swap button
Bit 7 (0x80) = Lower swap button
```

**IMPORTANT**: Mask with 0x3F before checking mode!

#### Knob Rotation (Byte 2):
```
Bits 0-3: Fine knob (1s, 0.025 MHz)
  0x01 = Decrease
  0x02 = Increase

Bits 4-7: Coarse knob (MHz, 1 kHz)
  0x10 = Decrease
  0x20 = Increase
```

### Output (Computer → Panel) - 13 bytes

```
Byte 0:     Report ID (0x00)
Bytes 1-5:  Upper left display
Bytes 6-10: Upper right display
Byte 11:    Active mode byte
Byte 12:    Unused
```

#### Display Encoding:
```
Digits: 0x00-0x09 for '0'-'9'
Blank: 0xFF

Decimal point: OR the digit byte with 0x80
Example: '1' with decimal = 0x01 | 0x80 = 0x81
```

#### Special Characters:
```
Minus sign (ADF): byte | 0xD0
  Used at position buf[3] for ADF frequencies < 1000 kHz
```

#### Active Mode Byte (Byte 11):
```
Sets which radio is shown on displays:
0x12 = COM1
0x22 = COM2
0x44 = NAV1
0x84 = NAV2
0x10 = ADF
0x20 = DME
0x40 = XPDR
```

#### Special Display Modes:

**XPDR Blinking** (Ident):
Alternate between normal display and all 0xFF (blank) every 500ms

**DME Display**:
- Left display: Distance (nn.n format)
- Right display: Speed (nnn format)
- Leading zero suppressed for distance < 10.0

**ADF Display**:
- Format: nnn.n kHz
- Leading blank for frequencies < 1000 kHz
- Decimal mark at buf[3] |= 0xD0

---

## SimConnect Events

### Multi Panel Commands

**Mode Selection Knobs:**
- ALT mode: `AP_ALT_VAR_INC` / `AP_ALT_VAR_DEC`
- VS mode: `AP_VS_VAR_INC` / `AP_VS_VAR_DEC`
- IAS mode: `AP_SPD_VAR_INC` / `AP_SPD_VAR_DEC`
- HDG mode: `HEADING_BUG_INC` / `HEADING_BUG_DEC`
- CRS mode: `VOR1_OBI_INC` / `VOR1_OBI_DEC`

**AP Buttons (TOGGLE commands):**
- HDG: `AP_HDG_HOLD`
- NAV: `AP_NAV1_HOLD`
- IAS: `FLIGHT_LEVEL_CHANGE` (G1000) or `AP_AIRSPEED_HOLD` (standard)
- ALT: `AP_ALT_HOLD`
- VS: `AP_VS_HOLD`
- APR: `AP_APR_HOLD`
- REV: `AP_BC_HOLD` (G1000) or `AP_BACKCOURSE_HOLD` (standard)

**Other:**
- AP Master: `AP_MASTER`
- Autothrottle: `AUTO_THROTTLE_ARM`
- Flaps: `FLAPS_INCR` / `FLAPS_DECR`
- Trim: `ELEV_TRIM_UP` / `ELEV_TRIM_DN`

### Switch Panel Commands

**Dual Systems:**
- Battery: `BATTERY1_SET` + `BATTERY2_SET` (data = 1 for on, 0 for off)
- Alternator: `TOGGLE_ALTERNATOR1` + `TOGGLE_ALTERNATOR2`

**Magneto:**
- `MAGNETO_OFF`, `MAGNETO_RIGHT`, `MAGNETO_LEFT`, `MAGNETO_BOTH`, `MAGNETO_START`

**Cowl Flaps:**
- Open: `COWLFLAP_OPEN` (data = 16383)
- Close: `COWLFLAP_CLOSE` (data = 0)

**Other switches:** Standard MSFS toggle events (e.g., `TOGGLE_BEACON_LIGHTS`)

### Radio Panel Commands

**Frequency Adjustments:**
- COM1 coarse: `COM_RADIO_WHOLE_INC` / `COM_RADIO_WHOLE_DEC`
- COM1 fine: `COM_RADIO_FRACT_INC` / `COM_RADIO_FRACT_DEC`
- (Similar for COM2, NAV1, NAV2)

**Swap:**
- COM1: `COM_STBY_RADIO_SWAP`
- NAV1: `NAV1_RADIO_SWAP`
- (Similar for COM2, NAV2)

**ADF:**
- `ADF_WHOLE_INC/DEC`, `ADF_FRACT_INC/DEC`

**Transponder:**
- `XPNDR_INC` / `XPNDR_DEC`
- Ident: `XPNDR_IDENT_ON` (automatic 18-second blink)

---

## SimConnect SimVars (Read)

### Autopilot Data
- `AUTOPILOT ALTITUDE LOCK VAR` (feet)
- `AUTOPILOT VERTICAL HOLD VAR` (feet/min)
- `AUTOPILOT AIRSPEED HOLD VAR` (knots)
- `AUTOPILOT HEADING LOCK DIR` (degrees)
- `NAV OBS:1` (degrees) - CRS
- `AUTOPILOT MASTER` (bool)
- `AUTOPILOT HEADING LOCK` (bool)
- `AUTOPILOT NAV1 LOCK` (bool)
- `AUTOPILOT FLIGHT LEVEL CHANGE` (bool) - G1000 FLC
- `AUTOPILOT ALTITUDE LOCK` (bool)
- `AUTOPILOT VERTICAL HOLD` (bool)
- `AUTOPILOT APPROACH HOLD` (bool)
- `AUTOPILOT BACKCOURSE HOLD` (bool)

### Gear State
- `GEAR LEFT POSITION` (percent, 0-100)
- `GEAR CENTER POSITION`
- `GEAR RIGHT POSITION`

### Radio Frequencies
- `COM ACTIVE FREQUENCY:1` (MHz, BCD encoded)
- `COM STANDBY FREQUENCY:1`
- `NAV ACTIVE FREQUENCY:1`
- `NAV STANDBY FREQUENCY:1`
- `ADF ACTIVE FREQUENCY:1` (kHz, BCD encoded)
- `TRANSPONDER CODE:1` (BCD encoded)

### DME
- `NAV DME:1` (nautical miles)
- `GPS GROUND SPEED` (knots)
- Time to station (calculated)

---

## Notes

### Discovered Through Reverse Engineering
All protocols were discovered through:
1. USB packet capture
2. Trial and error with hidapi
3. Observing panel behavior
4. Testing with MSFS2020

### Key Discoveries
- Multi Panel LED bits are **scrambled** (don't match button order)
- Switch Panel fuel pump is **inverted**
- Radio Panel swap buttons need masking (0x3F) before mode check
- VS minus sign is 0xDE, not standard ASCII
- DME requires combining NAV DME distance with GPS ground speed
- Transponder ident creates 18-second automatic blink sequence

### Testing Environment
- Linux (Fedora)
- MSFS2020 via Steam + GE-Proton
- Python 3.x + hidapi
- .NET 8.0 + SimConnect SDK

---

**Last Updated**: 2025-01-XX
**Version**: 1.0
