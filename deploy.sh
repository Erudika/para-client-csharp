#!/usr/bin/env bash
set -euo pipefail

#lastver=$(git describe --abbrev=0 --tags)
#echo "Last tag was: $lastver"
#echo "---"
#read -e -p "Tag: " ver
#
#sed -i -e "s/>${lastver:1}</>$ver</g" para-client-csharp/para-client-csharp.csproj
#
#git add -A && git commit -m "Release v$ver."
#git tag "v$ver"
#git push origin master && git push --tags

repo_root="$(cd "$(dirname "$0")" && pwd)"
project="$repo_root/para-client-csharp/para-client-csharp.csproj"
test_project="$repo_root/para-client-tests/para-client-tests.csproj"
artifacts_dir="$repo_root/artifacts"
nuget_source="https://api.nuget.org/v3/index.json"

mkdir -p "$artifacts_dir"

last_tag="$(git describe --abbrev=0 --tags 2>/dev/null || echo 'v0.0.0')"
echo "Last tag: $last_tag"
read -r -p "Release version (ex: 1.34.0): " release_version

if [[ -z "${release_version}" ]]; then
  echo "Release version is required."
  exit 1
fi

release_version="${release_version#v}"
echo "Restoring and building..."
dotnet restore "$project"
dotnet build "$project" -c Release

echo "Running tests..."
#dotnet test "$test_project" -c Release --no-build

echo "Packing version $release_version..."
dotnet pack "$project" -c Release -o "$artifacts_dir" \
  -p:PackageVersion="$release_version" \
  -p:Version="$release_version" \
  -p:ContinuousIntegrationBuild=true

nupkg_path="$(find "$artifacts_dir" -maxdepth 1 -name "*.${release_version}.nupkg" -print -quit)"
snupkg_path="$(find "$artifacts_dir" -maxdepth 1 -name "*.${release_version}.snupkg" -print -quit)"

if [[ -z "$nupkg_path" ]]; then
  echo "Failed to locate packaged nupkg."
  exit 1
fi

if [[ -z "${NUGET_API_KEY:-}" ]]; then
  echo "NUGET_API_KEY environment variable must be set before publishing."
  exit 1
fi

echo "Publishing package to NuGet..."
dotnet nuget push "$nupkg_path" --source "$nuget_source" --api-key "$NUGET_API_KEY" --skip-duplicate

if [[ -n "$snupkg_path" ]]; then
  dotnet nuget push "$snupkg_path" --source "$nuget_source" --api-key "$NUGET_API_KEY" --skip-duplicate
fi

git tag "v$release_version"
git push origin "v$release_version"

echo "Release v$release_version completed."
