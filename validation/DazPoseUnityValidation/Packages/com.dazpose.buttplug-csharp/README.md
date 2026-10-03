# Buttplug C# Unity runtime

This embedded Unity package contains `Buttplug.dll` from the official
[`Buttplug` NuGet package](https://www.nuget.org/packages/Buttplug/5.0.1), version
5.0.1, `lib/netstandard2.0` asset. The upstream source commit recorded in the
NuGet package is `ec8e6ff175e33dd75778cc91c485ec1a24353915`.

The OpenUPM `com.nonpolynomial.buttplug-unity` 4.0.0 package bundles the older
message-attribute API. It does not expose the v4 feature `OutputType` API needed
to discover `HwPositionWithDuration`. This package replaces that incompatible
assembly with the official NuGet build. `LICENSE` contains the upstream BSD
3-Clause terms.

DLL SHA-256:

```text
A335A5ADF4BA5D1E8F11AEE75366BB6BFAE61B6FA8B26D5DF58B3D16932FFA12
```
