# LinkSpace

LinkSpace is a Unity XR prototype for linked SpatialViews. A SpatialView is a
rendered, walkable view surface that can be linked to another SpatialView so
players, rays, grabbed objects, and moving rigidbodies can be mapped between
the two spaces.

The current project terminology is SpatialView-based. User-facing setup and
scene objects should be named SpatialView, SpatialView Screen,
SpatialTraveller, and LinkSpace.

## Current Setup

This repository is currently configured for:

- Unity `6000.4.7f1`
- Universal Render Pipeline `17.4.0`
- Input System `1.19.0`
- XR Interaction Toolkit `3.4.1`
- XR Management `4.5.4`
- OpenXR `1.16.1`
- Meta Quest style controller bindings in the demo scene

Open the project folder directly in Unity Hub. The main demo scene is:

`Assets/LinkSpace/Demo/SampleScene.unity`

Useful prefabs:

- `Assets/LinkSpace/Demo/Prefabs/Spatial View.prefab`
- `Assets/LinkSpace/Demo/Prefabs/Grab Interactable.prefab`
- `Assets/LinkSpace/Demo/XR Origin LinkSpace Demo (XR Rig).prefab`
- `Assets/LinkSpace/Prefabs/XR Origin LinkSpace (XR Rig).prefab`

## Setup Check

After opening or importing LinkSpace into another project, check these items:

1. Use Unity `6000.4.7f1` or a compatible Unity 6 version.
2. Use the Universal 3D/URP project setup.
3. Install/enable XR Plugin Management and OpenXR.
4. In `Project Settings -> XR Plug-in Management -> Project Validation`, run
   `Fix All` where appropriate.
5. Ensure the OpenXR interaction profiles for your target headset/controllers
   are enabled. The demo is configured around Quest-style controllers.
6. Install XR Interaction Toolkit and import the Starter Assets if you are
   building a fresh project.
7. Run `Tools -> LinkSpace -> Run Layer Setup`.
8. Verify `Project Settings -> Tags and Layers` contains `SpatialViewScreen`.
9. Verify `Project Settings -> Physics` has collisions disabled for the
   `SpatialViewScreen` layer. The visible SpatialView surface is queried by
   raycasts and movement segments; it should not physically block travellers.

The repository already contains the expected packages, layer name, and OpenXR
settings. The setup check matters most when copying LinkSpace into a new Unity
project or after Unity rewrites project settings.

## Core Concepts

`SpatialView`
: Main component for a linked view. It renders what the linked SpatialView sees,
tracks travellers near the view, maps positions/rotations/directions/velocities,
and performs traversal.

`SpatialViewScreen`
: Child object representing the visible screen surface. Its collider lives on
the `SpatialViewScreen` layer. Screen crossing and far interaction are based on
this surface, not on the broad root trigger.

`SpatialTraveller`
: Base component for objects that can be represented and transferred through a
SpatialView. It manages graphics clones and slicing visuals.

`PlayerSpatialTraveller`
: Traveller used by the XR Origin/player. It maps the player by the teleport
origin/camera setup rather than by a simple object center.

`GrabSpatialTraveller`
: Traveller used by grabbable rigidbody objects, such as the demo cube. It
supports XR grabbing, visible screen crossing while grabbed, ungrabbed
rigidbody transfer, velocity mapping, and visual clone handling.

`LinkSpaceManager`
: Runtime helper for creating, destroying, moving, rotating, linking, and
unlinking SpatialViews.

`LinkSpaceRayInteractor`
: XR ray interactor extension for teleport rays through SpatialViews.

`LinkSpaceNearFarInteractor`
: Near/far interactor extension for selecting interactables through
SpatialViews. While holding a `GrabSpatialTraveller`, the curved far ray visual
is hidden to avoid misleading rays after object transfer.

## SpatialView Setup

The easiest starting point is `Spatial View.prefab`.

For a custom SpatialView:

1. Add a GameObject that defines the view plane.
2. Add the `SpatialView` component.
3. Either enable `Use Mesh As Screen` and assign
   `Assets/LinkSpace/Materials/Spatial View Screen.mat`, or assign an existing
   child `SpatialViewScreen`.
4. Make sure the visible screen is thin and lies on the intended local surface.
5. Link two SpatialViews through `Linked View`, or call
   `LinkSpaceManager.LinkSpatialViews(viewA, viewB)`.

