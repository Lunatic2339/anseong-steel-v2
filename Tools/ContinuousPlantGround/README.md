# Road-based continuous plant paving

Requires Python with Shapely2.1+. ExportContinuousGroundObstacles reads active factory prefabs only to prevent greenery/building intersections; building boxes never define material colours. Run build_continuous_plant_ground.py in this folder, then ApplyContinuousPlantGround on a backed-up scene without Ground_ContinuousRoadBasedDesign. Inputs reuse Tools/SurfaceZones and Tools/CentrelineRoads. CompactContinuousGroundMesh welds duplicate vertices without changing polygons or material slots.

Reference: user-supplied aerial photograph and official POSCO overview at https://posco.co.kr/homepage/docs/kor7/jsp/common/posco/s91a1000012c.jsp . Photographs are reference only; no photo pixels bundled. Six road-delimited factory parcels; green and walking strips follow curves, with road/building/combat-core clearance checks. Existing paving texture and shader reused; no extra bitmap textures.
