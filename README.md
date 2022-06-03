# Ryujinx-NxLdn

very early WIP

I created this repo to share my progress in an easy way.

Since I'm on Linux I will make everything work here first and check if Windows requires more special handling.

## Credits

- [kinnay](https://github.com/kinnay) did bascially all the groundwork for this project with their [LDN](https://github.com/kinnay/LDN) project.

## New dependencies

- [Sharppcap](https://github.com/dotpcap/sharppcap)
- Windows only: [Npcap](https://github.com/nmap/npcap)
  - This library will be used to support raw packet capture as well as packet injection. Check these sources out to get more information about how this library will be used for this project:
    - [Raw packet capture](https://npcap.com/guide/npcap-devguide.html#npcap-feature-dot11)
    - [Npcap Features](https://npcap.com/guide/index.html#npcap-features)

## What kind of wifi adapter is needed?

The wifi adapter needs to support monitor mode (it might also work with just promiscuous mode being available, but I haven't tested that yet).
As long as monitor mode is available, packet injection should also work just fine.

### Windows: How to figure out if my adapter supports monitor mode

- If you already installed Npcap (maybe because you are using Wireshark) you can open a Powershell window at `C:\Windows\System32\Npcap` and execute the following commands:

  ```ps1
  # Copy either the name or the GUID of the interface you want to use
  netsh wlan show interfaces
  # If this commands outputs a list that contains monitor then your wifi adapter is supported
  .\WlanHelper.exe <wifi interface> modes
  ```

- Without installing any tools:

  ```ps1
  # This will output a long list and which contains two imortant items: "Promiscuous Mode" and "Monitor Mode"
  netsh wlan show wirelesscapabilities
  ```

### Linux: How to figure out if my adapter supports monitor mode

```sh
# This will output a long list.
# Look for the following item "Supported interface modes:" and see if "monitor" is one of the supported modes.
iw phy
```
