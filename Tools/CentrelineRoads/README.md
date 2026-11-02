# Centre-line road generation
The active scene uses Roads_CentrelineRebuilt, saved in Assets/_Anseong/Art/Environment/CentrelineRoads.
Source: rebuild_road_centerlines.py, requires Python and Shapely2.1+. Run from this folder, then Unity menu Anseong/Art/Rebuild Roads From Centre Lines on a backup of the prior scene. Duplicate application is guarded. The previous patchwork/union roads are inactive for recovery.
32 trucks are projected back onto the new lane paths; building transforms are unchanged. Width checks, polygon connectivity, obstacle intersection and triangulation checks are recorded in VerificationReport.md.
