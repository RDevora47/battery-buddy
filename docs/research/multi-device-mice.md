# Multi-device (host-switching) mice: reference list

_Compiled 2026-09-25._

## How this list was put together

This is a list of about 50 popular wireless mice that can store more than one paired host and switch between them. Logitech calls this "Easy-Switch". Other brands call it "multi-device" or give each channel its own button. The list is meant to guide a Windows feature that switches the mouse's active host from software. On Logitech hardware that feature uses HID++ 2.0 feature `0x1814` CHANGE_HOST.

Sources:

- **Protocol and IDs.** These came from three places. The first is Solaar's per-device feature dumps in `docs/devices/*.txt` (pwr-Solaar/Solaar, master). Each dump lists a device's HID++ features and its transport IDs. The second is the libratbag `data/devices/logitech-*.device` files. The third is the Linux kernel's `drivers/hid/hid-logitech-hidpp.c` Bluetooth device table. Solaar's `lib/logitech_receiver/descriptors.py` only lists older devices. Newer devices report their own names and IDs, so the feature dumps were the more useful source.
- **Popularity.** This is a rough blend of several sources: the Amazon "Computer Mice" best-seller list (as reflected in search results), "best wireless / multi-device mouse 2026" roundups (Tom's Hardware, RTINGS, pctechkits, KeebFinder's multi-device filter), and how often a model comes up on Reddit. **The ranking is approximate.** It is meant for ordering test and support priorities, not as sales data. Logitech models rank slightly higher on purpose.
- **Host counts.** These come from vendor spec pages and datasheets (Logitech, HP, Dell, Razer, Microsoft support). A count marked `?` could not be confirmed from a primary source.

ID conventions:

- `WPID` is the Unifying/Lightspeed wireless PID seen through a receiver.
- `BT` is the Bluetooth (BLE) product ID under vendor 0x046D.
- `USB` is the wired PID.
- Logitech **Bolt** devices report the same B0xx ID over Bolt as over BLE. Solaar shows only `btleid` for them, so the BT ID is also the Bolt ID.
- A blank cell means no reliable source was found. No IDs were guessed.

## Table

