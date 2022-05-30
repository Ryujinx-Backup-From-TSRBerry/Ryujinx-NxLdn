#!/bin/bash

adapter=$(nmcli device status | grep wifi | awk '{print $1}')

case $1 in
    activate)
        nmcli device set $adapter managed false
        nmcli radio wifi off
        sudo rfkill unblock wifi
        sudo iw dev $adapter set monitor none
        sudo ip link set $adapter up
        ;;
    *)
        echo "Unknwon action."
        echo "Currently known actions:"
        echo "  - activate"
        ;;
esac
