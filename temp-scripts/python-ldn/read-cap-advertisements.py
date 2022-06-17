#!/usr/bin/env python3
from scapy.all import *
import ldn
import json
import base64

cap_data = rdpcap("debug-cap.pcap")
switch_pkts = cap_data.filter(lambda x: len(x) > 1000 and x.layers()[
                              1] == scapy.layers.dot11.Dot11FCS and x.SC == 127)


class LdnEncoder(json.JSONEncoder):
    def default(self, obj):
        if isinstance(obj, ldn.AdvertisementFrame):
            ad_dict = obj.__dict__.copy()
            ad_dict["header"] = ad_dict["header"].__dict__
            ad_dict["info"] = ad_dict["info"].__dict__
            ad_dict["header"]["ssid"] = base64.b64encode(
                ad_dict["header"]["ssid"]).decode()
            ad_dict["nonce"] = base64.b64encode(ad_dict["nonce"]).decode()
            ad_dict["info"]["key"] = base64.b64encode(
                ad_dict["info"]["key"]).decode()
            ad_dict["info"]["application_data"] = base64.b64encode(
                ad_dict["info"]["application_data"]).decode()
            participants = ad_dict["info"]["participants"].copy()
            ad_dict["info"]["participants"] = []
            for participant in participants:
                participant_dict = participant.__dict__
                participant_dict["mac_address"] = ":".join(
                    [hex(x)[2:].zfill(2) for x in participant_dict["mac_address"].fields]).upper()
                ad_dict["info"]["participants"].append(participant_dict)
            return ad_dict
        return super().default(obj)


num = 0
for pkt in switch_pkts:
    print(f"Working on packet: {num}")
    frame = ldn.AdvertisementFrame()
    frame.decode(raw(pkt)[53:-4])
    print("\n------\n")
    print(json.dumps(frame, indent=2, cls=LdnEncoder))
    print("<----\n")

    # info = ldn.NetworkInfo()
    # info.address = pkt.addr2
    # info.channel = 1
    # info.parse(frame)
    # param = ldn.ConnectNetworkParam()
    # param.network = info
    # param.password = "testme"
    # param.name = "testnick"
    # param.app_version = 6
    # print("Trying to connect...")
    # with ldn.connect(param) as network:
    #     print(network)

    # input("Press enter to continue")
    num += 1
