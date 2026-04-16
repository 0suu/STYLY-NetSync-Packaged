"""Build a single-file STYLY NetSync Server executable using PyInstaller."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build a single executable for STYLY NetSync Server."
    )
    parser.add_argument(
        "--name",
        default="styly-netsync-server",
        help="Output executable name (default: styly-netsync-server).",
    )
    parser.add_argument(
        "--clean",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Remove previous build artifacts before building (default: true).",
    )
    parser.add_argument(
        "--format",
        choices=("onefile", "macos-app"),
        default="onefile",
        help=(
            "Build output format: onefile executable or macOS .app bundle "
            "(default: onefile)."
        ),
    )
    parser.add_argument(
        "--codesign-identity",
        default=None,
        help=(
            "macOS code-signing identity passed to PyInstaller so collected "
            "binaries are signed during the build."
        ),
    )
    return parser.parse_args()


def ensure_pyinstaller_installed() -> None:
    try:
        import PyInstaller  # noqa: F401
    except ImportError:
        print(
            "PyInstaller is not installed.\n"
            "Install it with either:\n"
            "  pip install pyinstaller\n"
            "or run via uv:\n"
            "  uv run --python 3.11 --with pyinstaller "
            "python scripts/build_single_executable.py",
            file=sys.stderr,
        )
        raise SystemExit(1) from None


def build_command(
    project_root: Path,
    app_name: str,
    build_format: str,
    codesign_identity: str | None,
) -> list[str]:
    entrypoint = project_root / "pyinstaller" / "entrypoint.py"
    if not entrypoint.exists():
        print(f"Entrypoint not found: {entrypoint}", file=sys.stderr)
        raise SystemExit(1)

    command = [
        sys.executable,
        "-m",
        "PyInstaller",
        "--noconfirm",
        "--name",
        app_name,
        "--paths",
        str(project_root / "src"),
        "--distpath",
        str(project_root / "dist"),
        "--workpath",
        str(project_root / "build"),
        "--specpath",
        str(project_root / "build"),
        "--collect-data",
        "styly_netsync",
        "--collect-binaries",
        "zmq",
        "--collect-submodules",
        "uvicorn",
        "--collect-submodules",
        "fastapi",
        "--collect-submodules",
        "starlette",
        "--collect-submodules",
        "pydantic",
        "--collect-submodules",
        "anyio",
        "--collect-submodules",
        "zmq.backend",
    ]

    if codesign_identity:
        command.extend(["--codesign-identity", codesign_identity])

    if build_format == "macos-app":
        command.extend(["--windowed"])
    else:
        command.extend(["--onefile", "--console"])

    command.append(str(entrypoint))
    return command


def main() -> None:
    args = parse_args()
    ensure_pyinstaller_installed()

    project_root = Path(__file__).resolve().parents[1]
    if args.clean:
        shutil.rmtree(project_root / "build", ignore_errors=True)
        shutil.rmtree(project_root / "dist", ignore_errors=True)

    cmd = build_command(
        project_root, args.name, args.format, args.codesign_identity
    )
    subprocess.run(cmd, check=True, cwd=project_root)

    if args.format == "macos-app":
        output_path = project_root / "dist" / f"{args.name}.app"
    else:
        extension = ".exe" if sys.platform.startswith("win") else ""
        output_path = project_root / "dist" / f"{args.name}{extension}"
    print(f"Build completed: {output_path}")


if __name__ == "__main__":
    main()
