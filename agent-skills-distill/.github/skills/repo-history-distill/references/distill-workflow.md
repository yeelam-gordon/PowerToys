# Distill Workflow — Data Model & Query Recipes

This document backs the `repo-history-distill` skill. Read it when you need the exact
API calls or the JSON shape the script produces.

## Data Model

`distill.py fetch` writes, under `--out/<owner>-<repo>/raw/`:

```
raw/
  modules.json            # the resolved module map (name -> [paths])
  <Module>/
    prs.json              # merged PRs touching the module, with metadata
    review_comments.json  # line-level review comments (/pulls/{n}/comments)
    conversation.json     # PR conversation comments (/issues/{n}/comments)
    issues.json           # issues labeled as bug/regression touching the area
```

### `prs.json` element

```json
{
  "number": 12345,
  "title": "ModuleA: fix DPI scaling on multi-monitor",
  "author": "someone",
  "merged_at": "2024-06-01T12:00:00Z",
  "labels": ["ModuleA", "Regression"],
  "body": "…",
  "files": ["src/modules/module-a/…"],
  "url": "https://github.com/{owner}/{repo}/pull/12345"
}
```

### `review_comments.json` element

```json
{
  "pr": 12345,
  "author": "maintainer",
  "author_association": "MEMBER",
  "path": "src/modules/module-a/…/Foo.cpp",
  "body": "We can't take a global lock here — it deadlocks the UI thread.",
  "reactions": 4,
  "url": "https://github.com/{owner}/{repo}/pull/12345#discussion_r…"
}
```

## Query Recipes (gh CLI)

### Which PRs touched a module path?

Search cannot filter PRs by path. Enumerate commits for the path, then resolve PRs:

```bash
# commits under a directory
gh api "/repos/{owner}/{repo}/commits?path=src/modules/module-a/&since=2024-01-01T00:00:00Z&per_page=100" --paginate

# PRs that contain a commit
gh api "/repos/{owner}/{repo}/commits/{sha}/pulls"
```

### GraphQL alternative (fewer round-trips)

```graphql
query($owner:String!, $repo:String!, $path:String!, $cursor:String) {
  repository(owner:$owner, name:$repo) {
    ref(qualifiedName:"refs/heads/main") {
      target { ... on Commit {
        history(path:$path, first:100, after:$cursor) {
          pageInfo { hasNextPage endCursor }
          nodes {
            associatedPullRequests(first:1) {
              nodes { number title mergedAt author { login }
                labels(first:20){ nodes { name } } url }
            }
          }
        }
      }}
    }
  }
}
```

### PR review comments vs conversation

```bash
gh api "/repos/{owner}/{repo}/pulls/{n}/comments" --paginate      # line-level review
gh api "/repos/{owner}/{repo}/issues/{n}/comments"  --paginate     # conversation
gh api "/repos/{owner}/{repo}/pulls/{n}"                            # body + merged state
```

### Regression / bug issues

```bash
gh api "/repos/{owner}/{repo}/issues?labels=Regression&state=all&per_page=100" --paginate
```

Attribute an issue to a module by matching its title/body or linked PR files against the
module map paths.

## Reactions & Association (ranking signal)

- `author_association` ∈ {`OWNER`,`MEMBER`,`COLLABORATOR`,`CONTRIBUTOR`,`NONE`}.
  Weight `OWNER`/`MEMBER`/`COLLABORATOR` higher — those are maintainers.
- `reactions.total_count` on a comment is a crowd signal; sort important comments by it.

## Rendering

`distill.py render` emits one `<Module>.md` skeleton per module with the five sections
and pre-populated candidate lists (top PRs, top-reacted maintainer comments, regression
issues). Copilot then rewrites each section as analytical prose — see the "What Important
Means" section of SKILL.md.
