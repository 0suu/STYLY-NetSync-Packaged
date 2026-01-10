import os
import sys
import threading
import time
import traceback

_thread = None
_stop = threading.Event()
_started = threading.Event()
_start_error: str | None = None
_server = None
BOOTSTRAP_REV = "2026-02-10b"


def _append_log(log_path: str, message: str) -> None:
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    with open(log_path, "a", encoding="utf-8") as f:
        f.write(message.rstrip("\n") + "\n")


def _check_pyzmq_health() -> str:
    import zmq

    try:
        libzmq_version = zmq.zmq_version()
    except Exception:
        libzmq_version = "unknown"

    try:
        from zmq.backend.cython import _zmq
        backend_file = getattr(_zmq, "__file__", "unknown")
    except Exception as e:
        zmq_file = getattr(zmq, "__file__", "unknown")
        raise RuntimeError(
            "pyzmq backend import failed: "
            f"zmq_file={zmq_file}, error={type(e).__name__}: {e}"
        ) from e

    zmq_file = getattr(zmq, "__file__", "unknown")
    return (
        f"pyzmq={getattr(zmq, '__version__', 'unknown')}, "
        f"libzmq={libzmq_version}, zmq_file={zmq_file}, backend_file={backend_file}"
    )


def _optional_import_status() -> str:
    optional_modules = ("psutil", "msgpack", "fastapi", "pydantic", "uvicorn")
    states: list[str] = []
    for name in optional_modules:
        try:
            __import__(name)
            states.append(f"{name}=ok")
        except Exception as e:
            states.append(f"{name}=missing({type(e).__name__})")
    return ", ".join(states)


def _run_styly_server(host: str, port: int, log_path: str) -> None:
    global _server

    from styly_netsync.logging_utils import configure_logging
    from styly_netsync.server import NetSyncServer

    log_dir = os.path.dirname(log_path)
    configure_logging(log_dir=log_dir, console_level="INFO", console_json=False)

    local_server = NetSyncServer(
        dealer_port=port,
        pub_port=port + 1,
        enable_server_discovery=False,
    )
    _server = local_server

    _append_log(
        log_path,
        f"[bootstrap] Starting STYLY server host={host} dealer={port} pub={port + 1}",
    )
    local_server.start()
    _started.set()

    try:
        while not _stop.is_set():
            time.sleep(0.2)
    finally:
        try:
            local_server.stop()
        except Exception as e:
            _append_log(log_path, f"[bootstrap] server stop error: {type(e).__name__}: {e}")
        if _server is local_server:
            _server = None
        _append_log(log_path, "[bootstrap] STYLY server stopped")


def _run(host: str, port: int, log_path: str) -> None:
    global _start_error

    try:
        _append_log(log_path, f"[bootstrap] start requested host={host} port={port}")
        _append_log(log_path, f"[bootstrap] rev={BOOTSTRAP_REV}")
        _append_log(log_path, f"[bootstrap] python={sys.version}")
        # Android deployment default: disable REST bridge unless explicitly enabled.
        os.environ.setdefault("NETSYNC_ENABLE_REST_BRIDGE", "0")

        zmq_info = _check_pyzmq_health()
        _append_log(log_path, f"[bootstrap] {zmq_info}")
        _append_log(log_path, f"[bootstrap] optional_imports: {_optional_import_status()}")

        _run_styly_server(host, port, log_path)
    except Exception:
        _start_error = traceback.format_exc()
        _append_log(log_path, "[bootstrap] startup failed")
        _append_log(log_path, _start_error)


def start(host: str, port: int, log_path: str):
    global _thread, _start_error

    if _thread and _thread.is_alive():
        _append_log(log_path, "[bootstrap] start ignored: already running")
        return

    _stop.clear()
    _started.clear()
    _start_error = None

    _thread = threading.Thread(target=_run, args=(host, int(port), log_path), daemon=True)
    _thread.start()

    # Wait briefly for either successful startup or immediate failure.
    deadline = time.time() + 5.0
    while time.time() < deadline:
        if _started.is_set():
            return
        if _start_error is not None:
            raise RuntimeError(_start_error)
        if _thread is not None and not _thread.is_alive():
            break
        time.sleep(0.05)

    if _started.is_set():
        return

    if _start_error is not None:
        raise RuntimeError(_start_error)
    raise RuntimeError("Server startup timed out")


def stop():
    global _thread, _server

    _stop.set()

    if _server is not None:
        try:
            _server.stop()
        except Exception:
            pass

    if _thread:
        _thread.join(timeout=5.0)
    _thread = None
    _server = None
