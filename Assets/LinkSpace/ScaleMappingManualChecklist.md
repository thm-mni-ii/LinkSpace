# LinkSpace Scale Mapping Manual Checklist

Use two linked Spatial Views and run each check in both travel directions where applicable.

- PreserveScale is the default on both views. Existing demo scenes render, ray teleport, remote grabbing, and object traversal exactly as before.
- Same-size SpatialViews in PreserveScale preserve object size, player size, linear velocity magnitude, and angular velocity magnitude.
- Same-size SpatialViews in ScaleByPortalSize behave like PreserveScale because the mapping factor is 1.
- Large-to-small SpatialViews in ScaleByPortalSize map points closer together on the target side, shrink non-player travellers when Scale Traveller Objects is enabled, scale linear velocity by the SpatialView-size ratio, and only rotate angular velocity.
- Small-to-large SpatialViews in ScaleByPortalSize map points farther apart on the target side, enlarge non-player travellers when Scale Traveller Objects is enabled, scale linear velocity by the SpatialView-size ratio, and only rotate angular velocity.
- CustomScaleFactor maps point offsets and linear velocity by the configured factor. Traveller scale follows only when Scale Traveller Objects is enabled.
- Ray teleportation through a scaled Spatial View still lands on valid teleport targets and keeps the redirected ray direction normalized.
- Near/far interaction through a scaled Spatial View still selects remote interactables and places the attach point at the corresponding source-side location.
- Grabbed object transfer through raycast mode preserves or updates object scale according to the selected mode and applies linear/angular velocities consistently after release.
- Grabbed object transfer through traversation mode follows the same position, rotation, object scale, and velocity rules after release.
- Non-uniform SpatialView scale in ScaleByPortalSize logs a warning and uses one uniform SpatialView-size factor. Confirm the behavior is acceptable or switch to PreserveScale/CustomScaleFactor.
- Slicing and graphics clones remain visual embodiments of the planar Spatial View crossing; they do not imply true volumetric crossing.
