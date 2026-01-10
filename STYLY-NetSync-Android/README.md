# STYLY NetSync Android (Chaquopy + Foreground Service)

## What is implemented
- Kotlin Android app (`minSdk 26`, `targetSdk 34`, `compileSdk 35`)
- Foreground Service (`dataSync`) with persistent notification
- Start/Stop from app UI and notification action
- Runtime notification permission request (`POST_NOTIFICATIONS`, Android 13+)
- Host/Port input UI (`0.0.0.0:5555` default)
- LAN IP display
- File log tail display (`filesDir/netsync.log`)
- Python bootstrap in `app/src/main/python/netsync_bootstrap.py`
- Existing `styly_netsync` package vendored into `app/src/main/python/styly_netsync`

## Project path
- `STYLY-NetSync-Android`

## Notes
- Bootstrap starts only the existing STYLY NetSync server (`styly_netsync.server.NetSyncServer`).
- Existing server is ZeroMQ-based. If `pyzmq` is unavailable or broken, startup fails fast and the service enters `ERROR`.
- Service acquires `PARTIAL_WAKE_LOCK` and `WifiManager.MulticastLock` while running.

## Build in Android Studio
1. Open `STYLY-NetSync-Android` in Android Studio.
2. Let Gradle sync and install requested SDK/NDK components.
3. Build and run on a device.
4. Press `Start` and confirm persistent notification appears.
5. Press `Stop` and confirm notification disappears.

## Local wheels requirement (offline pip)
- This app installs Python dependencies with:
  - `--no-index --find-links src/main/python/wheels --only-binary=:all:`
- Create this directory before build:
  - `app/src/main/python/wheels/`
- Required wheel:
  - `pyzmq==27.1.0` with tags including `cp313` and `android_24_arm64_v8a`
  - Example pattern: `pyzmq-27.1.0-*-cp313-*-android_24_arm64_v8a.whl`
- Required wheel:
  - `loguru==0.7.2`
  - Example pattern: `loguru-0.7.2-*.whl`
- Build now runs `validatePythonWheels` and fails early if these wheels are missing.

## Dependency policy on Android
- Required at startup:
  - `pyzmq`, `loguru`
- Optional (startup should continue even if missing):
  - `psutil` (IP discovery falls back to socket-based method)
  - `fastapi`, `pydantic`, `uvicorn` (REST bridge only)
  - `msgpack` (future/optional NV features)
- REST bridge is disabled by default on Android (`NETSYNC_ENABLE_REST_BRIDGE=0`).

## Clean reinstall flow
1. `adb uninstall dev.styly.netsyncandroid`
2. Android Studio: Sync project
3. Android Studio: Build and Install
4. Start service and confirm in `filesDir/netsync.log`:
   - `pyzmq=... libzmq=... zmq_file=... backend_file=...`
   - no `[bootstrap] startup failed`

## Log checks
- App UI log tail reads `filesDir/netsync.log`.
- `adb logcat | grep NetSync` for Kotlin-side transitions.
