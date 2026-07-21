#!/usr/bin/env python3
"""distill.py - Mine per-module engineering knowledge from a GitHub repo.

Part of the `repo-history-distill` skill. Uses the authenticated `gh` CLI
(`gh api`) so it inherits your GitHub credentials and the 5000 req/hr limit.

Subcommands:
  fetch         Fetch merged PRs (+review/conversation comments) and bug/regression
                issues per module, into <out>/<owner>-<repo>/raw/.
  render        Turn the raw data into per-module <Module>.md skeletons for Copilot
                to finish as analytical prose.
  map-modules   Print a draft module map (JSON) from a local clone's directories.

Examples:
  python distill.py fetch --repo <owner>/<repo> --modules module-map.json \
      --since 2024-06-01 --max-prs 40 --out ./distilled
  python distill.py render --repo <owner>/<repo> --out ./distilled
  python distill.py map-modules <path-to-local-clone> > module-map.json
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

DEFAULT_IGNORE_USERS = {
    "dependabot[bot]", "dependabot", "github-actions[bot]", "github-actions",
    "codecov[bot]", "dependabot-preview[bot]", "renovate[bot]", "ghost",
    "github-advanced-security[bot]", "github-advanced-security",
}
MAINTAINER_ASSOC = {"OWNER", "MEMBER", "COLLABORATOR"}
MODULE_ROOTS = ["src/modules", "src", "apps", "packages", "components", "modules"]


def log(msg: str) -> None:
    print(f"[distill] {msg}", file=sys.stderr, flush=True)


API_ROOT = "https://api.github.com"
_TOKEN_CACHE: list[str] = []


def _resolve_token() -> str:
    """Find a GitHub token without spawning a process per call.

    Order: GH_TOKEN / GITHUB_TOKEN env, then `gh auth token`, then git credential.
    Cached for the process lifetime. Empty string means unauthenticated (60 req/hr).
    """
    if _TOKEN_CACHE:
        return _TOKEN_CACHE[0]
    tok = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN") or ""
    if not tok:
        try:
            p = subprocess.run(["gh", "auth", "token"], capture_output=True,
                               text=True, encoding="utf-8", errors="replace")
            if p.returncode == 0:
                tok = (p.stdout or "").strip()
        except FileNotFoundError:
            pass
    if not tok:
        try:
            p = subprocess.run(["git", "credential", "fill"], input=
                               "protocol=https\nhost=github.com\n\n",
                               capture_output=True, text=True,
                               encoding="utf-8", errors="replace")
            for line in (p.stdout or "").splitlines():
                if line.startswith("password="):
                    tok = line[len("password="):].strip()
                    break
        except FileNotFoundError:
            pass
    _TOKEN_CACHE.append(tok)
    if not tok:
        log("WARNING: no GitHub token found (env/gh/git credential). "
            "Running unauthenticated at 60 req/hr — expect throttling.")
    return tok


def _request(url: str, max_retries: int = 6):
    """Single HTTP GET returning (parsed_json, link_header). Handles rate limits."""
    token = _resolve_token()
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": "repo-history-distill",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    if token:
        headers["Authorization"] = f"Bearer {token}"
    attempt = 0
    while True:
        attempt += 1
        req = urllib.request.Request(url, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=60) as resp:
                body = resp.read().decode("utf-8", errors="replace")
                link = resp.headers.get("Link", "")
                return (json.loads(body) if body.strip() else None), link
        except urllib.error.HTTPError as e:
            if e.code == 404:
                return None, ""
            reset = e.headers.get("X-RateLimit-Remaining")
            if (e.code in (403, 429)) and attempt <= max_retries:
                retry_after = e.headers.get("Retry-After")
                if retry_after and retry_after.isdigit():
                    wait = min(90, int(retry_after))
                elif reset == "0":
                    reset_at = int(e.headers.get("X-RateLimit-Reset", "0"))
                    wait = max(1, min(90, reset_at - int(time.time()) + 1))
                else:
                    wait = min(60, 2 ** attempt)
                log(f"HTTP {e.code} (rate/abuse), backing off {wait}s "
                    f"(attempt {attempt}) …")
                time.sleep(wait)
                continue
            raise RuntimeError(f"GitHub API {e.code} for {url}: "
                               f"{e.read().decode('utf-8', 'replace')[:300]}")
        except urllib.error.URLError as e:
            if attempt <= max_retries:
                wait = min(30, 2 ** attempt)
                log(f"network error {e.reason}, retry in {wait}s …")
                time.sleep(wait)
                continue
            raise


def _next_link(link_header: str) -> str | None:
    for part in link_header.split(","):
        seg = part.split(";")
        if len(seg) >= 2 and 'rel="next"' in seg[1]:
            return seg[0].strip().strip("<>")
    return None


def gh_api(path: str, paginate: bool = False, max_retries: int = 6):
    """GET a GitHub REST path (e.g. '/repos/o/r/pulls/1'), returning parsed JSON.

    Direct HTTP (urllib) — no per-call `gh` subprocess. When paginate=True, follows
    the Link header and concatenates array pages into one flat list.
    """
    url = API_ROOT + path if path.startswith("/") else path
    data, link = _request(url, max_retries=max_retries)
    if data is None:
        return [] if paginate else {}
    if not paginate:
        return data
    if not isinstance(data, list):
        return data
    result = list(data)
    nxt = _next_link(link)
    while nxt:
        page, link = _request(nxt, max_retries=max_retries)
        if isinstance(page, list):
            result.extend(page)
        nxt = _next_link(link)
    return result


def check_auth() -> None:
    if not _resolve_token():
        log("Proceeding unauthenticated (60 req/hr). Set GH_TOKEN or run `gh auth login`.")


def norm(p: str) -> str:
    return p.replace("\\", "/").lstrip("./")


def match_module(path: str, module_map: dict) -> list[str]:
    p = norm(path)
    hits = []
    for name, prefixes in module_map.items():
        for pref in prefixes:
            if p.startswith(norm(pref)):
                hits.append(name)
                break
    return hits


# --------------------------------------------------------------------------- fetch
def normalize_map(raw: dict) -> dict:
    """Accept either {name: [paths]} or {name: {paths:[...], labels:[...]}}.

    Returns {name: {"paths": [...], "labels": [...]}}. `labels` are GitHub issue
    AREA labels (e.g. 'Area-<Module>' / 'Product-<Module>') used to find
    the module's bug/regression issues — text matching alone misses most of them.
    """
    out = {}
    for name, val in raw.items():
        if isinstance(val, dict):
            out[name] = {"paths": val.get("paths", []),
                         "labels": val.get("labels", []),
                         "keywords": val.get("keywords", [])}
        else:
            out[name] = {"paths": list(val), "labels": [], "keywords": []}
    return out


def _derive_keywords(module: str, labels: list) -> list:
    """Best-effort issue-search keywords when the map doesn't supply them.

    Combines the module name and each area label's product suffix, both as a
    spaced phrase and a compact form (e.g. 'Always On Top' + 'alwaysontop').
    """
    phrases = set()
    def add(s):
        s = (s or "").strip()
        if len(s) >= 3:
            phrases.add(s)
            phrases.add(s.replace(" ", ""))
    add(module)
    # split CamelCase module into words
    import re as _re
    spaced = _re.sub(r"(?<=[a-z])(?=[A-Z])", " ", module)
    if spaced != module:
        add(spaced)
    for l in labels:
        add(l.split("-", 1)[-1] if "-" in l else l)
    return sorted(phrases, key=len, reverse=True)[:4]


def cmd_fetch(args) -> int:
    check_auth()
    owner, repo = args.repo.split("/", 1)
    module_map = normalize_map(json.loads(Path(args.modules).read_text(encoding="utf-8")))
    ignore = DEFAULT_IGNORE_USERS | set(args.ignore_users or [])
    since = None
    if args.since:
        since = args.since if "T" in args.since else args.since + "T00:00:00Z"
    elif args.months:
        since = (datetime.now(timezone.utc) - timedelta(days=30 * args.months)) \
            .strftime("%Y-%m-%dT00:00:00Z")

    out_root = Path(args.out) / f"{owner}-{repo}" / "raw"
    out_root.mkdir(parents=True, exist_ok=True)
    (out_root / "modules.json").write_text(json.dumps(module_map, indent=2), encoding="utf-8")

    reg_labels = args.regression_labels or ["Regression", "regression", "bug", "Bug"]
    bug_labels = args.bug_labels or ["Issue-Bug"]
    exclude_prs = set(args.exclude_prs or [])

    for module, spec in module_map.items():
        prefixes = spec["paths"]
        area_labels = spec["labels"]
        keywords = spec.get("keywords") or _derive_keywords(module, area_labels)
        log(f"module: {module}")
        pr_numbers: dict[int, dict] = {}
        for pref in prefixes:
            q = f"/repos/{owner}/{repo}/commits?path={norm(pref)}&per_page=100"
            if since:
                q += f"&since={since}"
            commits = gh_api(q, paginate=True)
            if isinstance(commits, dict):
                commits = [commits]
            for c in commits[: args.max_commits]:
                sha = c.get("sha")
                if not sha:
                    continue
                pulls = gh_api(f"/repos/{owner}/{repo}/commits/{sha}/pulls")
                for pr in (pulls or []):
                    n = pr.get("number")
                    if n in exclude_prs:
                        continue  # held-out test PRs (train/test split for benchmarking)
                    if n and pr.get("merged_at") and n not in pr_numbers:
                        pr_numbers[n] = pr
                        if len(pr_numbers) >= args.max_prs:
                            break
                if len(pr_numbers) >= args.max_prs:
                    break
            if len(pr_numbers) >= args.max_prs:
                break

        prs, review_comments, conversation = [], [], []
        for n, pr in list(pr_numbers.items())[: args.max_prs]:
            full = gh_api(f"/repos/{owner}/{repo}/pulls/{n}")
            files = gh_api(f"/repos/{owner}/{repo}/pulls/{n}/files?per_page=100", paginate=True)
            file_paths = [f.get("filename") for f in (files or []) if isinstance(f, dict)]
            prs.append({
                "number": n,
                "title": full.get("title") if isinstance(full, dict) else pr.get("title"),
                "author": (full.get("user") or {}).get("login") if isinstance(full, dict) else None,
                "merged_at": pr.get("merged_at"),
                "labels": [l.get("name") for l in (full.get("labels") or [])] if isinstance(full, dict) else [],
                "body": (full.get("body") or "")[:4000] if isinstance(full, dict) else "",
                "files": [fp for fp in file_paths if match_module(fp or "", {module: prefixes})],
                "url": pr.get("html_url") or f"https://github.com/{owner}/{repo}/pull/{n}",
            })
            for rc in (gh_api(f"/repos/{owner}/{repo}/pulls/{n}/comments?per_page=100", paginate=True) or []):
                user = (rc.get("user") or {}).get("login")
                if user in ignore:
                    continue
                review_comments.append({
                    "pr": n, "author": user,
                    "author_association": rc.get("author_association"),
                    "path": rc.get("path"),
                    "line": rc.get("line") or rc.get("original_line"),
                    "diff_hunk": (rc.get("diff_hunk") or "")[:1200],
                    "in_reply_to": rc.get("in_reply_to_id"),
                    "body": (rc.get("body") or "")[:2000],
                    "reactions": (rc.get("reactions") or {}).get("total_count", 0),
                    "url": rc.get("html_url"),
                })
            for cc in (gh_api(f"/repos/{owner}/{repo}/issues/{n}/comments?per_page=100", paginate=True) or []):
                user = (cc.get("user") or {}).get("login")
                if user in ignore:
                    continue
                conversation.append({
                    "pr": n, "author": user,
                    "author_association": cc.get("author_association"),
                    "body": (cc.get("body") or "")[:2000],
                    "reactions": (cc.get("reactions") or {}).get("total_count", 0),
                    "url": cc.get("html_url"),
                })

        issues = []
        seen_issue = set()

        def _collect(query: str, require_match: bool):
            data = gh_api(query, paginate=True)
            for it in (data or []):
                if it.get("pull_request") or it.get("number") in seen_issue:
                    continue
                labels = [l.get("name") for l in (it.get("labels") or [])]
                if require_match:
                    text = f"{it.get('title','')} {it.get('body','') or ''}".lower()
                    if not (any(norm(p).rstrip("/").split("/")[-1].lower() in text
                                for p in prefixes) or module.lower() in text):
                        continue
                seen_issue.add(it.get("number"))
                issues.append({
                    "number": it.get("number"), "title": it.get("title"),
                    "labels": labels, "state": it.get("state"),
                    "is_bug": any(b.lower() in [x.lower() for x in labels] for b in bug_labels),
                    "url": it.get("html_url"),
                })

        # Preferred: area label (AND bug label when possible) — precise attribution.
        for area in area_labels:
            enc_area = urllib.parse.quote(area)
            for bug in bug_labels:
                _collect(f"/repos/{owner}/{repo}/issues?labels={enc_area},"
                         f"{urllib.parse.quote(bug)}&state=all&per_page=100", False)
            # also area-only (catches bugs not tagged Issue-Bug)
            _collect(f"/repos/{owner}/{repo}/issues?labels={enc_area}"
                     f"&state=all&per_page=100", False)
        # Fallback for repos without area labels: generic labels + text match.
        if not area_labels:
            for label in reg_labels:
                _collect(f"/repos/{owner}/{repo}/issues?labels="
                         f"{urllib.parse.quote(label)}&state=all&per_page=50", True)

        # KEYWORD SEARCH — catches issues that were mislabeled / never area-labeled.
        # Uses the Search API (title+body); restricted to issues (not PRs).
        if not args.no_keyword_search:
            for kw in keywords:
                q = (f'repo:{owner}/{repo} type:issue "{kw}" '
                     f'label:{",".join(bug_labels)}')
                # try bug-labeled first (precise), then unlabeled keyword hits
                for qq in (q, f'repo:{owner}/{repo} type:issue "{kw}" in:title'):
                    res = gh_api("/search/issues?per_page=30&q="
                                 + urllib.parse.quote(qq))
                    for it in (res.get("items", []) if isinstance(res, dict) else []):
                        if it.get("pull_request") or it.get("number") in seen_issue:
                            continue
                        labels = [l.get("name") for l in (it.get("labels") or [])]
                        seen_issue.add(it.get("number"))
                        issues.append({
                            "number": it.get("number"), "title": it.get("title"),
                            "labels": labels, "state": it.get("state"),
                            "is_bug": any(b.lower() in [x.lower() for x in labels]
                                          for b in bug_labels),
                            "matched_keyword": kw,
                            "url": it.get("html_url"),
                        })

        # Prefer bug-labeled, most-recent; cap to keep the LLM focused on signal.
        issues.sort(key=lambda i: (not i.get("is_bug", False), -(i.get("number") or 0)))
        issues = issues[: args.max_issues]

        mdir = Path(args.out) / f"{owner}-{repo}" / "raw" / _safe(module)
        mdir.mkdir(parents=True, exist_ok=True)
        (mdir / "prs.json").write_text(json.dumps(prs, indent=2), encoding="utf-8")
        (mdir / "review_comments.json").write_text(json.dumps(review_comments, indent=2), encoding="utf-8")
        (mdir / "conversation.json").write_text(json.dumps(conversation, indent=2), encoding="utf-8")
        (mdir / "issues.json").write_text(json.dumps(issues, indent=2), encoding="utf-8")
        log(f"  {len(prs)} PRs, {len(review_comments)} review comments, "
            f"{len(conversation)} conversation comments, {len(issues)} issues")
    log(f"done. raw data in {out_root}")
    return 0


def _safe(name: str) -> str:
    return "".join(c if c.isalnum() or c in "-_" else "_" for c in name)


# -------------------------------------------------------------------------- render
def cmd_render(args) -> int:
    owner, repo = args.repo.split("/", 1)
    raw_root = Path(args.out) / f"{owner}-{repo}" / "raw"
    if not raw_root.exists():
        log(f"no raw data at {raw_root}; run `fetch` first.")
        return 2
    module_map = json.loads((raw_root / "modules.json").read_text(encoding="utf-8"))
    out_dir = Path(args.out) / f"{owner}-{repo}"

    for module in module_map:
        mdir = raw_root / _safe(module)
        if not mdir.exists():
            continue
        prs = _load(mdir / "prs.json")
        rcs = _load(mdir / "review_comments.json")
        conv = _load(mdir / "conversation.json")
        issues = _load(mdir / "issues.json")

        def rank(c):
            score = c.get("reactions", 0) * 2
            if c.get("author_association") in MAINTAINER_ASSOC:
                score += 3
            score += min(len(c.get("body", "")) // 200, 3)  # longer = more substance
            return score

        top_comments = sorted(rcs + conv, key=rank, reverse=True)[: args.top]

        lines = [f"# {module} — Distilled Knowledge", "",
                 f"_Source: {owner}/{repo}. Generated skeleton — replace bracketed prompts "
                 f"with analytical prose._", "",
                 "## Overview", "", f"<!-- What {module} does; key entry points. -->", "",
                 "## Key Decisions", ""]
        for pr in prs[: args.top]:
            lines.append(f"- [#{pr['number']}]({pr['url']}) {pr['title']} "
                         f"<!-- decision / rejected alternative? -->")
        lines += ["", "## Important PR Comments", ""]
        for c in top_comments:
            tag = "maintainer" if c.get("author_association") in MAINTAINER_ASSOC else c.get("author_association", "")
            body = " ".join((c.get("body") or "").split())[:280]
            lines.append(f"- **@{c.get('author')}** ({tag}, {c.get('reactions',0)}👍) on "
                         f"PR #{c.get('pr')}: \"{body}\"  \n  [link]({c.get('url')}) "
                         f"<!-- why it matters -->")
        lines += ["", "## Regression History", ""]
        for it in issues[: args.top]:
            lines.append(f"- [#{it['number']}]({it['url']}) {it['title']} "
                         f"({it['state']}) <!-- symptom → root cause → fix → guardrail -->")
        lines += ["", "## Common Practices", "",
                  "<!-- Conventions the maintainers enforce in review: testing, threading, "
                  "settings round-trip, i18n, etc. Derive from the comments above. -->", ""]

        out_file = out_dir / f"{_safe(module)}.md"
        out_file.write_text("\n".join(lines), encoding="utf-8")
        log(f"rendered {out_file}")
    return 0


def _load(p: Path):
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else []


# --------------------------------------------------------------------- map-modules
def cmd_map(args) -> int:
    root = Path(args.repo_dir)
    if not root.exists():
        log(f"{root} does not exist")
        return 2
    result: dict[str, list[str]] = {}
    for mroot in MODULE_ROOTS:
        base = root / mroot
        if not base.is_dir():
            continue
        for child in sorted(base.iterdir()):
            if child.is_dir() and not child.name.startswith("."):
                name = child.name
                rel = norm(str(child.relative_to(root))) + "/"
                result.setdefault(name, [])
                if rel not in result[name]:
                    result[name].append(rel)
        if result:
            break  # first productive root wins
    print(json.dumps(result, indent=2))
    return 0


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="command", required=True)

    f = sub.add_parser("fetch", help="fetch raw history per module")
    f.add_argument("--repo", required=True, help="owner/name, e.g. <owner>/<repo>")
    f.add_argument("--modules", required=True, help="path to module-map.json")
    f.add_argument("--out", default="./distilled")
    f.add_argument("--since", help="ISO date lower bound, e.g. 2024-06-01")
    f.add_argument("--months", type=int, help="alternative to --since: last N months")
    f.add_argument("--max-prs", type=int, default=40, help="cap PRs per module")
    f.add_argument("--max-commits", type=int, default=300, help="cap commits scanned per path")
    f.add_argument("--regression-labels", nargs="*", help="issue labels treated as bugs/regressions")
    f.add_argument("--bug-labels", nargs="*", help="labels ANDed with a module's area label to find bug issues (default: Issue-Bug)")
    f.add_argument("--exclude-prs", nargs="*", type=int, help="PR numbers to hold out (train/test split for benchmarking)")
    f.add_argument("--max-issues", type=int, default=60, help="cap bug/regression issues kept per module")
    f.add_argument("--no-keyword-search", action="store_true", help="disable Search-API keyword issue mining (label-only)")
    f.add_argument("--ignore-users", nargs="*", help="extra bot/user logins to drop")
    f.set_defaults(func=cmd_fetch)

    r = sub.add_parser("render", help="render per-module markdown skeletons")
    r.add_argument("--repo", required=True)
    r.add_argument("--out", default="./distilled")
    r.add_argument("--top", type=int, default=12, help="items per section")
    r.set_defaults(func=cmd_render)

    m = sub.add_parser("map-modules", help="print a draft module map from a local clone")
    m.add_argument("repo_dir", help="path to a local clone of the repo")
    m.set_defaults(func=cmd_map)
    return p


def main() -> int:
    args = build_parser().parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
