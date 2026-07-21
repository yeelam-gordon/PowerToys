# Module Map

A **module map** tells the distiller how repository paths group into named modules.
It is a JSON object: `{ "ModuleName": ["path/prefix/", "another/path/"] }`.

## Worked Example

A repo that keeps each module under a common root (e.g. `src/modules/<name>/`) maps close to 1:1:

```json
{
  "ModuleA":  ["src/modules/module-a/"],
  "ModuleB":  ["src/modules/module-b/"],
  "ModuleC":  ["src/modules/module-c/"],
  "Settings": ["src/settings-ui/"]
}
```

Layouts differ per repo (`packages/<name>/`, `apps/<name>/`, `components/<name>/`, …); inspect the
target repo (or ask the user) and adapt.

## Building a Map

1. From a local clone, seed a draft:
   `python scripts/distill.py --map-modules <repo_dir> > module-map.json`
   This lists immediate subdirectories of common roots (`src/`, `src/modules/`, `apps/`,
   `packages/`, `components/`) as candidate modules.
2. **Edit it.** Merge directories that form one feature; drop build/infra dirs
   (`.github/`, `installer/`, `deps/`, `tools/`).
3. Validate a path has history:
   `gh api "/repos/{owner}/{repo}/commits?path=<dir>&per_page=1"` — a non-empty result
   confirms the path is real and tracked.

## Guidance

- **Prefer directory prefixes** over single files — modules evolve and files move.
- **One module = one mental model.** If contributors think of a feature area as one thing,
  keep it one module even if it spans several directories.
- **Trailing slash matters.** `src/modules/foo/` will not match `src/modules/foobar/`;
  the script does prefix matching on normalized POSIX paths.
- Keep the map in the repo you are distilling (or alongside the output) so runs are
  reproducible.