| Rank | Model | Brand | # hosts | Connection | Switch protocol | Product ID(s) |
|---:|---|---|---|---|---|---|
| 1 | MX Master 3S (incl. "for Mac") | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (Solaar: v1) | BT/Bolt 0xB034 |
| 2 | M720 Triathlon | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 405E; BT 0xB015 |
| 3 | MX Master 4 | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (Solaar: **v2**, plus 0x1815 v2) | BT/Bolt 0xB042 |
| 4 | MX Anywhere 3S | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (expected; same family as MX Anywhere 3) | BT/Bolt 0xB037 |
| 5 | Pebble Mouse 2 M350s | Logitech | 3 | Bluetooth | HID++ 0x1814 (expected; no Solaar dump) | |
| 6 | Lift Vertical Ergonomic (incl. Left, for Mac) | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (Solaar) | BT/Bolt 0xB031 |
| 7 | Signature M750 / M750 L / Signature Plus M750 | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (expected; no Solaar dump) | |
| 8 | MX Vertical | Logitech | 3 | Bluetooth / Unifying / USB-C | HID++ 0x1814 (Solaar) | WPID 407B; BT 0xB020; USB 0xC08A |
| 9 | MX Ergo S (trackball) | Logitech | 2 | Bluetooth / Bolt | HID++ 0x1814 (expected) | |
| 10 | POP Mouse | Logitech | 3 | Bluetooth | HID++ 0x1814 (expected; no Solaar dump) | |
| 11 | Bluetooth Ergonomic Mouse (now sold by Incase) | Microsoft | 3 | Bluetooth | proprietary (slider/button; "Smart Switch" via Mouse and Keyboard Center) | |
| 12 | Pro Click V2 / Pro Click V2 Vertical | Razer | up to 5 (3 BT + HyperSpeed + wired) | Bluetooth / 2.4GHz dongle | proprietary | |
| 13 | MX Master 3 (incl. "for Mac") | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 4082; BT 0xB023 |
| 14 | MX Anywhere 3 (incl. "for Mac") | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 4090; BT 0xB025 |
| 15 | M590 / M585 Multi-Device (Silent) | Logitech | 2 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 406B; BT 0xB01B |
| 16 | M6 | Keychron | 3 BT + 2.4GHz (+ wired) | Bluetooth / 2.4GHz dongle | proprietary | |
| 17 | EM11 NL vertical | ProtoArc | 3 | Bluetooth / 2.4GHz dongle | proprietary | |
| 18 | MX Master 2S | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 4069; BT 0xB019 |
| 19 | MS5320W Multi-Device (Pro Plus) | Dell | 3 | Bluetooth / 2.4GHz dongle | proprietary | |
| 20 | 710 Rechargeable Silent | HP | 3 | Bluetooth / 2.4GHz dongle | proprietary | |
| 21 | Surface Precision Mouse | Microsoft | 3 | Bluetooth (+ USB wired) | proprietary | |
| 22 | Pro Click Mini | Razer | 4 | Bluetooth / 2.4GHz dongle | proprietary | |
| 23 | MX Ergo (trackball) | Logitech | 2 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 406F; BT 0xB01D |
| 24 | MX Anywhere 2S | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 406A; BT 0xB01A |
| 25 | Basilisk V3 Pro | Razer | 2 (HyperSpeed + BT) | Bluetooth / 2.4GHz dongle | proprietary (mode switch) | |
| 26 | G604 Lightspeed | Logitech | 2 (Lightspeed + BT) | Bluetooth / Lightspeed | HID++ 0x1814 (Solaar) | WPID 4085; BT 0xB024 |
| 27 | 930 Creator | HP | 3 | Bluetooth / 2.4GHz dongle | proprietary | |
| 28 | MS7421W Premier Rechargeable | Dell | 3? | Bluetooth / 2.4GHz dongle | proprietary | |
| 29 | Go Wireless Multi-Device Mouse | Lenovo | 3? | Bluetooth / 2.4GHz dongle | proprietary | |
| 30 | ProArt MD300 | ASUS | 3? | Bluetooth / 2.4GHz dongle | proprietary | |
| 31 | Marshmallow MD100 | ASUS | 3? (2 BT + 2.4GHz) | Bluetooth / 2.4GHz dongle | proprietary | |
| 32 | M5 | Keychron | 3 BT + 2.4GHz? | Bluetooth / 2.4GHz dongle | proprietary | |
| 33 | 635 Multi-Device | HP | 3 (dongle + 2 BT) | Bluetooth / 2.4GHz dongle | proprietary | |
| 34 | 435 Multi-Device | HP | 2 | Bluetooth / 2.4GHz dongle | proprietary | |
| 35 | Expert Mouse Wireless Trackball | Kensington | 3? (2 BT + dongle) | Bluetooth / 2.4GHz dongle | proprietary | |
| 36 | SlimBlade Pro Trackball | Kensington | 3? (BT, 2.4GHz, wired) | Bluetooth / 2.4GHz dongle | proprietary | |
| 37 | M501 trackball | Nulea | 3? | Bluetooth / 2.4GHz dongle | proprietary | |
| 38 | HUGE Plus / DEFT Pro trackball | Elecom | 3? (BT, 2.4GHz, wired) | Bluetooth / 2.4GHz dongle | proprietary | |
| 39 | Pebble M350 (original) | Logitech | 2 (receiver / BT) | Bluetooth / Unifying-class receiver | HID++ 0x1814 (Solaar, v1) | WPID 4080; BT 0xB021 |
| 40 | MX Anywhere 2 | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 404A, 4072, 4063; BT 0xB013, 0xB018, 0xB01F (libratbag) |
| 41 | MX Master (original) | Logitech | 3 | Bluetooth / Unifying | HID++ 0x1814 (Solaar) | WPID 4041, 4060, 4071; BT 0xB012, 0xB017, 0xB01E |
| 42 | Pro Click (original) | Razer | 4? | Bluetooth / 2.4GHz dongle | proprietary | |
| 43 | Touch PBT mouse | Lofree | 3? | Bluetooth / 2.4GHz dongle | proprietary | |
| 44 | Dark Core RGB Pro | Corsair | 2 (Slipstream + BT) | Bluetooth / 2.4GHz dongle | proprietary | |
| 45 | Aerox 3 Wireless | SteelSeries | 2 (2.4GHz + BT) | Bluetooth / 2.4GHz dongle | proprietary | |
| 46 | MX Master 3S for Business | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (expected) | BT/Bolt 0xB035 |
| 47 | MX Master 3 for Business | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (Solaar, v1) | BT/Bolt 0xB028 |
| 48 | MX Anywhere 3 for Business | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (Solaar, v1) | BT/Bolt 0xB02D |
| 49 | MX Anywhere 3S for Business | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (expected) | BT/Bolt 0xB038 (kernel: "MX Anywhere 3SB") |
| 50 | Lift for Business | Logitech | 3 | Bluetooth / Bolt | HID++ 0x1814 (Solaar, v1) | BT/Bolt 0xB033 |

Totals: 50 entries. 25 are Logitech and 25 are other brands.

## Notes

### Confirmed 0x1814 CHANGE_HOST support (Solaar feature dumps)

These mice have a CHANGE HOST `{1814}` entry in Solaar `docs/devices/`:

- MX Master (B012/4041, 4071)
- MX Master 2S (4069/B019)
- MX Master 3 (4082/B023)
- MX Master 3 for Business (B028)
- MX Master 3S (B034)
- MX Master 4 (B042, **version 2**, together with HOSTS_INFO 0x1815 v2)
- MX Anywhere 2 (404A, 4072)
- MX Anywhere 2S (406A/B01A)
- MX Anywhere 3 (4090/B025)
- MX Anywhere 3 for Business (B02D)
- MX Vertical (407B/B020)
- MX Ergo (406F)
- M720 Triathlon (405E/B015)
- M585/M590 (406B)
- Lift (B031)
- Lift for Business (B033)
- Pebble M350 (4080)
- G604 (4085)

