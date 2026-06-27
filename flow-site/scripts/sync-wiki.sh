#!/usr/bin/env bash
#
# sync-wiki.sh — populate src/docs/wiki/ with the 26 flow-sharp wiki pages (D-49-25).
#
# Two modes, in priority order:
#   1. CLONE  — if WIKI_REPO_URL is set, `git clone --depth 1 "$WIKI_REPO_URL"` into the
#               destination. This is the Cloudflare Pages build path: the wiki repo is cloned
#               fresh on every deploy (set WIKI_REPO_URL in the CF Pages env, a tokenized HTTPS
#               URL if the wiki is private — RESEARCH Pitfall 6).
#   2. SEED   — if WIKI_REPO_URL is unset OR the clone fails, copy the in-repo `wiki/` seed
#               (the 26 markdown files committed at the repo root) into the destination. This
#               is the local-dev + CI path, and a resilience fallback so a transient clone
#               failure never blocks the build / ships empty docs.
#
# Fails loudly (set -euo pipefail) — a wiki we cannot populate from EITHER source is a hard
# error, never a silent empty-docs ship (Pitfall 6, threat T-49-04-SYNC).
#
# Run from the flow-site/ directory (pnpm runs scripts from package root) or anywhere — paths
# are resolved relative to this script's own location.

set -euo pipefail

# Resolve flow-site/ (script lives in flow-site/scripts/) and the repo root (its parent).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SITE_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${SITE_DIR}/.." && pwd)"

DEST="${SITE_DIR}/src/docs/wiki"
SEED="${REPO_ROOT}/wiki"

# WIKI_REPO_URL is optional; treat unset as empty (compatible with set -u).
WIKI_REPO_URL="${WIKI_REPO_URL:-}"

# Count *.md files in a directory (0 if the dir does not exist).
count_md() {
	local dir="$1"
	if [ -d "$dir" ]; then
		find "$dir" -maxdepth 1 -name '*.md' -type f | wc -l | tr -d '[:space:]'
	else
		echo 0
	fi
}

seed_from_repo() {
	if [ ! -d "$SEED" ]; then
		echo "[sync-wiki] FATAL: in-repo wiki seed not found at ${SEED}" >&2
		return 1
	fi
	local seed_count
	seed_count="$(count_md "$SEED")"
	if [ "$seed_count" -eq 0 ]; then
		echo "[sync-wiki] FATAL: in-repo wiki seed at ${SEED} has zero markdown files" >&2
		return 1
	fi
	echo "[sync-wiki] seeding docs from in-repo wiki/ (${seed_count} pages) -> ${DEST}"
	rm -rf "$DEST"
	mkdir -p "$DEST"
	# Copy only the markdown files (the wiki seed is flat markdown).
	cp "$SEED"/*.md "$DEST"/
}

clone_from_remote() {
	echo "[sync-wiki] cloning wiki from \$WIKI_REPO_URL -> ${DEST}"
	rm -rf "$DEST"
	mkdir -p "$(dirname "$DEST")"
	# --depth 1: we only need the current snapshot, not wiki history.
	git clone --depth 1 "$WIKI_REPO_URL" "$DEST"
}

if [ -n "$WIKI_REPO_URL" ]; then
	if clone_from_remote; then
		echo "[sync-wiki] clone succeeded."
	else
		echo "[sync-wiki] WARNING: clone from \$WIKI_REPO_URL failed — falling back to in-repo seed." >&2
		seed_from_repo
	fi
else
	echo "[sync-wiki] WIKI_REPO_URL unset — seeding from in-repo wiki/ (local/CI path)."
	seed_from_repo
fi

# Final hard check: the destination MUST have markdown, or fail the build loudly.
FINAL_COUNT="$(count_md "$DEST")"
if [ "$FINAL_COUNT" -eq 0 ]; then
	echo "[sync-wiki] FATAL: ${DEST} has zero markdown pages after sync — refusing to ship empty docs." >&2
	exit 1
fi

echo "[sync-wiki] OK — ${FINAL_COUNT} wiki pages synced to ${DEST}"
