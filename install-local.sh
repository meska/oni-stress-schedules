#!/bin/zsh
set -euo pipefail

project_dir=${0:A:h}
mod_dir="$HOME/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/OniStressSchedules"
dll_path="$project_dir/src/OniStressSchedules/bin/Release/net48/OniStressSchedules.dll"

# Prima compila, dopo copia: cussì no resta mai una mod mezza cotta.
dotnet build "$project_dir/src/OniStressSchedules/OniStressSchedules.csproj" -c Release
mkdir -p "$mod_dir"
cp "$project_dir/package/mod.yaml" "$mod_dir/mod.yaml"
cp "$project_dir/package/mod_info.yaml" "$mod_dir/mod_info.yaml"
# No sta coprir le soglie che l'utente ga personalizà.
if [[ ! -f "$mod_dir/config.json" ]]; then
  cp "$project_dir/package/config.json" "$mod_dir/config.json"
fi
cp "$dll_path" "$mod_dir/OniStressSchedules.dll"

echo "Installed Stress Schedules in: $mod_dir"
