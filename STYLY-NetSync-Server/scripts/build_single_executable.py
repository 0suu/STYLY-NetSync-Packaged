"""Build a single-file STYLY NetSync Server executable using PyInstaller."""

from __future__ import annotations

import argparse
import plistlib
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
        choices=("onefile", "macos-app", "macos-app-onefile"),
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
    parser.add_argument(
        "--bundle-identifier",
        default="com.styly.netsync.server",
        help=(
            "Bundle identifier used when building a macOS .app bundle "
            "(default: com.styly.netsync.server)."
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
    elif build_format == "macos-app-onefile":
        command.extend(["--onefile", "--console"])
    else:
        command.extend(["--onefile", "--console"])

    command.append(str(entrypoint))
    return command


def create_macos_app_bundle(
    dist_dir: Path,
    app_name: str,
    bundle_identifier: str,
) -> Path:
    binary_path = dist_dir / app_name
    if not binary_path.exists():
        print(f"Executable not found: {binary_path}", file=sys.stderr)
        raise SystemExit(1)

    app_path = dist_dir / f"{app_name}.app"
    contents_dir = app_path / "Contents"
    macos_dir = contents_dir / "MacOS"
    resources_dir = contents_dir / "Resources"
    bin_dir = resources_dir / "bin"

    shutil.rmtree(app_path, ignore_errors=True)
    macos_dir.mkdir(parents=True, exist_ok=True)
    bin_dir.mkdir(parents=True, exist_ok=True)
    shutil.move(str(binary_path), str(bin_dir / app_name))

    launcher_path = macos_dir / app_name
    launcher_path.write_text(
        "\n".join(
            [
                "#!/bin/bash",
                "set -euo pipefail",
                'APP_CONTENTS_DIR="$(cd "$(dirname "$0")/.." && pwd)"',
                'open -a Terminal "$APP_CONTENTS_DIR/Resources/run.command"',
                "",
            ]
        ),
        encoding="utf-8",
    )
    launcher_path.chmod(0o755)

    run_command_path = resources_dir / "run.command"
    run_command_path.write_text(
        "\n".join(
            [
                "#!/bin/bash",
                "set -euo pipefail",
                'SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"',
                'APP_BUNDLE_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"',
                'APP_PARENT_DIR="$(cd "$APP_BUNDLE_DIR/.." && pwd)"',
                'AUTO_CONFIG_PATH="$APP_PARENT_DIR/config.toml"',
                'HAS_EXPLICIT_CONFIG=0',
                'for arg in "$@"; do',
                '  if [[ "$arg" == "--config" || "$arg" == --config=* ]]; then',
                "    HAS_EXPLICIT_CONFIG=1",
                "    break",
                "  fi",
                "done",
                'if [[ $HAS_EXPLICIT_CONFIG -eq 0 && -f "$AUTO_CONFIG_PATH" ]]; then',
                f'  exec "$SCRIPT_DIR/bin/{app_name}" --config "$AUTO_CONFIG_PATH" "$@"',
                "fi",
                f'exec "$SCRIPT_DIR/bin/{app_name}" "$@"',
                "",
            ]
        ),
        encoding="utf-8",
    )
    run_command_path.chmod(0o755)

    plist_path = contents_dir / "Info.plist"
    with plist_path.open("wb") as plist_file:
        plistlib.dump(
            {
                "CFBundleDevelopmentRegion": "en",
                "CFBundleExecutable": app_name,
                "CFBundleIdentifier": bundle_identifier,
                "CFBundleInfoDictionaryVersion": "6.0",
                "CFBundleName": app_name,
                "CFBundlePackageType": "APPL",
                "CFBundleShortVersionString": "1.0",
                "CFBundleVersion": "1",
                "LSMinimumSystemVersion": "11.0",
                "LSUIElement": True,
            },
            plist_file,
            sort_keys=True,
        )

    return app_path


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
    elif args.format == "macos-app-onefile":
        output_path = create_macos_app_bundle(
            project_root / "dist", args.name, args.bundle_identifier
        )
    else:
        extension = ".exe" if sys.platform.startswith("win") else ""
        output_path = project_root / "dist" / f"{args.name}{extension}"
    print(f"Build completed: {output_path}")


if __name__ == "__main__":
    main()
