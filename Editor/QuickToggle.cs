using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

//Made by Dreadrith#3238
//Server: https://discord.gg/ZsPfrGn
//Github: https://github.com/Dreadrith/DreadScripts
//Gumroad: https://gumroad.com/dreadrith

namespace DreadScripts
{
    public class QuickToggle : EditorWindow
    {
        #region VRC

#if VRC_SDK_VRCSDK3
        private static VRCAvatarDescriptor avatar;
        private static VRCExpressionsMenu menu;
        private static VRCToggleFlags vrcAddFlags = VRCToggleFlags.All;
        private static string _parameter = "Toggle";

        private static string parameter
        {
            get => _parameter;
            set
            {
                if (_parameter == value) return;
                _parameter = value;
                RefreshUniqueParameter();
            }
        }

        private static bool uniqueParameter = true;
        private static bool useWriteDefaults = false;
        private enum VRCToggleFlags
        {
            None = 0,
            FX = 1 << 0,
            Menu = 1 << 1,
            Parameters = 1 << 2,
            All = ~0
        }
#endif

        #endregion

        #region Private Variables

        private static bool init;
        private static bool clipValid = false;
        private static string folderPath;

        private static UnityEditorInternal.ReorderableList targetList;
        private static Vector2 scroll;

        public static int toolbarIndex;
        private static readonly string[] toolbarOptions = {"Toggle", "Blendshape", "Settings"};
        private const string PREF_KEY = "QuickToggleDataKey";

        private enum BlendClipMode
        {
            SingleClip,
            ClipPerRenderer,
            ClipPerBlendshape
        }

        #endregion

        #region Input

        public static GameObject _root;

        public static GameObject root
        {
            get => _root;
            set
            {
                if (_root == value) return;
                _root = value;
                OnRootChanged();
            }
        }

        public static List<ToggleObject> targets = new List<ToggleObject>();

        private static List<SkinnedShapeSlot> skinnedRenderers = new List<SkinnedShapeSlot>() {new SkinnedShapeSlot()};

        public static string clipName;

        #endregion

        #region Settings

        [SerializeField] private int frameSpan = 1;
        [SerializeField] private bool loopTime;
        [SerializeField] private bool pingFolder = true;
        [SerializeField] private bool autoClose = true;
        [SerializeField] private string savePath = "Assets/DreadScripts/Quick Actions/Quick Toggle/Generated Clips";

        private static bool autoName = true;
        private static bool individualToggle;
        private static bool createOpposite = true;
        private static bool useAllShapes;
        private static BlendClipMode shapeClipMode = BlendClipMode.SingleClip;

        #endregion

        [MenuItem("DreadTools/Quick Toggle", false, 840)]
        [MenuItem("GameObject/Quick Toggle", false, -10)]
        public static void ShowWindow()
        {
            GetWindow();
            GameObject[] targetObjs = Selection.GetFiltered<GameObject>(SelectionMode.Editable);
            targets = targetObjs.Select(t => new ToggleObject(t)).ToList();
            skinnedRenderers = targetObjs.Select(t => new SkinnedShapeSlot(t.GetComponent<SkinnedMeshRenderer>())).Where(s => s.renderer).ToList();

            GameObject[] selectedObj = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel);
            if (!root && selectedObj.Length > 0)
                root = selectedObj[0].transform.root.gameObject;

#if VRC_SDK_VRCSDK3
            if (!root) root = FindObjectOfType<VRCAvatarDescriptor>()?.gameObject;
#endif

            if (autoName)
            {
                clipName = string.Empty;
                switch (targetObjs.Length)
                {
                    case 0:
                        clipName = "Objects";
                        break;
                    case 1:
                        clipName = targetObjs[0].name;

#if VRC_SDK_VRCSDK3
                        parameter = clipName;
#endif

                        break;

                    default:
                    {
                        for (int i = 0; i < targetObjs.Length; i++)
                        {
                            int letterCount = Mathf.Clamp(7 - targetObjs.Length, 2, 5);
                            clipName += targetObjs[i].name.Substring(0, Mathf.Clamp(letterCount, 1, targetObjs[i].name.Length));
                            if (i != targetObjs.Length - 1)
                                clipName += "-";
                        }

#if VRC_SDK_VRCSDK3
                        parameter = clipName;
#endif

                        break;
                    }
                }

                clipName += " Enable";
            }

