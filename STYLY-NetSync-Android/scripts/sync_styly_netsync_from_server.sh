#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'USAGE'
Usage:
  sync_styly_netsync_from_server.sh [--check]

Options:
  --check   Validate sync state without modifying files.
USAGE
}

CHECK_ONLY=false
if [[ "${1:-}" == "--check" ]]; then
    CHECK_ONLY=true
    shift
fi

if [[ $# -ne 0 ]]; then
    usage
    exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ANDROID_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${ANDROID_ROOT}/.." && pwd)"

UPSTREAM_DIR="${REPO_ROOT}/STYLY-NetSync-Server/src/styly_netsync"
PATCH_ROOT="${ANDROID_ROOT}/python-overrides/patches/styly_netsync"
SERIES_FILE="${PATCH_ROOT}/series"
CANONICAL_DIR="${ANDROID_ROOT}/python-source/styly_netsync"
APP_DIR="${ANDROID_ROOT}/app/src/main/python/styly_netsync"
AAR_DIR="${ANDROID_ROOT}/netsync-aar/src/main/python/styly_netsync"

fail() {
    echo "[sync] ERROR: $*" >&2
    exit 1
}

require_dir() {
    local dir="$1"
    [[ -d "${dir}" ]] || fail "Missing directory: ${dir}"
}

require_file() {
    local file="$1"
    [[ -f "${file}" ]] || fail "Missing file: ${file}"
}

compare_dirs() {
    local expected="$1"
    local actual="$2"
    local label="$3"

    if [[ ! -d "${expected}" ]]; then
        fail "Expected directory not found for ${label}: ${expected}"
    fi
    if [[ ! -d "${actual}" ]]; then
        fail "Actual directory not found for ${label}: ${actual}"
    fi

    local diff_output
    if ! diff_output=$(diff -qr -x __pycache__ "${expected}" "${actual}" || true); then
        :
    fi

    if [[ -n "${diff_output}" ]]; then
        echo "[sync] Mismatch detected: ${label}" >&2
        echo "${diff_output}" >&2
        return 1
    fi

    return 0
}

mirror_copy() {
    local src="$1"
    local dst="$2"

    mkdir -p "${dst}"
    rsync -a --delete --exclude '__pycache__/' "${src}/" "${dst}/"
}

require_dir "${UPSTREAM_DIR}"
require_file "${SERIES_FILE}"

TMP_DIR="$(mktemp -d)"
cleanup() {
    rm -rf "${TMP_DIR}"
}
trap cleanup EXIT

WORKTREE_DIR="${TMP_DIR}/worktree"
mkdir -p "${WORKTREE_DIR}/styly_netsync"

# 1) Copy upstream source into isolated worktree.
rsync -a --delete --exclude '__pycache__/' "${UPSTREAM_DIR}/" "${WORKTREE_DIR}/styly_netsync/"

# 2) Apply Android-specific patches in series order with 3-way merge support.
git -C "${WORKTREE_DIR}" init -q
git -C "${WORKTREE_DIR}" add styly_netsync
git -C "${WORKTREE_DIR}" -c user.name='sync-bot' -c user.email='sync-bot@example.com' commit -q -m 'upstream snapshot'

while IFS= read -r entry || [[ -n "${entry}" ]]; do
    if [[ -z "${entry}" || "${entry}" == \#* ]]; then
        continue
    fi

    patch_file="${PATCH_ROOT}/${entry}"
    require_file "${patch_file}"

    if ! git -C "${WORKTREE_DIR}" apply --3way --whitespace=nowarn "${patch_file}"; then
        fail "Patch apply failed: ${entry}"
    fi

done < "${SERIES_FILE}"

if [[ "${CHECK_ONLY}" == true ]]; then
    # 3) Verify generated trees are already in sync.
    compare_dirs "${WORKTREE_DIR}/styly_netsync" "${CANONICAL_DIR}" "canonical python-source" || fail "python-source is out of sync"
    compare_dirs "${WORKTREE_DIR}/styly_netsync" "${APP_DIR}" "app module python source" || fail "app module is out of sync"
    compare_dirs "${WORKTREE_DIR}/styly_netsync" "${AAR_DIR}" "netsync-aar module python source" || fail "netsync-aar module is out of sync"
    compare_dirs "${APP_DIR}" "${AAR_DIR}" "app vs netsync-aar parity" || fail "app and netsync-aar are not identical"

    echo "[sync] OK: all Python source trees are synchronized."
    exit 0
fi

# 4) Mirror generated result to canonical and module output trees.
mirror_copy "${WORKTREE_DIR}/styly_netsync" "${CANONICAL_DIR}"
mirror_copy "${WORKTREE_DIR}/styly_netsync" "${APP_DIR}"
mirror_copy "${WORKTREE_DIR}/styly_netsync" "${AAR_DIR}"

# 5) Strict post-copy parity checks.
compare_dirs "${APP_DIR}" "${AAR_DIR}" "app vs netsync-aar parity" || fail "post-copy parity check failed"

echo "[sync] Updated python-source, app, and netsync-aar from Server upstream + Android patches."
