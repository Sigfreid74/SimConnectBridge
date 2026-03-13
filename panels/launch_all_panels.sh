#!/bin/bash
# Launch all Logitech Flight Panels
# SimConnectBridge must be running on TCP port 5555

set -e

echo "╔════════════════════════════════════════════╗"
echo "║  Logitech Flight Panels - Linux Launcher  ║"
echo "╚════════════════════════════════════════════╝"
echo ""

# Check if bridge is running
if ! command -v nc &> /dev/null; then
    echo "⚠️  WARNING: 'nc' (netcat) not found, skipping bridge check"
else
    if ! nc -z localhost 5555 2>/dev/null; then
        echo "❌ ERROR: SimConnectBridge not running on port 5555"
        echo ""
        echo "Please start the bridge first:"
        echo "  cd bridge"
        echo "  WINEPREFIX=~/.steam/steam/steamapps/compatdata/1250410/pfx \\"
        echo "  wine bin/Release/net8.0/SimConnectBridge.exe"
        echo ""
        exit 1
    fi
    echo "✅ SimConnectBridge detected on port 5555"
fi

# Check for Python
if ! command -v python3 &> /dev/null; then
    echo "❌ ERROR: python3 not found"
    exit 1
fi

# Check for hidapi
if ! python3 -c "import hid" 2>/dev/null; then
    echo "❌ ERROR: hidapi not installed"
    echo "Install with: pip install hidapi"
    exit 1
fi

echo "✅ Python and hidapi found"
echo ""

# Array to track PIDs
PIDS=()

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "Stopping all panels..."
    for pid in "${PIDS[@]}"; do
        kill "$pid" 2>/dev/null || true
    done
    echo "All panels stopped."
    exit 0
}

trap cleanup SIGINT SIGTERM

# Start Switch Panel
if [ -f "switchpanel_bridge.py" ]; then
    echo "🔧 Starting Switch Panel..."
    python3 switchpanel_bridge.py &
    PIDS+=($!)
    echo "   PID: $!"
else
    echo "⚠️  switchpanel_bridge.py not found, skipping"
fi

# Start Radio Panel
if [ -f "radiopanel_bridge.py" ]; then
    echo "📻 Starting Radio Panel..."
    python3 radiopanel_bridge.py &
    PIDS+=($!)
    echo "   PID: $!"
else
    echo "⚠️  radiopanel_bridge.py not found, skipping"
fi

# Start Multi Panel
if [ -f "multipanel_bridge.py" ]; then
    echo "✈️  Starting Multi Panel..."
    python3 multipanel_bridge.py &
    PIDS+=($!)
    echo "   PID: $!"
else
    echo "⚠️  multipanel_bridge.py not found, skipping"
fi

echo ""
if [ ${#PIDS[@]} -eq 0 ]; then
    echo "❌ No panel scripts found!"
    exit 1
fi

echo "✅ ${#PIDS[@]} panel(s) started successfully!"
echo ""
echo "Press Ctrl+C to stop all panels"
echo ""

# Wait for Ctrl+C
wait