            CheckIfValid();
            init = false;

        }

        private static QuickToggle GetWindow() => GetWindow<QuickToggle>(false, "Quick Toggle", true);

        #region Main GUI Methods

        private void OnGUI()
        {
            if (!init)
                RefreshList();

            toolbarIndex = GUILayout.Toolbar(toolbarIndex, toolbarOptions, EditorStyles.toolbarButton);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            switch (toolbarIndex)
            {
                case 0:
                case 1:
                    DrawCreatorGUI();
                    break;
                case 2:
                    DrawSettingsGUI();
                    break;
            }


            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made by Dreadrith#3238", "boldlabel"))
                    Application.OpenURL("https://github.com/Dreadrith/DreadScripts");
            }

            EditorGUILayout.EndScrollView();

            if (!init)
            {
                GUI.FocusControl("CreateClip");
                init = true;
            }

            if (GUI.GetNameOfFocusedControl() == "CreateClip" && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                StartCreateClip(false);
            }
        }

        private void DrawCreatorGUI()
        {
            bool creatingToggle = toolbarIndex == 0;

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {

#if VRC_SDK_VRCSDK3
                bool showAvatarField = avatar && creatingToggle;
                EditorGUI.BeginChangeCheck();
                Object tempObject = EditorGUILayout.ObjectField(showAvatarField ? "Avatar" : "Root", showAvatarField ? (Object) avatar : (Object) root, showAvatarField ? typeof(VRCAvatarDescriptor) : typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    avatar = tempObject as VRCAvatarDescriptor;
                    if (avatar) root = avatar.gameObject;
                    else root = (GameObject) tempObject;
                }
#else
                root = (GameObject)EditorGUILayout.ObjectField( "Root", root, typeof(GameObject), true);
#endif
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (creatingToggle) targetList.DoLayoutList();
                else
                {

                    for (int i = 0; i < skinnedRenderers.Count; i++)
                        skinnedRenderers[i].Draw();

                    if (GUILayout.Button("Add Skinned Renderer")) skinnedRenderers.Add(new SkinnedShapeSlot());


                }
            }

            EditorGUILayout.Space();

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {


                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope())
                    {
                        DoToggle(ref createOpposite, Styles.createOppositeContent);
                        using (new GUILayout.VerticalScope()) clipName = EditorGUILayout.TextField("Clip Name", clipName);
                        using (new EditorGUI.DisabledScope(creatingToggle))
                            shapeClipMode = (BlendClipMode) EditorGUILayout.EnumPopup("Clip Mode", shapeClipMode);
                    }

                    using (new GUILayout.VerticalScope())
                    {
                        using (new EditorGUI.DisabledScope(!creatingToggle))
                        {
                            DoToggle(ref individualToggle, Styles.individualToggleContent);
                            autoName = EditorGUILayout.Toggle(new GUIContent("AutoName", "Automatically generate a clip name when using context menu button"), autoName);
                        }
                        using (new EditorGUI.DisabledScope(creatingToggle || shapeClipMode == BlendClipMode.ClipPerBlendshape))
                            DoToggle(ref useAllShapes, Styles.useAllShapesContent);

                    }
                }

            }

            EditorGUILayout.Space();

#if VRC_SDK_VRCSDK3
            if (avatar && creatingToggle)
            {
                using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    using (new GUILayout.VerticalScope())
                    {
                        using (new EditorGUI.DisabledScope(!creatingToggle || !avatar))
                            vrcAddFlags = (VRCToggleFlags) EditorGUILayout.EnumFlagsField("Add To", vrcAddFlags);

                        using (new EditorGUI.DisabledScope(!vrcAddFlags.HasFlag(VRCToggleFlags.Menu)))
                            menu = (VRCExpressionsMenu) EditorGUILayout.ObjectField("Target Menu", menu, typeof(VRCExpressionsMenu), false);
                    }

                    using (new GUILayout.VerticalScope())
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(!creatingToggle || !avatar || vrcAddFlags == VRCToggleFlags.None))
                                parameter = EditorGUILayout.TextField("Parameter", parameter);

                            if (!avatar) GUILayout.Label(Styles.noteIcon, GUILayout.Width(18));


                            var og = GUI.backgroundColor;
                            GUI.backgroundColor = uniqueParameter ? Color.green : Color.grey;

                            EditorGUI.BeginChangeCheck();
                            using (new EditorGUI.DisabledScope(vrcAddFlags == VRCToggleFlags.None))
                                uniqueParameter = GUILayout.Toggle(uniqueParameter, "Unique", GUI.skin.button, GUILayout.ExpandWidth(false));
                            if (EditorGUI.EndChangeCheck()) RefreshUniqueParameter();

                            GUI.backgroundColor = og;
                        }

                        useWriteDefaults = EditorGUILayout.Toggle("Write Defaults", useWriteDefaults);

                    }
                }

                EditorGUILayout.Space();
            }
