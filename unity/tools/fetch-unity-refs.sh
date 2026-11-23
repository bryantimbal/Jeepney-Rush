#!/usr/bin/env bash
# Downloads Unity reference assemblies from NuGet (into unity/.unity-refs, git-ignored)
# so the C# can be compile-checked without a Unity installation:
#   ./unity/tools/fetch-unity-refs.sh && dotnet build unity/tools/CompileCheck
# These are API reference assemblies (UnityEngine 2021.3 modules + UnityEditor 2021.1).
# Unity 6 is a superset for every API this project uses; the real build happens in Unity.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dest="$here/../.unity-refs"
mkdir -p "$dest"
fetch() { # id version folder
  if [ -d "$dest/$3" ]; then return; fi
  curl -fsSL -o "$dest/$3.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg"
  mkdir -p "$dest/$3" && (cd "$dest/$3" && unzip -q -o "../$3.nupkg") && rm "$dest/$3.nupkg"
}
fetch unityengine.modules 2021.3.33 engine
fetch unity3d.sdk 2021.1.14.1 editor
# NuGet zips store entries without read permission for normal users (root doesn't notice; CI does)
chmod -R u+rwX,go+rX "$dest"
echo "Unity reference assemblies in $dest"
