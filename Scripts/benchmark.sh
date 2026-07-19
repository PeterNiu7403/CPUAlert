#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DERIVED="$ROOT/build/DerivedData"
PRODUCTS="$DERIVED/Build/Products/Release"
APP="$PRODUCTS/CPUAlert.app"
CPU_STRESS="$PRODUCTS/CPUStress"
GPU_STRESS="$PRODUCTS/GPUStress"
SAMPLER="$ROOT/build/process_sampler"
MODE="${1:-green}"
DURATION_SECONDS="${DURATION_SECONDS:-300}"
RESULTS="$ROOT/build/benchmarks"
DEVELOPER_ROOT="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"

case "$MODE" in
    green|panel-open|elevated-cpu|elevated-gpu|expanded-thread|all) ;;
    *)
        echo "usage: $0 {green|panel-open|elevated-cpu|elevated-gpu|expanded-thread|all}" >&2
        exit 64
        ;;
esac

if ! [[ "$DURATION_SECONDS" =~ ^[0-9]+$ ]] || ((DURATION_SECONDS < 10 || DURATION_SECONDS > 300)); then
    echo "DURATION_SECONDS must be an integer in 10...300" >&2
    exit 64
fi

build_products() {
    DEVELOPER_DIR="$DEVELOPER_ROOT" xcodebuild \
        -project "$ROOT/CPUAlert.xcodeproj" \
        -scheme CPUAlert \
        -configuration Release \
        -derivedDataPath "$DERIVED" \
        build
    DEVELOPER_DIR="$DEVELOPER_ROOT" xcodebuild \
        -project "$ROOT/CPUAlert.xcodeproj" \
        -scheme CPUStress \
        -configuration Release \
        -derivedDataPath "$DERIVED" \
        CODE_SIGNING_ALLOWED=NO \
        build
    DEVELOPER_DIR="$DEVELOPER_ROOT" xcodebuild \
        -project "$ROOT/CPUAlert.xcodeproj" \
        -scheme GPUStress \
        -configuration Release \
        -derivedDataPath "$DERIVED" \
        CODE_SIGNING_ALLOWED=NO \
        build
    DEVELOPER_DIR="$DEVELOPER_ROOT" xcrun clang \
        -O2 -Wall -Wextra -Werror \
        "$ROOT/Scripts/process_sampler.c" \
        -o "$SAMPLER"
}

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    build_products
fi

if [[ "$MODE" == "all" ]]; then
    for benchmark_mode in green panel-open elevated-cpu elevated-gpu expanded-thread; do
        echo "== CPUAlert benchmark: $benchmark_mode =="
        SKIP_BUILD=1 DURATION_SECONDS="$DURATION_SECONDS" "$0" "$benchmark_mode"
    done
    exit 0
fi

mkdir -p "$RESULTS"
TRACE="$RESULTS/CPUAlert-$MODE.trace"
RESULT="$RESULTS/CPUAlert-$MODE.json"
LOG="$RESULTS/CPUAlert-$MODE-xctrace.log"
APP_LOG="$RESULTS/CPUAlert-$MODE-app.log"
TRACE_TOC="$RESULTS/.CPUAlert-$MODE-toc.xml"
LOAD_LOG="$RESULTS/CPUAlert-$MODE-load.log"
LOAD_MARKER="$RESULTS/.CPUAlert-$MODE-load-active"
TRACE_PID=""
LOAD_PID=""
APP_PID=""

cleanup() {
    rm -f "$LOAD_MARKER"
    rm -f "$TRACE_TOC"
    if [[ -n "$LOAD_PID" ]] && kill -0 "$LOAD_PID" 2>/dev/null; then
        load_children="$(pgrep -P "$LOAD_PID" || true)"
        if [[ -n "$load_children" ]]; then
            kill $load_children 2>/dev/null || true
        fi
        kill "$LOAD_PID" 2>/dev/null || true
        wait "$LOAD_PID" 2>/dev/null || true
    fi
    if [[ -n "$TRACE_PID" ]] && kill -0 "$TRACE_PID" 2>/dev/null; then
        kill -INT "$TRACE_PID" 2>/dev/null || true
        wait "$TRACE_PID" 2>/dev/null || true
    fi
    if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
        kill -CONT "$APP_PID" 2>/dev/null || true
        kill "$APP_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

existing_pids="$(pgrep -x CPUAlert || true)"
if [[ -n "$existing_pids" ]]; then
    kill -CONT $existing_pids 2>/dev/null || true
    kill $existing_pids
    sleep 1
    remaining_pids="$(pgrep -x CPUAlert || true)"
    if [[ -n "$remaining_pids" ]]; then
        kill -KILL $remaining_pids 2>/dev/null || true
    fi
fi

launch_arguments=()
case "$MODE" in
    green)
        launch_arguments=(--benchmark-green)
        ;;
    panel-open)
        launch_arguments=(--benchmark-panel-open)
        ;;
    elevated-cpu)
        logical_cpus="$(sysctl -n hw.activecpu)"
        stress_workers=$((logical_cpus * 3 / 4))
        ((stress_workers > 0)) || stress_workers=1
        "$CPU_STRESS" \
            --workers "$stress_workers" \
            --duty-percent 100 \
            --seconds "$DURATION_SECONDS" \
            >"$LOAD_LOG" 2>&1 &
        LOAD_PID=$!
        launch_arguments=(--benchmark-elevated-cpu)
        ;;
    elevated-gpu)
        touch "$LOAD_MARKER"
        (
            while [[ -e "$LOAD_MARKER" ]]; do
                "$GPU_STRESS" --seconds 60 >>"$LOAD_LOG" 2>&1 || exit $?
            done
        ) &
        LOAD_PID=$!
        launch_arguments=(--benchmark-elevated-gpu)
        ;;
    expanded-thread)
        "$CPU_STRESS" \
            --workers 1 \
            --duty-percent 50 \
            --seconds "$DURATION_SECONDS" \
            >"$LOAD_LOG" 2>&1 &
        LOAD_PID=$!
        launch_arguments=(--benchmark-expanded-thread --target-pid "$LOAD_PID")
        ;;
