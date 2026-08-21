#!/bin/bash
# Compile-check the project's C# without opening Unity.
#
# Compiles Assets/Scripts (runtime) and Assets/Editor (editor) with the Roslyn
# compiler that ships inside the Unity install, using the same reference set
# Unity itself would use. Nothing is written into the project — output goes to a
# temp dir and is thrown away.
#
# Usage: Tools/check-compile.sh
# Exits non-zero and prints Unity-style CSxxxx errors if anything fails.
#
# Why not `dotnet build Assembly-CSharp.csproj`? That builds every Unity package
# from source (~40s) and currently reports 2 pre-existing errors inside
# com.unity.ai.assistant, which drowns out our own.

set -uo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(awk '/^m_EditorVersion:/ {print $2}' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents"
SCRIPTING="$UNITY/Resources/Scripting"
DOTNET="$SCRIPTING/NetCoreRuntime/dotnet"
CSC="$SCRIPTING/DotNetSdkRoslyn/csc.dll"

if [ ! -x "$DOTNET" ]; then
  echo "Cannot find Unity $UNITY_VERSION at $UNITY" >&2
  exit 1
fi

OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

# A response file is required: the project path contains a space, and passing
# these args on the command line gets mangled by word-splitting.
emit_refs() {
  # $1 = "include-editor-modules" | "exclude-editor-modules"
  for dll in "$SCRIPTING/NetStandard/ref/2.1.0/"*.dll; do
    printf -- '-r:"%s"\n' "$dll"
  done
  for dll in "$SCRIPTING/Managed/UnityEngine/"*.dll; do
    if [ "$1" = "exclude-editor-modules" ]; then
      # UnityEditor.dll and UnityEditor.*Module.dll both define types like
      # MenuItem. Referencing both yields spurious CS0433 ambiguity errors.
      case "$(basename "$dll")" in UnityEditor.*Module.dll) continue ;; esac
    fi
    printf -- '-r:"%s"\n' "$dll"
  done
  for name in Unity.InputSystem Unity.InputSystem.ForUI UnityEngine.UI Unity.TextMeshPro; do
    printf -- '-r:"%s"\n' "$PROJECT/Library/ScriptAssemblies/$name.dll"
  done
}

status=0

{
  echo "-target:library"
  echo "-nologo"
  echo "-nostdlib"
  echo "-langversion:9.0"
  echo "-nowarn:0649"
  printf -- '-out:"%s/Runtime.dll"\n' "$OUT"
  emit_refs include-editor-modules
  find "$PROJECT/Assets/Scripts" -name '*.cs' -exec printf '"%s"\n' {} \;
} > "$OUT/runtime.rsp"

echo "--- runtime (Assets/Scripts) ---"
if ! "$DOTNET" "$CSC" "@$OUT/runtime.rsp"; then
  # The editor pass references Runtime.dll, so it would only add cascade noise.
  echo "=== compile FAILED (runtime) ===" >&2
  exit 1
fi

{
  echo "-target:library"
  echo "-nologo"
  echo "-nostdlib"
  echo "-langversion:9.0"
  echo "-nowarn:0649"
  printf -- '-out:"%s/Editor.dll"\n' "$OUT"
  emit_refs exclude-editor-modules
  printf -- '-r:"%s/Managed/UnityEditor.dll"\n' "$SCRIPTING"
  printf -- '-r:"%s/Runtime.dll"\n' "$OUT"
  find "$PROJECT/Assets/Editor" -name '*.cs' -exec printf '"%s"\n' {} \;
} > "$OUT/editor.rsp"

echo "--- editor (Assets/Editor) ---"
"$DOTNET" "$CSC" "@$OUT/editor.rsp" || status=1

if [ "$status" -eq 0 ]; then
  echo "=== compile clean ==="
else
  echo "=== compile FAILED ===" >&2
fi
exit "$status"