#endif




            GUI.SetNextControlName("CreateClip");

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(clipName)))
            {
                if (creatingToggle)
                {
                    using (new EditorGUI.DisabledScope(!clipValid))
                        if (GUILayout.Button("Create Toggle Clip", EditorStyles.toolbarButton))
                            StartCreateClip(false);
                }
                else
                    using (new EditorGUI.DisabledScope(!skinnedRenderers.Any(sr => sr.renderer)))
                        if (GUILayout.Button("Create Blendshape Clip", EditorStyles.toolbarButton))
                            StartCreateClip(true);
            }

        }

        private void DrawSettingsGUI()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.VerticalScope())
                {
                    frameSpan = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Clip Length", "The length of the animation clip in frames (60 fps)"), frameSpan));
                    autoClose = EditorGUILayout.Toggle(new GUIContent("Close Window", "Close window upon clip creation."), autoClose);
                }

                using (new GUILayout.VerticalScope())
                {
                    loopTime = EditorGUILayout.Toggle(new GUIContent("Loop Time", "Sets loop time to true on the created clips."), loopTime);
                    pingFolder = EditorGUILayout.Toggle(new GUIContent("Ping Folder", "Automatically highlights the folder where the clips were generated"), pingFolder);
                }
            }

            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            savePath = DSHelper.AssetFolderPath(savePath, "Generated Assets Path");

        }

        #endregion

        #region Main Methods

        public void StartCreateClip(bool isBlendshapes)
        {
#if VRC_SDK_VRCSDK3
            if (!isBlendshapes)
            {
                RefreshUniqueParameter();

                void VRCWarningCheck(string msg, bool condition)
                {
                    if (!condition) return;
                    EditorUtility.DisplayDialog("Warning", msg, "Ok");
                    throw new Exception(msg);
                }

                VRCWarningCheck("FX Controller not set in Avatar Descriptor.", vrcAddFlags.HasFlag(VRCToggleFlags.FX) && avatar.GetPlayableLayer(VRCAvatarDescriptor.AnimLayerType.FX) == null);

                int addCount = individualToggle ? targets.Count(o => o.valid) : 1;

                if (vrcAddFlags.HasFlag(VRCToggleFlags.Parameters))
                {
                    VRCWarningCheck("Expression Parameters not set in Avatar Descriptor", vrcAddFlags.HasFlag(VRCToggleFlags.Parameters) && avatar.expressionParameters == null);

                    if (avatar.expressionParameters.parameters == null)
                    {
                        avatar.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                        EditorUtility.SetDirty(avatar.expressionParameters);
                    }

                    VRCWarningCheck($"Expression Parameters requires {addCount}/256 free memory.", avatar.expressionParameters.CalcTotalCost() + addCount > 256);
                }

                if (vrcAddFlags.HasFlag(VRCToggleFlags.Menu))
                {
                    VRCWarningCheck("No Target Menu set.", vrcAddFlags.HasFlag(VRCToggleFlags.Menu) && menu == null);
                    VRCWarningCheck("Cannot add more than 8 toggles to an expression menu!", addCount > 8);
                    VRCWarningCheck($"Target Menu requires {addCount}/8 free control slots.", menu.controls.Count + addCount > 8);
                }



            }
#endif
            DSHelper.ReadyPath(savePath);
            folderPath = savePath + "/" + root.name;
            DSHelper.ReadyPath(folderPath);

            if (isBlendshapes)
                CreateBlendshapeClips();
            else CreateToggleClips();

            if (autoClose) Close();
        }

        private void CreateToggleClips()
        {
            AnimationClip currentClip = new AnimationClip();

            foreach (ToggleObject obj in targets.Where(o => o != null && o.valid))
            {
                string path = AnimationUtility.CalculateTransformPath(obj.gameObject.transform, root.transform);

                System.Type myType = obj.GetActive().GetType();

                currentClip.SetCurve(path, myType, myType == typeof(GameObject) ? "m_IsActive" : "m_Enabled", new AnimationCurve {keys = new Keyframe[] {new Keyframe {time = 0, value = obj.active ? 1 : 0}, new Keyframe {time = frameSpan / 60f, value = obj.active ? 1 : 0}}});

                if (individualToggle)
                {
                    SaveClip(currentClip, $" {obj.gameObject.name}", false);
                    RefreshUniqueParameter();
                    currentClip = new AnimationClip();
                }
            }

            if (!individualToggle)
                SaveClip(currentClip, string.Empty, false);

            if (pingFolder)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(folderPath));
        }

        private void CreateBlendshapeClips()
        {
            AnimationClip GetNewClip()
            {
                AnimationClip newClip = new AnimationClip();
                return newClip;
            }


            AnimationClip currentClip = null;
            for (int i = 0; i < skinnedRenderers.Count; i++)
            {
                if (!skinnedRenderers[i].renderer)
                    continue;
                string renderPath = AnimationUtility.CalculateTransformPath(skinnedRenderers[i].renderer.transform, root.transform);
                if ((shapeClipMode == BlendClipMode.ClipPerRenderer) || i == 0)
                    currentClip = GetNewClip();
                bool anyShapeUsed = false;
                for (int j = 0; j < skinnedRenderers[i].shapes.Count; j++)
                {
                    SkinnedShape shape = skinnedRenderers[i].shapes[j];
                    if (shape.value > 0 || (useAllShapes && shapeClipMode != BlendClipMode.ClipPerBlendshape))
                    {
                        anyShapeUsed = true;
                        currentClip.SetCurve(renderPath, typeof(SkinnedMeshRenderer), "blendShape." + shape.name, new AnimationCurve() {keys = new Keyframe[] {new Keyframe(0, shape.value), new Keyframe(frameSpan / 60f, shape.value)}});
                        if (shapeClipMode == BlendClipMode.ClipPerBlendshape)
                        {
                            SaveClip(currentClip, " " + shape.name, true);
                            currentClip = GetNewClip();
                        }
                    }
                }

                if (shapeClipMode == BlendClipMode.ClipPerRenderer && anyShapeUsed)
                {
                    SaveClip(currentClip, " " + skinnedRenderers[i].renderer.gameObject.name, true);
                    currentClip = GetNewClip();
                }

            }

            if (shapeClipMode == BlendClipMode.SingleClip)
                SaveClip(currentClip, string.Empty, true);
            if (pingFolder)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(folderPath));
        }

        private AnimationClip CreateOppositeClip(AnimationClip c, string clipPath, bool isBlendshapeClip)
        {
            AnimationClip newClip = new AnimationClip();
            EditorUtility.CopySerialized(c, newClip);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(newClip);
            foreach (var b in bindings)
            {
                int myValue = isBlendshapeClip || AnimationUtility.GetEditorCurve(c, b)[0].value != 0 ? 0 : 1;
                newClip.SetCurve(b.path, b.type, b.propertyName, new AnimationCurve() {keys = new Keyframe[] {new Keyframe(0, myValue), new Keyframe(frameSpan / 60f, myValue)}});
            }

            string oppPath = AssetDatabase.GenerateUniqueAssetPath(clipPath.Substring(0, clipPath.Length - 5) + " Opp.anim");
            SaveClip(newClip, oppPath);
            return newClip;
        }

        private void SaveClip(AnimationClip c, string suffix, bool isBlendShapeClip)
        {
            AnimationClip onClip = c, offClip = null;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{clipName}{suffix}.anim");
            SaveClip(c, path);

            if (createOpposite) offClip = CreateOppositeClip(c, path, isBlendShapeClip);

#if VRC_SDK_VRCSDK3
            DoVRCToggle(onClip, offClip);
#endif
        }

        private void SaveClip(AnimationClip c, string path)
        {
            AssetDatabase.CreateAsset(c, path);
            Debug.Log("<color=green>[Quick Toggle]</color> " + System.IO.Path.GetFileNameWithoutExtension(path) + " Created.");
            EnableLoopTime(c);
        }

        private void EnableLoopTime(AnimationClip c)
        {
            if (!loopTime) return;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(c);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(c, settings);
        }

