# Hand rig comparison sources

- PreviousApproved: team-owned V13 visual mesh, skeleton, greatsword and three sword motions from the old `anseong-steel/AnseongSteel/Assets/_BossSteelV13/BossSteelV13.prefab` and `_BossPrototype` assets.
- MatchedHandRig: previous body motion and weapon placement, with the V19 hand rig from the prior hand-rig work. Finger rotations are mapped to the revised joint axes; no whole-body Humanoid retargeting.
- Sword mount, guard wrist offset, support-hand correction and blade-roll motion were sampled from the previous team's implementation. Old runtime code was not copied into this repository.
- Clips are compact 24 fps art-preview samples for SwordIdle, SwordSlash and OverheadSmash only. They are not a replacement for the production combat controller.
- The materials are neutral inspection materials. The menu uses URP Lit in the new project.
- Original animation provenance: existing team Mixamo downloads; no new external asset download in this task.
- Full instructions: `docs/members/유진/손리깅_검방향_비교.md`.
