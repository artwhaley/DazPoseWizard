# Buttplug C# Unity runtime

This embedded package contains Buttplug C# 5.0.1 rebuilt from the official
source commit `ec8e6ff175e33dd75778cc91c485ec1a24353915`.

The supported reproducible build inputs are pinned to:

- Unity Editor `6000.5.9f1` and its .NET Standard 2.1 reference assemblies.
- Unity Roslyn SDK `8.0.318` shipped with that editor.
- `com.unity.nuget.newtonsoft-json` package version `3.2.2`, runtime assembly
  SHA-256 `7292D3EB508652D14726749DD27094F2D481AECCF2DB6427B62F68A71460897E`.
- Upstream Buttplug source commit listed above; no local source patches.
- Unity assembly version `5.0.1.0` and deterministic compilation.

The upstream NuGet binary requires Newtonsoft.Json 13.0.4. Unity's 3.2.2
package does not expose `JToken.ToString(Formatting)`, so the original binary
throws `MissingMethodException` during connection initialization. Building the
same source against Unity's assembly selects the compatible overload
`ToString(Formatting, params JsonConverter[])`.

Rebuild from the repository root with `scripts/build-buttplug-unity.ps1`. Import
the project first so its package cache is populated. The script checks the
Unity editor version, exact cached JSON package version and hash, Roslyn SDK,
and resulting Buttplug binary hash before replacing the embedded DLL. It does
not select an arbitrary cache entry.

The checked-in `Buttplug.dll` SHA-256 is:

```text
3D528966315940E1E944AB038FC2EFBE26E3BD103E9918CCBFD8D67AE6FCCC6A
```

No second JSON library is bundled. The OpenUPM
`com.nonpolynomial.buttplug-unity` 4.0.0 package contains an older
message-attribute API and does not expose the v4 feature output model required
for `HwPositionWithDuration`. `LICENSE` contains the upstream BSD 3-Clause
terms.