esac

if [[ -e "$TRACE" ]]; then
    rm -r "$TRACE"
fi
/usr/bin/env -i \
    PATH=/usr/bin:/bin:/usr/sbin:/sbin \
    LC_ALL=C \
    TMPDIR=/tmp/ \
    "$APP/Contents/MacOS/CPUAlert" "${launch_arguments[@]}" \
    >"$APP_LOG" 2>&1 &
APP_PID=$!
sleep 1
if ! kill -0 "$APP_PID" 2>/dev/null; then
    echo "CPUAlert did not launch; see $APP_LOG" >&2
    exit 1
fi

trace_limit=$((DURATION_SECONDS + 3))
DEVELOPER_DIR="$DEVELOPER_ROOT" xcrun xctrace record \
    --template 'Time Profiler' \
    --time-limit "${trace_limit}s" \
    --output "$TRACE" \
    --no-prompt \
    --attach "$APP_PID" \
    >"$LOG" 2>&1 &
TRACE_PID=$!
sleep 1
if ! kill -0 "$TRACE_PID" 2>/dev/null; then
    echo "xctrace did not attach; see $LOG" >&2
    exit 1
fi

"$SAMPLER" \
    --pid "$APP_PID" \
    --seconds "$DURATION_SECONDS" \
    --interval-ms 1000 \
    >"$RESULT"

set +e
wait "$TRACE_PID"
trace_status=$?
set -e
TRACE_PID=""
if ((trace_status != 0)) && ! grep -q "Output file saved" "$LOG"; then
    echo "xctrace failed with status $trace_status; see $LOG" >&2
    exit "$trace_status"
fi
DEVELOPER_DIR="$DEVELOPER_ROOT" xcrun xctrace export \
    --input "$TRACE" \
    --toc \
    >"$TRACE_TOC"
if grep -Eiq 'key="[^"]*(token|password|secret|credential|private.key)' "$TRACE_TOC"; then
    echo "Trace captured a sensitive environment-variable key; deleting $TRACE" >&2
    rm -r "$TRACE"
    exit 1
fi
rm -f "$TRACE_TOC"
if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
    kill -CONT "$APP_PID" 2>/dev/null || true
    kill "$APP_PID" 2>/dev/null || true
fi
APP_PID=""
rm -f "$LOAD_MARKER"
if [[ -n "$LOAD_PID" ]] && kill -0 "$LOAD_PID" 2>/dev/null; then
    load_children="$(pgrep -P "$LOAD_PID" || true)"
    if [[ -n "$load_children" ]]; then
        kill $load_children 2>/dev/null || true
    fi
    kill "$LOAD_PID" 2>/dev/null || true
    wait "$LOAD_PID" 2>/dev/null || true
fi
LOAD_PID=""

echo "Result: $RESULT"
echo "Trace:  $TRACE"
jq . "$RESULT"

if [[ "$MODE" == "green" ]]; then
    average_cpu="$(jq -r .average_cpu_percent "$RESULT")"
    average_resident="$(jq -r .average_resident_mb "$RESULT")"
    wakeups="$(jq -r .wakeups_per_second "$RESULT")"
    if ! awk -v cpu="$average_cpu" -v resident="$average_resident" -v wakeups="$wakeups" \
        'BEGIN { exit !(cpu <= 0.3 && resident <= 40.0 && wakeups <= 1.0) }'; then
        echo "Closed-green performance gate failed" >&2
        exit 3
    fi
    echo "Closed-green performance gate passed"
fi
