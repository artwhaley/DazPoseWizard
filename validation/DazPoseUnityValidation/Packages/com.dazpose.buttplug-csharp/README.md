# Buttplug C# Unity runtime

This embedded Unity package contains `Buttplug.dll` rebuilt from the official
Buttplug C# 5.0.1 source commit
`ec8e6ff175e33dd75778cc91c485ec1a24353915`, targeting Unity's .NET Standard 2.1
references and its installed `com.unity.nuget.newtonsoft-json` assembly.

The upstream NuGet binary requires Newtonsoft.Json 13.0.4. Unity's 3.2.2 package
does not expose `JToken.ToString(Formatting)`, so the original binary throws
`MissingMethodException` during the connection handshake. Compiling the same
unmodified source against Unity's assembly selects the compatible overload
`ToString(Formatting, params JsonConverter[])`.

Rebuild from the repository root with `./scripts/build-buttplug-unity.ps1`.
The script downloads the pinned source, uses Unity's Roslyn compiler and runtime
references, and preserves assembly version 5.0.1.0. Import the project first so
its Newtonsoft package cache is populated. No second JSON library is bundled.

The OpenUPM `com.nonpolynomial.buttplug-unity` 4.0.0 package bundles the older
message-attribute API. It does not expose the v4 feature `OutputType` API needed
to discover `HwPositionWithDuration`. This package replaces that incompatible
assembly with this Unity-compatible build. `LICENSE` contains the upstream BSD
3-Clause terms.

DLL SHA-256:

```text
3D528966315940E1E944AB038FC2EFBE26E3BD103E9918CCBFD8D67AE6FCCC6A
```
