Support: Discord (https://discord.gg/K88zmyuZFD)

================

Online Documentation: https://inabstudios.gitbook.io/interactive-dissolve-effects/
You can find more specific information on the asset functionality and setup process.

================

### BEFORE INSTALLING

- Make sure that VFX graph is installed.
- In Edit->Preferences->Visual Effects, "Experimental Operators/Blocks" option needs to be enabled.
- If you want to be able to control demo scenes, your project' input system needs to be set to either: "Input Manager (Old)" or "Both".

### IMPORTING

After downloading the asset, import the appropriate .unitypackage file based on your Unity version and SRP (URP.unitypackage, or HDRP.unitypackage).
If effects do not seem to work, go to Edit->VFX->Rebuild And Save All Vfx Graphs.

### EFFECT SETUP

!!! Every mesh / skinned mesh needs to have the "read/write" option in the mesh importer inspector enabled.

1) Mesh's materials need to use the Interactive Effect Shader.
2) Add a child GameObject with the Interactive Effect script to the mesh.
3) Follow the instructions provided by the script.
4) Move around and scale the Mask, and change its type if necessary to see how it works.
5) Modify material settings and VFX graph settings to adjust the effect's appearance.
6) Hit the Update and Setup button.
7) Adjust interactive settings with the mask transform to create the desired effect.
8) Call the PlayEffect() function on the interactive effect script.

For a more in-depth knowledge (C# API etc.) and video guidance, refer to the documentation.

### OTHER

- Look at the "Tutorials" scene. It is a good way to learn about the asset and how it works.
- MSAA is not supported when using "smooth" interactive effect shader with dithering.
- When using the asset with HDRP, use the "Emission Power" to adjust the emission strength of the particle effects.


