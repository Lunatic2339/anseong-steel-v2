# Compact mountain map

Pink-line approximation: foothills startZ1080–1150, just behind the factory rear roadZ1030.15 rounded peaks in two staggered rows; closer peaks nearZ1400–1510, rear peaksZ1840–2000. Terrain rear cropped atZ2400, matching the red-line composition. New terrain X-3200–250,Z-3000–2400; prior rear terrain reachedZ13000. Industrial land, beach and land collision also clipped atZ2400. Ocean retained as non-colliding visual horizon background; combat yard and buildings unchanged. Existing June-green shader/material reused; no additional bitmap textures.

Occlusion verification: 260 rays from both65m robot-eye proxies to rear edge X-3000..225, all blocked by terrain with2m margin. This is a static composition check, not headset validation or an occlusion-culling benchmark. User annotation interpreted from screenshot, not surveyed coordinates.
Meshes clipped=2; land box colliders shortened=2.

Current arrangement revised to10 rear peaks plus5 left-side peaks, forming an L; see LMountainLayout.md for fixed-seed integer spacing, coordinates and updated clearance checks.
