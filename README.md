BoxClip is a set of Unity editor scripts and shader includes to allow for the creation of complex non-euclidean rasterized geometry.

This was used to create the "impossible space" Selphina booth in the Vket4 Mirabilis Flor world.

No stencils are used! This technique has minimal overdraw.

Description here: https://booth.pm/en/items/2033623

Tried it? Having trouble? Please say hello to Lyuma#0781 or xnlyuma@gmail.com - I love to see any questions or feedback you may have!

## Features

- Clipping is done both in Geometry and Fragment stages for high efficiency even in large scenes.
- All content can be drawn in one pass, and no render queue manipulation is needed.
- Support showing and hiding rects.
- Support "ZCompress" - allows geometry to be compressed onto a plane. (Note: ZCompress does not handle borders well. Please leave extra room.)
- Real-time editing using MaterialPropertyBlock (Udon support is planned)
- Baking an efficient shader for upload.
- Supports "ShowVolume" and "HideVolume" to reveal things within a volume rather than "through a window"
- Supports "ShowCameraWithin" and "HideCameraWithin" to reveal the whole object when your camera is inside this volume.

## How to use

- Import an object.
- Create a new empty game object. Add a BoxClipOrigin component. Make another empty object, set this as the Origin.
- Create a new folder and set the folder as the Generated asset dir.
- Insert all objects to the "List of Renderers"
- Click Preview.
- Find the Show and Hide child objects, and create a new empty game object inside. These boxes will allow your object to be seen, or to be obscured.
- Move, rotate and scale empty game objects within Show and Hide to control clipping regions.
- You must click Bake To Shader when done editing, in order to upload to VRChat.

## Update notes

### v0.3.3:
- Workaround issues in locales which use commas.

### v0.3.1 and v0.3.2:
- Performance fixes related to geometry culling.

### v0.3:
- Added ShowVolume and HideVolume to reveal and hide what is contained in a 3d Volume.
- Added ShowCameraWithin and HideCameraWithin to reveal the object.

### v0.2:
- Fix Bake To Shader getting mixed up on upload when multiple BoxClipOrigin scripts are present.
- Add checkbox "Scale Clip Planes". Use this with a "Scale" setting of 1000, and with mesh transform scale set to 0.001. This will make large models invisible when avatar shaders are blocked.

