#!/usr/bin/env bash
# Lists every outdated dependency in the repository - NuGet, npm and GitHub Actions - and changes nothing.
# Run it before a release: take the minor and patch updates, and either take each major or write down why it stays
# pinned (an `ignore` with a comment in .github/dependabot.yml). `npm outdated` and `npm update` stay inside each
# package.json range (^6.1.0 never shows 7.x), so the npm part uses npm-check-updates, which lists the majors too.
#
#   scripts/check-outdated.sh            all three
#   scripts/check-outdated.sh npm        one of: nuget, npm, actions
set -uo pipefail

cd "$(dirname "$0")/.." || exit 1
only="${1:-all}"

# The npm folders Dependabot watches (.github/dependabot.yml)
npm_dirs=(
  test/NodeWorkspace
  test/Showcase.E2ETests
  sample/Showcase/Showcase.ReactApp.Fetch
  sample/Showcase/Showcase.ReactApp.Axios
)

if [[ "$only" == all || "$only" == nuget ]]; then
  echo "=== NuGet"
  # One line per package (the resolved version and the latest, in how many projects), majors marked
  dotnet list Atc.Rest.Api.SourceGenerator.slnx package --outdated 2>&1 \
    | awk '$1 == ">" { key = $2 " " $(NF-1) " -> " $NF; count[key]++ }
      END {
        for (key in count) {
          split(key, part, " "); split(part[2], from, "."); split(part[4], to, ".")
          printf "  %-55s %3d project(s)%s\n", key, count[key], (from[1] != to[1] ? "  MAJOR" : "")
        }
      }' \
    | sort
  echo
fi

if [[ "$only" == all || "$only" == npm ]]; then
  for dir in "${npm_dirs[@]}"; do
    echo "=== npm: $dir"
    (cd "$dir" && npx --yes npm-check-updates --format group --errorLevel 1 2>&1 | grep -v -E "^(Checking|Run ncu|Run npx|$)")
    echo
  done
fi

if [[ "$only" == all || "$only" == actions ]]; then
  echo "=== GitHub Actions (pinned SHA, # tag) against the latest release"
  grep -h -o -E "uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40} # v[0-9.]+" .github/workflows/*.yml \
    | sort -u \
    | while read -r _ ref _ tag; do
        repo="${ref%@*}"
        latest="$(gh api "repos/$repo/releases/latest" --jq .tag_name 2>/dev/null || echo "?")"
        if [[ "$latest" != "$tag" ]]; then
          echo "  $repo $tag -> $latest"
        fi
      done
  echo
fi