#if VRC_SDK_VRCSDK3
        private void DoVRCToggle(AnimationClip onClip, AnimationClip offClip)
        {
            if (vrcAddFlags == VRCToggleFlags.None) return;
            if (vrcAddFlags.HasFlag(VRCToggleFlags.FX))
            {
                AnimatorController c = avatar.GetPlayableLayer(VRCAvatarDescriptor.AnimLayerType.FX);
                if (!c) throw new NullReferenceException("Unexpected Error! FX Controller not found!");
                else
                {
                    c.AddParameter(parameter, AnimatorControllerParameterType.Bool);

                    AnimatorControllerLayer newLayer = c.AddLayer(parameter, 1);
                    AnimationClip buffer = ReadyBuffer();
                    AnimatorStateMachine m = newLayer.stateMachine;

                    m.exitPosition = Vector3.zero;
                    m.anyStatePosition = new Vector3(0, 40);
                    m.entryPosition = new Vector3(0, 80);

                    AnimatorState idleState = m.AddState("Idle", new Vector3(-20, 140));
                    AnimatorState onState = m.AddState($"{parameter} On", new Vector3(-140, 240));
                    AnimatorState offState = m.AddState($"{parameter} Off", new Vector3(100, 240));
                    idleState.writeDefaultValues = onState.writeDefaultValues = offState.writeDefaultValues = useWriteDefaults;

                    idleState.writeDefaultValues = onState.writeDefaultValues = offState.writeDefaultValues;

                    idleState.motion = buffer;
                    onState.motion = onClip;
                    offState.motion = offClip;

                    AnimatorStateTransition DoTransition(AnimatorState source, AnimatorState destination, bool state)
                    {
                        var t = source.AddTransition(destination);
                        t.hasExitTime = false;
                        t.duration = 0;
                        t.AddCondition(state ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
                        return t;
                    }

                    DoTransition(idleState, onState, true).offset = 1;
                    DoTransition(idleState, offState, false).offset = 1;

                    DoTransition(offState, onState, true);
                    DoTransition(onState, offState, false);
                }

            }

            if (vrcAddFlags.HasFlag(VRCToggleFlags.Parameters))
            {
                var so = new SerializedObject(avatar.expressionParameters);
                var prop = so.FindProperty("parameters");

                prop.arraySize++;
                var elem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                elem.FindPropertyRelative("name").stringValue = parameter;
                elem.FindPropertyRelative("valueType").enumValueIndex = 2;
                elem.FindPropertyRelative("saved").boolValue = true;
                elem.FindPropertyRelative("defaultValue").floatValue = 0;

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (vrcAddFlags.HasFlag(VRCToggleFlags.Menu))
            {
                if (menu.controls == null) 
                    menu.controls = new List<VRCExpressionsMenu.Control>();

                var newControl = new VRCExpressionsMenu.Control
                {
                    name = parameter,
                    parameter = new VRCExpressionsMenu.Control.Parameter() { name = parameter },
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    value = 1
                };
                menu.controls.Add(newControl);
                EditorUtility.SetDirty(menu);
            }
        }

        private AnimationClip ReadyBuffer()
        {
            string bufferPath = $"{savePath}/1 Frame Buffer.anim";
            AnimationClip buffer = AssetDatabase.LoadAssetAtPath<AnimationClip>(bufferPath);
            if (buffer) return buffer;
            buffer = new AnimationClip();
            buffer.SetCurve("_Buffer", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1 / 60f, 0)));
            AssetDatabase.CreateAsset(buffer, bufferPath);
            return buffer;
        }