Several models are marked **"expected"** in the table: MX Anywhere 3S, MX Master 3S for Business, MX Ergo S, Signature M750, Pebble 2 M350s and POP Mouse. They are sold with Easy-Switch, but no Solaar dump was found for them. Confirm at runtime: look up feature 0x1814 through IRoot (0x0000) rather than hard-coding a list.

### Logitech mice left out (single host, or no CHANGE_HOST in Solaar)

- Signature M650 / M650 L (B02A)
- Signature M550 / M550 L (B02B). Amazon calls it "multi-device", but it has no 0x1814 and a single channel.
- ERGO M575 / M575S (4096/B027)
- M240
- M196
- M337/M535
- M705 Marathon
- M510, M185, M325 and other older Unifying mice
- Most G-series Lightspeed gaming mice (G502 X Plus, G703, G903, PRO X), which have no CHANGE_HOST in their dumps

### Protocol quirks worth knowing

- **Function numbers.** 0x1814 fn 0 (`getHostInfo`) returns the host count and the current host. fn 1 (`setCurrentHost`, host index is 0-based) switches immediately. The device drops off the current host at once, so **expect no reply** (or a timeout) to the set command. Treat that as success.
- **HOSTS_INFO 0x1815.** Where present (MX Master 4, most Bolt-era devices), it lists which slots are paired and gives host names and OS. Use it to avoid switching to an empty slot. A switch to an unpaired slot leaves the mouse unreachable until the user presses the button.
- **One-way switch.** Once the mouse has left this PC, only the other host (or the physical button) can bring it back. A "switch back" needs a companion app on the other host, or Logitech's Flow / Enhanced Easy-Switch in Options+. Logitech added Enhanced Easy-Switch in 2026: pressing an Easy-Switch key on the keyboard moves both keyboard and mouse.
- **Bluetooth report size.** Over Bluetooth LE, HID++ is reachable only through the long (20-byte, report ID 0x11) vendor collection (usage page 0xFF43). Short 7-byte (0x10) reports are generally not supported over BLE. Through a Unifying or Bolt receiver, address the device by its slot index on the receiver's HID++ interface (0xFF00 collection).
- **Bolt IDs.** Bolt-paired devices use the same B0xx ID as over BLE (see Solaar "Transport IDs"). Unifying-era devices have separate WPID and BT IDs.
- **Business variants.** "for Business" / "B2B" variants have their own BT IDs (for example MX Master 3S 0xB034 vs for Business 0xB035). Match on the feature, not the PID.

### Other brands

No public host-switch command was found for any non-Logitech mouse on this list, so they are marked "proprietary". Switching is by a bottom button, slider or mode switch. Some vendor apps offer cursor-edge switching, for example Microsoft Mouse and Keyboard Center "Smart Switch" and Razer Synapse on the Pro Click V2. Their wire protocols are undocumented and would need reverse-engineering. The Apple Magic Mouse, Satechi M1 and similar single-pairing BT mice are left out.

### Source links

- Solaar device dumps: https://github.com/pwr-Solaar/Solaar/tree/master/docs/devices
- Solaar descriptors: https://github.com/pwr-Solaar/Solaar/blob/master/lib/logitech_receiver/descriptors.py
- libratbag device data: https://github.com/libratbag/libratbag/tree/master/data/devices
- Linux hid-logitech-hidpp BT table: https://github.com/torvalds/linux/blob/master/drivers/hid/hid-logitech-hidpp.c
- Logitech Signature M750 (Easy-Switch, 3 devices): https://support.logi.com/hc/en-150/articles/4414498437271-Getting-Started-Signature-M750
- Logitech Pebble Mouse 2 M350s: https://support.logi.com/hc/en-001/articles/16171126528407-Getting-Started-Pebble-Mouse-2-M350s
- Microsoft Bluetooth Ergonomic Mouse: https://support.microsoft.com/en-us/surface/accessories/use-microsoft-bluetooth-ergonomic-mouse
- Razer Pro Click V2 Vertical FAQ: https://mysupport.razer.com/app/answers/detail/a_id/15010
- Dell MS5320W datasheet: https://www.delltechnologies.com/asset/en-us/products/electronics-and-accessories/technical-support/dell_multi_device_wireless_mouse_ms5320w_data_sheet.pdf
- HP 635 datasheet: https://h20195.www2.hp.com/v2/GetPDF.aspx/4aa7-9759enw.pdf
- Tom's Hardware best wireless mouse: https://www.tomshardware.com/best-picks/best-wireless-mouse
- KeebFinder multi-device filter: https://keeb-finder.com/mice/filter/multi-device-pairing
- Amazon best sellers, Computer Mice: https://www.amazon.com/Best-Sellers-Computers-Accessories-Computer-Mice/zgbs/pc/11036491
