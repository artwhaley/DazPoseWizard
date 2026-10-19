# Bundled TMP resources

Copied the installed com.unity.ugui TMP Essential Resources.unitypackage to its original asset paths, preserving all original metas and the Liberation Sans OFL license. No package download or Unity launch. These supply runtime fonts, settings and shaders required by the shared TMP UI.

- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader
- Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader
- Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader
- Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader
- Assets/TextMesh Pro/Resources
- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SpaceWarp.shader
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD SpaceWarp.shader
- Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc
- Assets/TextMesh Pro/Resources/TMP Settings.asset
- Assets/TextMesh Pro/Shaders/TMPro.cginc
- Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader
- Assets/TextMesh Pro/Resources/Style Sheets
- Assets/TextMesh Pro/Shaders/TMP_SDF SSD SpaceWarp.shader
- Assets/TextMesh Pro/Shaders/TMP_SDF.shader
- Assets/TextMesh Pro/Fonts
- Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt
- Assets/TextMesh Pro/Resources/Fonts & Materials
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat
- Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset
- Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl
- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader
- Assets/TextMesh Pro/Shaders/TMP_SDF SpaceWarp.shader
- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader
- Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc
- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader
- Assets/TextMesh Pro/Shaders/TMP_Sprite.shader
- Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt
- Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc
- Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader
- Assets/TextMesh Pro/Fonts/LiberationSans.ttf
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat
- Assets/TextMesh Pro/Shaders
- Assets/TextMesh Pro
- Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader
- Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset
- Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt
- Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader

Nonsemantic indentation, trailing ASCII whitespace and final blank lines were normalized in newly staged text resources for Git checks. Font/shader identities, source code, atlas data and the bundled license remain retained. No installed package files were edited.

Four unused optional URP/HDRP Shader Graph variants were omitted after a full Assets GUID-consumer scan found zero references. The directly referenced standard mobile SDF runtime shader and its includes remain; no HDRP package is introduced for this UI.