Important orientation rule: traversal is based on the SpatialView's local
Z-axis. The flat visible surface should be perpendicular to local Z, and the
screen bounds should match the visible area.

## Collider Model

There are two different collider roles:

- The root `BoxCollider` on `SpatialView` is a trigger used as a small tracking
  volume for nearby travellers, visuals, and player proximity. In the demo
  prefab it is intentionally shallow on Z.
- The child `Screen` collider on `SpatialViewScreen` represents the actual
  visible crossing surface. Object transfer checks the movement segment against
  this screen surface.

Do not make the root trigger a huge volume to "catch" objects. It can cause
confusing late tracking. Keep it close to the SpatialView. Do not remove it
unless the tracking code is also replaced, because `SpatialView` still uses it
for threshold tracking.

## Object Travel

`SpatialView.travelMode` controls object behavior:

- `None`: no object traversal.
- `Traversation`: objects transfer when they cross the visible screen.
- `Raycast`: grabbed objects can be prepared by a grab ray, and moving objects
  still transfer by visible screen crossing.

The current object-transfer path is designed around the actual screen crossing:

- Grabbed cubes start a live transfer when their held pose crosses the
  `SpatialViewScreen` surface.
- Ungrabbed rigidbodies check their previous and current physics positions in
  `FixedUpdate`, so fast objects transfer when their movement segment crosses
  the visible screen, not later at the end of the root trigger.
- Linear and angular velocity are mapped to the linked SpatialView.
- Screen collisions are ignored for `GrabSpatialTraveller` objects so the screen
  can be crossed instead of acting as a physical wall.
- Visual clones and slicing are only visual aids; the object should have one
  authoritative physical position.

For grabbable objects, use `Grab Interactable.prefab` or add:

1. `Rigidbody`
2. `XRGrabInteractable`
3. `GrabSpatialTraveller`
4. A collider on the physical object
5. `Graphics Object` assigned to the visible mesh hierarchy

## Mapping Scale

SpatialViews can preserve world scale or map between differently sized views.
Relevant `SpatialView` settings:

- `Mapping Scale Mode`
- `Custom Mapping Scale`
- `Scale Traveller Objects`

Manual checks for scale behavior are documented in:

`Assets/LinkSpace/ScaleMappingManualChecklist.md`

## Recursion Rendering

Each SpatialView can render linked views recursively. Important settings:

- `Max Recursion Depth`
- `Recursion Texture Scale`
- `Near Clip Offset`
- `Near Clip Limit`

Higher recursion depth improves the repeated SpatialView-in-SpatialView effect
but increases render cost. When testing two facing SpatialViews, use the depth
high enough to verify recursion, then reduce it if performance becomes a
problem on device.

## Demo Controls

The demo input map is generated from:

`Assets/LinkSpace/Demo/Input/ExampleInputActions.inputactions`

Movement and view:

| Input | Action |
| --- | --- |
| Left joystick | Move |
| Right joystick left/right | Snap turn |
| Right joystick forward | Ray teleport |

SpatialView interaction:

| Input | Action |
| --- | --- |
| A button | Place SpatialView |
| B button | Destroy aimed SpatialView |
| Y button | Toggle placement alignment |
| Right trigger | Select/link two SpatialViews |
| Right grip | Grab and move a SpatialView |

Object interaction:

| Input | Action |
| --- | --- |
| X button | Spawn demo cube |
| Trigger/select on grabbable object | Grab object |

## Editor Utilities

- `Tools -> LinkSpace -> Run Layer Setup`
- `Tools -> LinkSpace Manager Debugger`
- Custom inspector for `SpatialView`

## Troubleshooting

- If rays or object transfer do not hit SpatialViews, check that the screen
  object is on `SpatialViewScreen`.
- If objects bounce off the screen, check the collision matrix and rerun
  `Tools -> LinkSpace -> Run Layer Setup`.
- If transfer happens late, inspect the root `SpatialView` trigger depth. It
  should be a small tracking volume, not the source of truth for traversal.
- If recursive rendering is blank or stale, confirm both SpatialViews are linked
  and the screen renderer uses the `Spatial View Screen` material.
- If a grabbed cube feels inconsistent after transfer, verify it uses
  `GrabSpatialTraveller` and the current XR rig prefab with
  `LinkSpaceNearFarInteractor`.
