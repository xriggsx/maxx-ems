# lib/

This folder holds reference-only DLLs vendored from the RPH SDK, since the dev PC
has no RPH runtime installed.

- `RagePluginHookSDK.dll` — from the RAGE Plugin Hook SDK download (version
  1.131.1424.17745, `SDK\RagePluginHookSDK.dll` in the SDK zip). Despite the
  filename, its assembly identity is `RagePluginHook` (confirmed via reflection),
  which is what `EmsMod.csproj` references. Referenced with `Private = False`, so
  it is **not** copied into the build output — the actual RPH host process
  supplies this assembly at runtime; this copy is compile-time only.

If the gaming PC's RPH install updates to a new version, re-copy the matching SDK
DLL here and re-commit to keep the dev PC's compile-time reference in sync with
the runtime version actually installed.