#endif

        private static void GreenLog(string msg, bool condition = true)
        {
            if (condition) Debug.Log($"<color=green>[QuickToggle]</color> {msg}");
        }
        private static void RedLog(string msg, bool condition = true)
        {
            if (condition) Debug.Log($"<color=red>[QuickToggle]</color> {msg}");
        }
        #endregion

        #region Automated Methods

        private static void AutoRename()
        {
            if (!autoName || targets.Count == 0)
                return;
            if (string.IsNullOrEmpty(clipName))
                return;

            string statusName = "";
            bool enabled = false, disabled = false;
            foreach (var t in targets)
            {
                if (t.gameObject)
                    if (t.active)
                    {
                        enabled = true;
                        statusName = " Enable";
                    }
                    else
                    {
                        disabled = true;
                        statusName = " Disable";
                    }

                if (enabled && disabled)
                {
                    statusName = " Toggle";
                    break;
                }
            }

            if (clipName == (clipName = Regex.Replace(clipName, " enable", statusName, RegexOptions.IgnoreCase)))
            {
                if (clipName == (clipName = Regex.Replace(clipName, " disable", statusName, RegexOptions.IgnoreCase)))
                {
                    clipName = Regex.Replace(clipName, " toggle", statusName, RegexOptions.IgnoreCase);
                }
            }

        }

        private static void OnRootChanged()
        {
#if VRC_SDK_VRCSDK3
            avatar = root?.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) menu = avatar.expressionsMenu;
