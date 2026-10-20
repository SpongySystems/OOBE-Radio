#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
game_directory="${1:-${VINTAGE_STORY:-}}"
if [[ -z "$game_directory" || ! -f "$game_directory/VintagestoryAPI.dll" ]]; then
  echo 'Usage: bash Build.sh "/path/to/VintageStory-1.22.7"' >&2
  exit 1
fi
command -v dotnet >/dev/null || { echo 'Install the .NET 10 SDK first.' >&2; exit 1; }
command -v zip >/dev/null || { echo 'Install zip first (or use Build.ps1 with PowerShell).' >&2; exit 1; }
output_directory="$project_root/build/compiled"
package_directory="$project_root/build/package"
dotnet build "$project_root/OobeRadio.csproj" -c Release "-p:GamePath=$game_directory" -o "$output_directory"
rm -rf -- "$package_directory"
mkdir -p "$package_directory" "$project_root/Releases"
cp "$output_directory/OobeRadio.dll" "$output_directory/NLayer.dll" "$package_directory/"
cp "$project_root/modinfo.json" "$project_root/modicon.png" "$package_directory/"
cp -R "$project_root/assets" "$project_root/licenses" "$package_directory/"
version="$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' "$project_root/modinfo.json")"
archive_path="$project_root/Releases/ooberadio_$version.zip"
rm -f -- "$archive_path"
(cd "$package_directory" && zip -q -r "$archive_path" .)
echo "Ready to install: $archive_path"
