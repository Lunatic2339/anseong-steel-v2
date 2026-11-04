V13 HQ Unity test scene
Open BossSteelV13Test.unity and press Play.
Original BossModelTest and previous versions remain unchanged.
HQ51 FBX and matching 8K UV maps are connected to the original 58-bone skeleton and 22 animation clips.
Turn, walk and 7 attack checks passed.
Textures: 8K, uncompressed, mipmaps enabled, trilinear, anisotropy 8. Preview-quality setup, not memory-optimized for release.
URP Lit uses BaseColor, Normal, Metallic/Smoothness, Emission. Authoring WearMask/Height are not needed by this material; source HQ51 package retains them. Blender-specific SurfaceResponse is not mapped directly to URP Lit.
Do not substitute older FBXs: their UV layouts differ.
