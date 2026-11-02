# Road cleanup source
Generated Unity mesh is already saved in Assets/_Anseong/Art/Environment/UnifiedSmoothRoads.
Rebuild in a backup scene: Unity menu Anseong/Art/Export Road Geometry, then run this folder's clean_road_union.py with Python and Shapely 2.1+ installed. Finally run Anseong/Art/Apply Unified Smooth Roads. Existing applied scene refuses duplicate application; restore the prior road roots first or use the pre-cleanup scene revision.
No third-party meshes or textures are included. Shapely is only an offline geometry tool, not a Unity dependency.
