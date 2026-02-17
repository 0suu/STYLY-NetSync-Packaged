# STYLY NetSync Server Feature Compatibility Matrix (Android AAR)

Last verified: 2026-02-16

Scope:
- Baseline server: `STYLY-NetSync-Server/src/styly_netsync`
- Android target: `STYLY-NetSync-Android/netsync-aar` via Unity bridge
- Unity caller: `STYLY-NetSync-Unity/Packages/com.styly.styly-netsync/Runtime/NetSyncAndroidServiceController.cs`

## Verdict

`STYLY-netsync-android` is **not** feature-complete relative to current `styly-netsync-server`,
but protocol v3 transform serialization is now aligned.

Operational note:
- Upstream follow is managed via `STYLY-NetSync-Android/scripts/sync_styly_netsync_from_server.sh`
  and patch series under `STYLY-NetSync-Android/python-overrides/patches/styly_netsync/`.
- `app/src/main/python/styly_netsync` and `netsync-aar/src/main/python/styly_netsync` are generated targets.

## Matrix

| Feature (from `styly-netsync-server`) | Server status | Android AAR status | Evidence | Notes |
|---|---|---|---|---|
| Transform protocol v3 as current protocol | Supported | **Supported** | Server: `STYLY-NetSync-Server/src/styly_netsync/binary_serializer.py:9` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/binary_serializer.py:9`, `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:663` / Unity: `STYLY-NetSync-Unity/Packages/com.styly.styly-netsync/Runtime/Internal Scripts/BinarySerializer.cs:10` | Android serializer now uses `PROTOCOL_VERSION = 3` and pose message names (`MSG_CLIENT_POSE`, `MSG_ROOM_POSE`). |
| Legacy v2 and JSON fallback removal | Supported (removed) | **Behavior differs** | Server: `STYLY-NetSync-Server/README.md:47`, `STYLY-NetSync-Server/src/styly_netsync/server.py:1038` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:658`, `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:693` | Android keeps JSON fallback path for backward compatibility. |
| TOML config system (`default.toml`, `--config`) | Supported | **Not supported** | Server: `STYLY-NetSync-Server/src/styly_netsync/config.py:1`, `STYLY-NetSync-Server/src/styly_netsync/default.toml:1`, `STYLY-NetSync-Server/src/styly_netsync/server.py:2107` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:1540` | Android parser has no `--config` path and no bundled `config.py`/`default.toml`. |
| Configurable timing/rate/limits from config | Supported | **Partially supported (fixed constants)** | Server: `STYLY-NetSync-Server/src/styly_netsync/server.py:194`, `STYLY-NetSync-Server/src/styly_netsync/server.py:335` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:138`, `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:245` | Android hardcodes core values (broadcast interval, limits, etc.). |
| Client timeout cleanup behavior | Supported (config-driven) | **Behavior differs** | Server default: `STYLY-NetSync-Server/src/styly_netsync/default.toml:39`, applied at `STYLY-NetSync-Server/src/styly_netsync/server.py:203` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:145` | Android sets `CLIENT_TIMEOUT = None` by default. |
| Empty-room expiry/removal | Supported (delayed expiry) | **Behavior differs** | Server: `STYLY-NetSync-Server/src/styly_netsync/server.py:205`, `STYLY-NetSync-Server/src/styly_netsync/server.py:298`, `STYLY-NetSync-Server/src/styly_netsync/server.py:1879` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:1293`, `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:1331` | Android cleanup path has no delayed empty-room grace timer; default timeout is disabled, so this path does not run unless timeout is changed. |
| Advanced control-queue/backpressure path (ROUTER control queue, token budget, monitor) | Supported | **Not supported (legacy/simpler path)** | Server: `STYLY-NetSync-Server/src/styly_netsync/server.py:163`, `STYLY-NetSync-Server/src/styly_netsync/server.py:251`, `STYLY-NetSync-Server/src/styly_netsync/server.py:1107` / Android: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:182` | Android server uses a simpler single publish queue path and lacks the newer ROUTER control-queue/budget flow. |
| REST bridge for client variables (`/v1/rooms/{roomId}/devices/{deviceId}/client-variables`) | Supported | **Conditionally supported but disabled by default in Android bootstrap** | Server doc: `STYLY-NetSync-Server/README.md:113`, `STYLY-NetSync-Server/README.md:117` / Android server env gate: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:527` / Android bootstrap default: `STYLY-NetSync-Android/netsync-aar/src/main/python/netsync_bootstrap.py:102` | Android bootstrap sets `NETSYNC_ENABLE_REST_BRIDGE=0` unless overridden. |
| REST runtime dependencies packaged for Android | Supported in server environment | **Not bundled by default in AAR build** | Android README optional deps: `STYLY-NetSync-Android/README.md:55`, `STYLY-NetSync-Android/README.md:57` / AAR pip install list: `STYLY-NetSync-Android/netsync-aar/build.gradle.kts:94`, `STYLY-NetSync-Android/netsync-aar/build.gradle.kts:95` | `fastapi/pydantic/uvicorn` are optional and not part of the default wheels install list. |
| Server discovery service | Supported and enabled by default | **Supported (enabled in Unity bootstrap path)** | Server default: `STYLY-NetSync-Server/src/styly_netsync/default.toml:27` / Android bootstrap: `STYLY-NetSync-Android/netsync-aar/src/main/python/netsync_bootstrap.py:70` | Android `NetSyncServer` discovery is enabled in the Unity start path. |
| Unity-facing Android control surface | N/A (server concern) | **Limited exposure** | Android bridge API: `STYLY-NetSync-Android/netsync-aar/src/main/java/dev/styly/netsyncandroid/aar/NetSyncAndroidBridge.kt:9`, `STYLY-NetSync-Android/netsync-aar/src/main/java/dev/styly/netsyncandroid/aar/NetSyncAndroidBridge.kt:21` / Unity caller: `STYLY-NetSync-Unity/Packages/com.styly.styly-netsync/Runtime/NetSyncAndroidServiceController.cs:99`, `STYLY-NetSync-Unity/Packages/com.styly.styly-netsync/Runtime/NetSyncAndroidServiceController.cs:153` | Unity can only call `start(context, host, port)` and `stop(context)`. No API for config/discovery/REST toggles. |
| Host bind control via Unity `host` argument | N/A on server CLI | **Misleading (host value not used for bind)** | Bootstrap log-only usage: `STYLY-NetSync-Android/netsync-aar/src/main/python/netsync_bootstrap.py:76` and constructor call without host `STYLY-NetSync-Android/netsync-aar/src/main/python/netsync_bootstrap.py:67` / actual bind: `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:283`, `STYLY-NetSync-Android/netsync-aar/src/main/python/styly_netsync/server.py:489` | Android server binds `tcp://*` regardless of `host` parameter passed from Unity. |
| Simulator tooling (`styly-netsync-simulator`) in normal server workflow | Supported | **Not exposed in Unity Android AAR flow** | Server usage doc: `STYLY-NetSync-Server/README.md:36`, `STYLY-NetSync-Server/README.md:40` / Unity bridge API (start/stop only): `STYLY-NetSync-Android/README.md:36` | Simulator code exists in Android Python tree, but Unity AAR interface does not expose simulator operations. |

## Practical Conclusion

If your requirement is "all currently supported `styly-netsync-server` features also work in `styly-netsync-android`", the answer is **No** in current repo state.

Minimum parity work items:
1. Add config parity (`config.py`, `default.toml`, `--config` or equivalent runtime config API).
2. Expose runtime switches for discovery/REST bridge from Unity bridge API.
3. Reconcile server behavior differences (timeouts, empty-room expiry, advanced control queue path).
