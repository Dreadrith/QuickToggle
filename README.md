# Toggle Mode
Easily create clips that Enable/Disable GameObjects or their components.  

**Quick Steps:**  
1. Set the Root
2. Select the Target GameObjects or Components.
3. Right Click > Quick Actions > Quick Toggle.
4. Set the desired settings.
5. Press "Create Toggle Clip".

# Blendshape Mode
Easily create clips that modify the Blendshapes of skinned mesh renderers.
**Quick Steps:**  
1. Set the Root.
2. Set a clip name.
3. Set the target Skinned Mesh Renderers.
4. Expand the foldout, set the desired blendshape values.
5. Press "Create Blendshape Clip".

# Settings
- Clip Length: The length of the animation clip in frames (60 fps)
- Loop time: Sets loop time to true on the created animation clips
- Ping Folder: Highlights the folder where the new animations clips were created.
- Close Window: Automatically close the window after animation clips creation.
- Create Opposite: Create an opposite clip for each animation clip created.
	- Toggle: Disables Objects/Components previously enabled and vice versa
	- Blendshape: Sets Blendshapes value in the opposite clip to 0.

- (Toggle) Individual Toggles: Creates an Animation clip for each target Object/Component

- (Blendshape) Clip Mode: Chooses the clip creation
	- Single Clip: Puts all set Blendshape values in a single animation clip.
	- Separate Renderers: Makes an animation clip for each target Skinned Mesh Renderer. Can be used for multiple Clips for the same Renderer.
	- Separate Blendshapes: Makes an animation clip for each used Blendshape (Value > 0).

- (Blendshape) Use all Blendshapes: Uses all the Blendshape values in the target Renderers in the created clips.
