#!/bin/sh

SCRIPT_DIR=$(dirname $(realpath $0))
RYUJINX_BIN="Ryujinx"

if [ -f "$SCRIPT_DIR/Ryujinx.Ava" ]; then
    RYUJINX_BIN="Ryujinx.Ava"
fi

if [ -f "$SCRIPT_DIR/Ryujinx.Headless.SDL2" ]; then
    RYUJINX_BIN="Ryujinx.Headless.SDL2"
fi

sudo setcap cap_net_admin,cap_net_raw+ep "$SCRIPT_DIR/$RYUJINX_BIN"

env DOTNET_EnableAlternateStackCheck=1 "$SCRIPT_DIR/$RYUJINX_BIN" "$@"
