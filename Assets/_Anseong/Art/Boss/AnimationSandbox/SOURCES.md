# Final boss animation sandbox sources

- Body: team V13 HQ51 model/atlas layout from the old `anseong-steel/AnseongSteel/Assets/_BossSteelV13/`.
- Textures: final `SteelV13_BaseColor`, `SteelV13_Normal`, `SteelV13_MetallicSmoothness`, `SteelV13_Emission` PNG files, copied byte-for-byte from that directory. URP Lit, 8K preview imports with mipmaps. This sandbox is a high-quality inspection scene, not the production VR memory budget.
- Hands: revised V19-based hand skeleton/weights from the preceding hand-rig work, connected to the preserved canonical body skeleton. Previous final finger poses are converted into the revised local joint axes.
- Hand UV: projected from the matching HQ51 surface to the unchanged hand geometry. 37,678 triangles; maximum source-surface distance 0.00000931 m in the working model. UV seam splitting preserves positions, weights and normals. Tangents recomputed for the final normal map.
- Motions: 22 existing team clips from `_BossPrototype/Generated/BossAnimator.controller`, originally Mixamo/body clips plus prior project corrections. Imported as compact 30 fps local-transform samples with finger, sword-mount, support-hand and blade-roll corrections. No whole-body Humanoid retargeting.
- Draw/sheath: original contact fractions (0.36 / 0.46) and 0.14-second transfer settling retained in the sampled weapon path.
- Source reflection environment: team `SteelReflection.asset`.
- Shared immutable body/sword mesh dependencies remain in `Art/Boss/HandRigComparison/`; retain that folder when transferring this sandbox.
- Existing runtime combat code was referenced for animation appearance, not copied into the new project's combat stack. No new external assets were downloaded.
- Usage and validation: `docs/members/유진/최종보스_애니메이션_테스트.md`.