#endif
            CheckIfValid();
        }

        private static void CheckIfValid()
        {
            clipValid = root;
            if (!root) return;
            foreach (ToggleObject obj in targets)
                obj.valid = obj.gameObject && obj.gameObject.transform.IsChildOf(root.transform);

            clipValid = targets.All(t => t.valid);
        }

#if VRC_SDK_VRCSDK3
        private static void RefreshUniqueParameter()
        {
            if (!uniqueParameter) return;
            if (!avatar || string.IsNullOrEmpty(_parameter) ||
                vrcAddFlags == VRCToggleFlags.None ||
                vrcAddFlags == VRCToggleFlags.Menu) return;

            if (avatar.expressionParameters && avatar.expressionParameters.parameters != null)
                _parameter = DSHelper.GenerateUniqueString(_parameter, s => avatar.expressionParameters.parameters.All(p => p.name != s));

            foreach (var c in avatar.baseAnimationLayers.Concat(avatar.specialAnimationLayers).Where(p => !p.isDefault).Select(p => p.animatorController as AnimatorController).Where(c => c))
                _parameter = DSHelper.GenerateUniqueString(_parameter, s => c.parameters.All(p => p.name != s));

        }
#endif

        private void OnEnable()
        {
            this.Load(PREF_KEY);

            RefreshList();
            CheckIfValid();
            init = false;
        }

        private void OnDisable() => this.Save(PREF_KEY);

        #endregion

        #region Extra GUI Methods

        private static string CutString(string s, int maxLength)
            => s.Length <= maxLength ? s : s.Substring(0, maxLength - 3) + "...";

        private static void DoToggle(ref bool b, GUIContent label) => b = EditorGUILayout.Toggle(label, b);

        private void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Targets");

            if (GUI.Button(new Rect(rect.width - 10, rect.y + 2, 20, EditorGUIUtility.singleLineHeight), Styles.switchIcon, GUIStyle.none))
            {
                foreach (ToggleObject obj in targets)
                    obj.active = !obj.active;
                AutoRename();
            }
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(index < targets.Count && index >= 0))
                return;
            if (GUI.Button(new Rect(rect.x, rect.y + 2, 20, EditorGUIUtility.singleLineHeight), "X"))
            {
                targets.RemoveAt(index);
                CheckIfValid();
                AutoRename();
                return;
            }

            ToggleObject toggleObj = targets[index];
            Rect myRect = new Rect(rect.x + 22, rect.y + 2, rect.width - 62, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();

            Object dummy;
            dummy = EditorGUI.ObjectField(myRect, toggleObj.GetActive(), typeof(GameObject), true);

            if (EditorGUI.EndChangeCheck())
            {
                if (dummy == null)
                    targets[index] = new ToggleObject();
                else
                {
                    if (((GameObject) dummy).scene.IsValid())
                    {
                        targets[index] = new ToggleObject((GameObject) dummy);
                    }
                    else
                        Debug.LogWarning("[QuickToggle] GameObject must be a scene object!");
                }

                CheckIfValid();
            }

            float xCoord = rect.x + rect.width - 18;
            if (!toggleObj.valid)
                EditorGUI.LabelField(new Rect(xCoord - 60, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), Styles.warnIcon);

            EditorGUI.BeginDisabledGroup(!dummy || targets[index].allComps.Length < 2);
            if (GUI.Button(new Rect(xCoord - 20, rect.y + 2, 20, 18), Styles.nextIcon, GUIStyle.none))
            {
                targets[index].next();
            }

            EditorGUI.EndDisabledGroup();

            if (toggleObj.active)
                if (GUI.Button(new Rect(xCoord, rect.y, 20, 18), Styles.greenLight, GUIStyle.none))
                {
                    toggleObj.active = false;
                    AutoRename();
                }

            if (!toggleObj.active)
                if (GUI.Button(new Rect(xCoord, rect.y, 20, 18), Styles.redLight, GUIStyle.none))
                {
                    toggleObj.active = true;
                    AutoRename();
                }

        }

        private void RefreshList()
        {
            targetList = new UnityEditorInternal.ReorderableList(targets, typeof(ToggleObject), false, true, true, false)
            {
                drawElementCallback = DrawElement,
                drawHeaderCallback = DrawHeader
            };
        }

        #endregion

        #region Classes

        public class ToggleObject
        {
            public GameObject gameObject = null;
            public bool active = true;
            public bool valid = true;

            public Component[] allComps;
            public int index;

            public ToggleObject()
            {
            }

            public ToggleObject(GameObject o)
            {
                this.gameObject = o;
                allComps = gameObject.GetComponents<Component>();
            }

            public Object GetActive()
            {
                if (index == 0)
                {
                    return gameObject;
                }
                else
                {
                    return allComps[index];
                }
            }

            public void next()
            {
                index++;
                if (index >= allComps.Length)
                    index = 0;
            }
        }

        private class SkinnedShapeSlot
        {
            private SkinnedMeshRenderer _renderer;

            public SkinnedMeshRenderer renderer
            {
                get => _renderer;
                private set
                {
                    if (_renderer == value) return;
                    _renderer = value;
                    OnRendererChanged();
                }
            }

            public readonly List<SkinnedShape> shapes = new List<SkinnedShape>();
            private bool expanded;
            private Vector2 scroll;

            public SkinnedShapeSlot()
            {
            }

            public SkinnedShapeSlot(SkinnedMeshRenderer renderer) => this.renderer = renderer;


            void GetShapes()
            {
                shapes.Clear();
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    shapes.Add(new SkinnedShape(renderer.sharedMesh, i));
                }

                SortShapes();
            }

            void SortShapes()
            {
                int CompareShape(SkinnedShape a, SkinnedShape b)
                {
                    return a.name.CompareTo(b.name);
                }

                shapes.Sort(CompareShape);
            }

            public void Draw()
            {
                if (expanded && renderer != null)
                    EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.MaxHeight(300));
                else
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                using (new GUILayout.HorizontalScope())
                {
                    EditorGUIUtility.labelWidth = 10;
                    expanded = GUILayout.Toggle(expanded, string.Empty, GUI.skin.GetStyle("Foldout"), GUILayout.Width(15));
                    renderer = (SkinnedMeshRenderer) EditorGUILayout.ObjectField(string.Empty, renderer, typeof(SkinnedMeshRenderer), true);

                    EditorGUIUtility.labelWidth = 0;

                    if (GUILayout.Button("X", GUI.skin.label, GUILayout.Width(18)))
                    {
                        skinnedRenderers.Remove(this);
                        return;
                    }
                }

                if (expanded && shapes.Count > 0)
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    EditorGUIUtility.labelWidth = 100;
                    EditorGUI.indentLevel++;
                    foreach (var s in shapes) s.Draw();

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndScrollView();
                    EditorGUIUtility.labelWidth = 0;
                }

                EditorGUILayout.EndVertical();
            }

            private void OnRendererChanged()
            {
                if (renderer == null) shapes.Clear();
                else if (root && !renderer.transform.IsChildOf(root.transform))
                {
                    Debug.LogWarning("Renderer is not a child of root!");
                    renderer = null;
                    shapes.Clear();
                }
                else if (!renderer.sharedMesh)
                {
                    Debug.LogWarning("Renderer is not using any mesh!");
                    renderer = null;
                    shapes.Clear();
                }
                else GetShapes();
            }
        }

        private class SkinnedShape
        {
            public string name;
            public int index;
            public float value;

            public SkinnedShape(Mesh m, int i)
            {
                index = i;
                name = m.GetBlendShapeName(i);
                value = 0;
            }

            public void Draw()
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(CutString(name, 28), GUILayout.Width(180));
                    value = EditorGUILayout.Slider(value, 0, 100);
                }
            }
        }

        private static class Styles
        {
            internal static readonly GUIContent
                noteIcon = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.inactive.sml")) {tooltip = "No Avatar Descriptor found on Root"},
                warnIcon = new GUIContent(EditorGUIUtility.IconContent("d_console.warnicon.sml")) {tooltip = "Object is not a child of Root!"},
                greenLight = new GUIContent(EditorGUIUtility.IconContent("d_greenLight")) {tooltip = "Enable"},
                redLight = new GUIContent(EditorGUIUtility.IconContent("d_redLight")) {tooltip = "Disable"},
                switchIcon = new GUIContent(EditorGUIUtility.IconContent("d_Animation.Record")) {tooltip = "Invert Toggles"},
                nextIcon = new GUIContent(EditorGUIUtility.IconContent("Refresh")) {tooltip = "Cycle Components"},
                createOppositeContent = new GUIContent("Create Opposite", "Make the opposite clip for created animation clips"),
                individualToggleContent = new GUIContent("Individual Toggles", "Make an animation clip for each target separately"),
                useAllShapesContent = new GUIContent("Use all Blendshapes", "When creating the clip, utilize all the blendshapes of the Renderers");
        }

        #endregion
    }

    internal static class DSHelper
    {
        internal static void ReadyPath(string folderPath)
        {
            if (Directory.Exists(folderPath)) return;
            Directory.CreateDirectory(folderPath);
            AssetDatabase.ImportAsset(folderPath);
        }
        internal static string AssetFolderPath(string variable, string title)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(title, variable);

                if (!GUILayout.Button("...", GUILayout.Width(30))) return variable;
                var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : "Assets", string.Empty);
                if (string.IsNullOrEmpty(dummyPath))
                    return variable;
                string newPath = FileUtil.GetProjectRelativePath(dummyPath);

                if (!newPath.StartsWith("Assets"))
                {
                    Debug.LogWarning("New Path must be a folder within Assets!");
                    return variable;
                }

                variable = newPath;
            }

            return variable;
        }

        internal static void Save<T>(this T window, string prefs) where T : EditorWindow
        {
            string data = JsonUtility.ToJson(window, false);
            PlayerPrefs.SetString(prefs, data);
        }
        internal static void Load<T>(this T window, string prefs) where T : EditorWindow
        {
            string defaultData = JsonUtility.ToJson(window, false);
            string data =  PlayerPrefs.GetString(prefs, defaultData);
            JsonUtility.FromJsonOverwrite(data, window);
        }

        internal static string GenerateUniqueString(string s, System.Func<string, bool> check)
        {
            if (check(s))
                return s;

            int suffix = 0;

            int.TryParse(s.Substring(s.Length - 2, 2), out int d);
            if (d >= 0)
                suffix = d;
            if (suffix > 0) s = suffix > 9 ? s.Substring(0, s.Length - 2) : s.Substring(0, s.Length - 1);

            s = s.Trim();

            suffix++;

            string newString = s + " " + suffix;
            while (!check(newString))
            {
                suffix++;
                newString = s + " " + suffix;
            }

            return newString;
        }

        internal static AnimatorControllerLayer AddLayer(this AnimatorController controller, string name, float defaultWeight)
        {
            var newLayer = new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = defaultWeight,
                stateMachine = new AnimatorStateMachine
                {
                    name = name,
                    hideFlags = HideFlags.HideInHierarchy
                },
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, controller);
            controller.AddLayer(newLayer);
            return newLayer;
        }

#if VRC_SDK_VRCSDK3
        internal static AnimatorController GetPlayableLayer(this VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type)
            => avi.baseAnimationLayers.Concat(avi.specialAnimationLayers).FirstOrDefault(l => l.type == type).animatorController as AnimatorController;
#endif
    }
}