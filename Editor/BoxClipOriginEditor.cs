/* Copyright (c) 2020 Lyuma <xn.lyuma@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(BoxClipOrigin))]
// [CanEditMultipleObjects]
public class BoxClipOriginEditor : Editor
{
    SerializedProperty managedMaterials;
    SerializedProperty knownBuiltinMaterials;
    SerializedProperty origin;
    SerializedProperty generatedDir;
    DefaultAsset generatedDirAsset;
    SerializedProperty rendererObjects;
    SerializedProperty scale;
    SerializedProperty scaleClipPlanes;
    SerializedProperty allowInFront;
    SerializedProperty doPreview;
    bool isPreviewMode;
    Dictionary<Material, MaterialEditor> unmanagedMaterialEditors = new Dictionary<Material, MaterialEditor>();
    Dictionary<Material, MaterialEditor> materialEditors = new Dictionary<Material, MaterialEditor>();
    // MaterialEditor multiMaterialEditors;

    private static GUIStyle ToggleButtonStyleNormal = null;
    private static GUIStyle ToggleButtonStyleToggled = null;
    private static GUIStyle style_BoldLabel;
    private static GUIStyle style_BoldCenterLabel;

    private Color defaultBackgroundColor = Color.white;
    private Color badShaderHighlight = new Color(1f,0.65f,0.6f,1f);

    void OnEnable()
    {
        managedMaterials = serializedObject.FindProperty("managedMaterials");
        knownBuiltinMaterials = serializedObject.FindProperty("knownBuiltinMaterials");
        origin = serializedObject.FindProperty("origin");
        generatedDir = serializedObject.FindProperty("generatedDir");
        rendererObjects = serializedObject.FindProperty("rendererObjects");
        scale = serializedObject.FindProperty("scale");
        scaleClipPlanes = serializedObject.FindProperty("scaleClipPlanes");
        allowInFront = serializedObject.FindProperty("allowInFront");
        doPreview = serializedObject.FindProperty("doPreview");
        isPreviewMode = doPreview.boolValue;
        generatedDirAsset = null;
        if (generatedDir != null) {
            generatedDirAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(generatedDir.stringValue);
        }
    }

    bool IsManagedAsset(Object o) {
        return AssetDatabase.GetAssetPath(o).StartsWith(generatedDir.stringValue + "/");
    }

    bool IsBoxClipShader(Shader s) {
        return (IsManagedAsset(s) || s.name.StartsWith("BoxClip/"));
    }
    bool IsBoxClipBakedShader(Shader s) {
        return s.name.StartsWith("Hidden/BoxClipBaked/");
    }

    void manageMaterialShader(Material m) {
        if (!IsManagedAsset(m)) {
            Debug.LogError("Material " + m.name + " must be managed to change shader", m);
            return;
        }
        Shader origShader = m.shader;
        if (IsBoxClipShader(origShader)) {
            return; // already managed.
        }
        if (IsBoxClipBakedShader(origShader)) {
            Debug.LogWarning("Shader " + origShader.name + " used by material " + m + " was baked for a different BoxClip instance!", m);
            return; // managed for another boxclip instance
        }
        string boxClipName = "BoxClip/" + origShader.name;
        Shader newShader = Shader.Find(boxClipName);
        if (newShader == null) {
            Debug.LogWarning("Failed to find BoxClip variant of shader " + origShader.name, m);
            return;
        }
        int oldQueue = m.renderQueue;
        m.shader = newShader;
        m.renderQueue = oldQueue;
    }

    Material getBuiltinMaterial(string name) {
        foreach (SerializedProperty prop in knownBuiltinMaterials) {
            Material m = (Material)prop.objectReferenceValue;
            if (m != null && m.name.Equals(name)) {
                return m;
            }
        }
        return null;
    }
    void addBuiltinMaterial(Material m) {
        if (getBuiltinMaterial(m.name) == null) {
            int arrsz = knownBuiltinMaterials.arraySize;
            knownBuiltinMaterials.arraySize++;
            int i = 0;
            foreach (SerializedProperty prop in knownBuiltinMaterials) {
                if (i == arrsz) {
                    prop.objectReferenceValue = m;
                }
                i++;
            }
        }
    }

    Material manageMaterial(Material m, Object ctx) {
        if (IsManagedAsset(m)) {
            return m;
        }
        string guid;
        long localId;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out guid, out localId)) {
            Debug.LogError("Unable to lookup GUID for material " + m.name, ctx);
            return m;
        }
        if (AssetDatabase.GUIDToAssetPath(guid).StartsWith("Resources/")) {
            // Unity Bug: cannot iterate through builtin resources.
            // https://issuetracker.unity3d.com/issues/using-loadallassetsatpath-to-load-the-paths-does-not-load-objects-stored-at-paths-and-leaves-the-the-expecting-object-empty
            // So we will keep our own references to known builtin materials.
            addBuiltinMaterial(m);
        }
        // Debug.Log("Asset " + m.name + " is at " + AssetDatabase.GetAssetPath(m) + "; instanceId=" + m.GetInstanceID() + ": local id:" + localId);
        // Debug.Log("GUID For asset " + m.name + " is " + guid + " at " + AssetDatabase.GUIDToAssetPath(guid), m);
        string materialdir = generatedDir.stringValue;
        string newBasename = m.name + " BoxClip";
        Material outm = null;
        foreach (string testGuid in AssetDatabase.FindAssets(newBasename, new string[]{materialdir})) {
            Material testm = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(testGuid));
            if (guid.Equals(testm.GetTag("BoxClipGUID", false))) {
                outm = testm;
                break;
            }
        }
        if (outm == null) {
            outm = new Material(m);
            outm.SetOverrideTag("BoxClipGUID", guid);
            outm.SetOverrideTag("BoxClipName", m.name);
            outm.name = m.name;
            AssetDatabase.CreateAsset(outm, AssetDatabase.GenerateUniqueAssetPath(materialdir + "/" + newBasename + ".mat"));
        } else {
            outm.CopyPropertiesFromMaterial(m);
            outm.SetOverrideTag("BoxClipGUID", guid);
            outm.SetOverrideTag("BoxClipName", m.name);
            outm.name = m.name;
            outm.shader = m.shader;
            outm.renderQueue = m.renderQueue;
        }
        manageMaterialShader(outm);
        return outm;
    }

    Material unmanageMaterial(Material m) {
        string origMatGUID = m.GetTag("BoxClipGUID", false);
        string origMatName = m.GetTag("BoxClipName", false);
        if (origMatGUID == null || origMatGUID.Length == 0) {
            return null;
        }
        Material origMat = null;
        if (AssetDatabase.GUIDToAssetPath(origMatGUID).StartsWith("Resources/")) {
            origMat = getBuiltinMaterial(origMatName);
        }
        Object [] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(origMatGUID));
        Debug.Log("Loading guid " + origMatGUID + " path " + AssetDatabase.GUIDToAssetPath(origMatGUID) + " has " + assets.Length + " assets!");
        foreach (Object testobj in assets) {
            if (testobj is Material testm) {
                Debug.Log("Found builtin material " + testm.name + "; looking for " + origMatName);
                if (testm.name.Equals(origMatName) || testm.name.Equals(m.name.Substring(0, m.name.IndexOf(" BoxClip")))) {
                    origMat = testm;
                    break;
                }
            }
        }
        return origMat;
    }

    private void updateManagedMaterials(Dictionary<Material, List<Renderer>> materialSet) {
        List<Material> targetManaged = ((BoxClipOrigin)this.target).getManagedMaterialList();
        bool dirty = true;
        if (targetManaged.Count == materialSet.Count) {
            dirty = false;
            int unmatched = materialSet.Count;
            foreach (Material testm in targetManaged) {
                if (materialSet.ContainsKey(testm)) {
                    unmatched--;
                } else {
                    dirty = true;
                    break;
                }
            }
            if (unmatched != 0) {
                dirty = true;
            }
        }
        if (dirty) {
            int msCount = materialSet.Count;
            int delta = msCount - targetManaged.Count;
            Debug.Log("Delta is " + delta);
            managedMaterials.arraySize = msCount;
            // while (delta < 0) {
            //     delta++;
            //     managedMaterials.DeleteArrayElementAtIndex(msCount - delta);
            //     Debug.Log("Delete one");
            // }
            // while (delta > 0) {
            //     managedMaterials.InsertArrayElementAtIndex(msCount - delta);
            //     delta--;
            //     Debug.Log("Insert one");
            // }
            Debug.Log("List size = "  + materialSet.Count);
            List<Material> updatedList = new List<Material>(materialSet.Keys);
            int i = 0;
            foreach (SerializedProperty sp in managedMaterials) {
                sp.objectReferenceValue = updatedList[i];
            }
            i++;
            List<Material> oldMaterialEditors = new List<Material>(materialEditors.Keys);
            foreach (Material m in oldMaterialEditors) {
                if (!materialSet.ContainsKey(m)) {
                    materialEditors.Remove(m);
                }
            }
            foreach (Material m in updatedList) {
                if (!materialEditors.ContainsKey(m) && materialEditors.Count < 10) {
                    ///////materialEditors.Add(m, (MaterialEditor)CreateEditor(m));
                }
            }
            // multiMaterialEditors = (MaterialEditor)CreateEditor(updatedList.ToArray());
        }
    }

    private void drawMaterialList(ref Dictionary<Shader, List<Material>> shaderSet) {
        Dictionary<Material, List<Renderer>> unManagedMaterialSet = new Dictionary<Material, List<Renderer>>();
        Dictionary<Material, List<Renderer>> materialSet = new Dictionary<Material, List<Renderer>>();
        shaderSet = new Dictionary<Shader, List<Material>>();
        foreach (SerializedProperty sp in rendererObjects) {
            Renderer r = (Renderer)sp.objectReferenceValue;
            if (!r) {
                continue;
            }
            foreach (Material m in r.sharedMaterials) {
                if (m == null) {
                    continue;
                }
                if (materialSet.ContainsKey(m)) {
                    materialSet[m].Add(r);
                    continue;
                }
                if (unManagedMaterialSet.ContainsKey(m)) {
                    unManagedMaterialSet[m].Add(r);
                    continue;
                }
                if (IsManagedAsset(m)) {
                    materialSet[m] = new List<Renderer>(new Renderer[]{r});
                } else {
                    unManagedMaterialSet[m] = new List<Renderer>(new Renderer[]{r});
                }
            }
        }
        updateManagedMaterials(materialSet);
        if (unManagedMaterialSet.Count > 0) {
            Rect topR = EditorGUILayout.GetControlRect();
            GUILayout.Label("Unmanaged Materials", style_BoldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("The following materials are not yet managed by this script.", MessageType.Warning);
            if (GUILayout.Button("Manage All")) {
                Dictionary<Material, Material> dict = new Dictionary<Material, Material>();
                foreach (Material m in unManagedMaterialSet.Keys) {
                    List<Renderer> renderers = unManagedMaterialSet[m];
                    Material outm = manageMaterial(m, renderers[0]);
                    if (outm != null) {
                        dict[m] = outm;
                    }
                }
                foreach (SerializedProperty sp in rendererObjects) {
                    Renderer r = (Renderer)sp.objectReferenceValue;
                    if (!r) {
                        continue;
                    }
                    Material[] sharedMats = r.sharedMaterials;
                    bool update = false;
                    for (int i = 0; i < sharedMats.Length; i++) {
                        if (sharedMats[i] == null) {
                            continue;
                        }
                        Material outmat;
                        if (dict.TryGetValue(sharedMats[i], out outmat)) {
                            sharedMats[i] = outmat;
                            update = true;
                        }
                    }
                    if (update) {
                        r.sharedMaterials = sharedMats;
                    }
                }
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
            foreach (Material m in unManagedMaterialSet.Keys) {
                if (materialSet.Count == 0) {
                    if (!unmanagedMaterialEditors.ContainsKey(m) && unmanagedMaterialEditors.Count < 10) {
                        ///////unmanagedMaterialEditors.Add(m, (MaterialEditor)CreateEditor(m));
                    }
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                Material newM = (Material)EditorGUILayout.ObjectField(m, typeof(object), true);
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("Manage", EditorStyles.miniButton, GUILayout.Width(100))) {
                    List<Renderer> rens = unManagedMaterialSet[m];
                    Material newm = manageMaterial(m, rens[0]);
                    foreach (Renderer r in rens) {
                        Material[] sharedMats = r.sharedMaterials;
                        for (int i = 0; i < sharedMats.Length; i++) {
                            if (sharedMats[i] == m) {
                                sharedMats[i] = newm;
                            }
                        }
                        r.sharedMaterials = sharedMats;
                    }
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.EndHorizontal();
            }
            Rect bottomR = EditorGUILayout.GetControlRect();
            Handles.BeginGUI();
            Handles.color = Color.red;
            Handles.DrawLine(new Vector3(topR.x - 2, topR.y + 10), new Vector3(topR.x + topR.width + 2, topR.y + 10));
            Handles.DrawLine(new Vector3(topR.x + topR.width + 2, topR.y + 10), new Vector3(topR.x + topR.width + 2, bottomR.y + 10));
            Handles.DrawLine(new Vector3(topR.x - 2, bottomR.y + 10), new Vector3(topR.x + topR.width + 2, bottomR.y + 10));
            Handles.DrawLine(new Vector3(topR.x - 2, topR.y + 10), new Vector3(topR.x - 2, bottomR.y + 10));
            Handles.EndGUI();
            // Rect borderRect = new Rect(topR.x, topR.y, topR.width, bottomR.y - topR.y);
            // GUILayout.Box("Box");
        } else {
            EditorGUILayout.GetControlRect();
        }
        foreach (Material m in new List<Material>(unmanagedMaterialEditors.Keys)) {
            if (!unManagedMaterialSet.ContainsKey(m)) {
                unmanagedMaterialEditors.Remove(m);
            }
        }
        if (materialSet.Count > 0) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Managed Materials", style_BoldLabel);
            if (GUILayout.Button("Unmanage All")) {
                Dictionary<Material, Material> dict = new Dictionary<Material, Material>();
                foreach (Material m in materialSet.Keys) {
                    Material outm = unmanageMaterial(m);
                    if (outm != null) {
                        dict[m] = outm;
                    }
                }
                foreach (SerializedProperty sp in rendererObjects) {
                    Renderer r = (Renderer)sp.objectReferenceValue;
                    if (!r) {
                        continue;
                    }
                    Material[] sharedMats = r.sharedMaterials;
                    bool update = false;
                    for (int i = 0; i < sharedMats.Length; i++) {
                        if (sharedMats[i] == null) {
                            continue;
                        }
                        Material outmat;
                        if (dict.TryGetValue(sharedMats[i], out outmat)) {
                            sharedMats[i] = outmat;
                            update = true;
                        }
                    }
                    if (update) {
                        r.sharedMaterials = sharedMats;
                    }
                }
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        foreach (Material m in materialSet.Keys) {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            Material newM = (Material)EditorGUILayout.ObjectField(m, typeof(Material), true);
            EditorGUI.EndDisabledGroup();
            if (shaderSet.ContainsKey(m.shader)) {
                shaderSet[m.shader].Add(m);
            } else {
                shaderSet.Add(m.shader, new List<Material>(new Material[]{m}));
            }
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(20))) {
                Material oldm = unmanageMaterial(m);
                if (oldm != null) {
                    foreach (Renderer r in materialSet[m]) {
                        Material[] sharedMats = r.sharedMaterials;
                        for (int i = 0; i < sharedMats.Length; i++) {
                            if (sharedMats[i] == m) {
                                sharedMats[i] = oldm;
                            }
                        }
                        r.sharedMaterials = sharedMats;
                    }
                    AssetDatabase.SaveAssets();
                }
            }
            EditorGUILayout.EndHorizontal();
            if (!IsBoxClipShader(m.shader)) {
                GUI.backgroundColor = badShaderHighlight;
                EditorGUILayout.BeginHorizontal(new GUIStyle { normal = { background = Texture2D.whiteTexture } });
            } else {
                EditorGUILayout.BeginHorizontal();
            }
            GUILayout.Label("", GUILayout.MaxWidth(100));
            Shader s = (Shader)EditorGUILayout.ObjectField(m.shader, typeof(Shader), true);
            if (s != m.shader) {
                m.shader = s;
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = defaultBackgroundColor;
            if (!materialEditors.ContainsKey(m) && materialEditors.Count < 10) {
                //////materialEditors.Add(m, (MaterialEditor)CreateEditor(m));
            }
        }
        if (materialSet.Count > 0) {
            EditorGUILayout.GetControlRect();
        }
    }

    private void performPreviewChecks() {
        Transform origin = ((Transform)this.origin.objectReferenceValue).transform;
        Vector3 originup = origin.TransformPoint(Vector3.up);
        Vector3 originfwd = origin.TransformPoint(Vector3.forward);
        Vector3 originleft = origin.TransformPoint(Vector3.left);
        HashSet<Material> materialSet = new HashSet<Material>();
        bool particleWarning = false;
        bool particleInfo = false;

        foreach (SerializedProperty sp in rendererObjects) {
            Renderer r = (Renderer)sp.objectReferenceValue;
            if (!r) {
                continue;
            }
            Transform tr = r.transform;
            bool isWorldSpace = false;
            if (r is SkinnedMeshRenderer smr) {
                tr = smr.rootBone;
            } else {
                smr = null;
            }
            if (r is ParticleSystemRenderer psr || r is LineRenderer lr || r is TrailRenderer xtr) {
                isWorldSpace = true;
                particleInfo = true;
            }
            if (tr == null || Vector3.Distance(isWorldSpace ? Vector3.up : originup, tr.TransformPoint(Vector3.up)) > 0.001f ||
            Vector3.Distance(isWorldSpace ? Vector3.forward : originfwd, tr.TransformPoint(Vector3.forward)) > 0.001f ||
            Vector3.Distance(isWorldSpace ? Vector3.left : originleft, tr.TransformPoint(Vector3.left)) > 0.001f || tr.CompareTag("EditorOnly")) {
                if (isWorldSpace) {
                    particleWarning = true;
                }
                EditorGUILayout.BeginHorizontal();
                if (tr == null) {
                    EditorGUILayout.HelpBox("\n \nRenderer " + r.name + " rootBone is null!\n \n", MessageType.Error);
                } else if (tr.CompareTag("EditorOnly")) {
                    EditorGUILayout.HelpBox("\n \nRenderer " + r.name + " rootBone is marked EditorOnly!\n \n", MessageType.Error);
                } else {
                    EditorGUILayout.HelpBox("\n \nRenderer " + r.name + " is too far from " + (isWorldSpace ? "world origin" : "BoxClipOrigin transform\n \n"), MessageType.Warning);
                }
                EditorGUILayout.BeginVertical();
                if (GUILayout.Button("Select")) {
                    Selection.SetActiveObjectWithContext(r, this.target);
                }
                if (smr != null) {
                    if (GUILayout.Button("Fix Root and Bounds")) {
                        Undo.RecordObject(smr, "Fix Root and Bounds");
                        smr.localBounds = LyumaMeshUtility.expandBounds(origin, smr.rootBone == null ? smr.transform : smr.rootBone, smr.localBounds);
                        smr.rootBone = origin;
                    }
                    if (GUILayout.Button("Fix Root Bone only")) {
                        Undo.RecordObject(smr, "Fix Root Bone only");
                        smr.rootBone = origin;
                    }
                } else if (r is MeshRenderer mr) {
                    if (GUILayout.Button("Recenter Mesh")) {
                        LyumaMeshUtility.RecenterMesh(mr, origin, generatedDir.stringValue);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }
        if (particleWarning || particleInfo) {
            if (Vector3.Distance(Vector3.up, originup) > 0.001f ||
                    Vector3.Distance(Vector3.forward, originfwd) > 0.001f ||
                    Vector3.Distance(Vector3.left, originleft) > 0.001f) {
                particleWarning = true;
            }
            EditorGUILayout.HelpBox("Particles and trails are only compatible if this BoxClipOrigin always remains at 0,0,0 in world space.", particleWarning ? MessageType.Error : MessageType.Info);
        }
    }

    int findAndConsumeComments(string target, int lastPos, string shaderSource) {
        int itercount = 999;
        int targetPos = 0;
        while (itercount-- > 0) {
            targetPos = shaderSource.IndexOf(target, lastPos, System.StringComparison.CurrentCultureIgnoreCase);
            if (targetPos == -1) {
                break;
            }
            while (itercount-- > 0) {
                int commenta = shaderSource.IndexOf("//", lastPos);
                int commentb = shaderSource.IndexOf("/*", lastPos);
                if (commenta != -1 && commenta < targetPos && (commenta < commentb || commentb == -1)) {
                    lastPos = shaderSource.IndexOf('\n', commenta);
                } else if (commentb != -1 && commentb < targetPos) {
                    lastPos = shaderSource.IndexOf("*/", commentb + 2);
                } else {
                    break;
                }
            }
            if (lastPos <= targetPos) {
                break;
            }
        }
        if (itercount <= 0) {
            throw new System.Exception("Failed to iterate through shader source!");
        }
        return targetPos;
    }

    static System.Globalization.NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo();
    static BoxClipOriginEditor() {
        nfi.NumberDecimalSeparator = ".";
    }

    string printFloat(float f) {
        return (float.IsInfinity(f) || float.IsNaN(f)) ? "0" : f.ToString(nfi);
    }
    string printFloat3(Vector4 data) {
        return "float3(" + printFloat(data.x) + "," + printFloat(data.y) + "," + printFloat(data.z) + ")";
    }
    string printFloat4(Vector4 data) {
        return "float4(" + printFloat(data.x) + "," + printFloat(data.y) + "," + printFloat(data.z) + "," + printFloat(data.w) + ")";
    }
    string generateBoxQuadsMacro(BoxClipOrigin boxClipObj, string arrName, BoxClipOrigin.BoxClipArray arrType) {
        int count;
        Vector4[] boxQuads = boxClipObj.createBoxQuads((Transform)origin.objectReferenceValue, arrType, out count);
        string outSource = "    #define boxQuad_" + arrName + "_Count " + count + "\n" +
        "    #define DECLARE_BOXCLIP_" + arrName + "_ARRAY static internalBoxQuad boxQuad_" + arrName + "_Quads[" + (count == 0 ? 1 : count) + "] = {\\\n";
        for (int i = 0 ; i < count || i == 0; i++) {
            outSource += "        {" +
                printFloat3(boxQuads[i * 4]) +
                "," + printFloat4(boxQuads[i * 4 + 1]) +
                "," + printFloat4(boxQuads[i * 4 + 2]) +
                "," + printFloat4(boxQuads[i * 4 + 3]) +
                "}" + (i == count - 1 ? "" : ",") + "\\\n";
        }
        outSource += "};\n\n";
        return outSource;
    }

    Shader BakeShader(Shader orig) {
        string path = AssetDatabase.GetAssetPath(orig);
        System.IO.StreamReader reader = new System.IO.StreamReader(path);
        string shaderSource = reader.ReadToEnd();
        reader.Close();
        string outfilename = AssetDatabase.GenerateUniqueAssetPath(generatedDir.stringValue + "/" + orig.name.Replace("/","-") + ".shader");
        int shaderNamePos = findAndConsumeComments("Shader", 0, shaderSource);
        int firstQuote = findAndConsumeComments("\"", shaderNamePos, shaderSource);
        int secondQuote = shaderSource.IndexOf ("\"", firstQuote + 1);
        int brace = findAndConsumeComments ("{", secondQuote + 1, shaderSource);

        BoxClipOrigin boxClipObj = (BoxClipOrigin)target;
        // (generatedDir.stringValue.GetHashCode() & 0x7fffffff)
        string outSource = shaderSource.Substring(0, firstQuote) + "\"" + "Hidden/BoxClipBaked/" + AssetDatabase.AssetPathToGUID(generatedDir.stringValue) + "Inst/" + orig.name.Substring(8) + "\" {\n";
        outSource += "CGINCLUDE\n" +
        "    #define BOXCLIP_CONFIGURED 1\n" +
        "    #define BOXCLIP_SCALE " + printFloat(scale.floatValue) + "\n" +
        "    #define BOXCLIP_ALLOW_IN_FRONT " + printFloat(allowInFront.floatValue) + "\n" +
        "    #define BOXCLIP_ALLOW_IN_FRONT " + printFloat(allowInFront.floatValue) + "\n\n";
        outSource += generateBoxQuadsMacro(boxClipObj, "ClipShow", BoxClipOrigin.BoxClipArray.ClipShow);
        outSource += generateBoxQuadsMacro(boxClipObj, "ClipHide", BoxClipOrigin.BoxClipArray.ClipHide);
        outSource += generateBoxQuadsMacro(boxClipObj, "ShowVolume", BoxClipOrigin.BoxClipArray.ShowVolume);
        outSource += generateBoxQuadsMacro(boxClipObj, "HideVolume", BoxClipOrigin.BoxClipArray.HideVolume);
        outSource += generateBoxQuadsMacro(boxClipObj, "ShowCameraWithin", BoxClipOrigin.BoxClipArray.ShowCameraWithin);
        outSource += generateBoxQuadsMacro(boxClipObj, "HideCameraWithin", BoxClipOrigin.BoxClipArray.HideCameraWithin);
        outSource += generateBoxQuadsMacro(boxClipObj, "ZCompress", BoxClipOrigin.BoxClipArray.ZCompress);
        outSource += "\nENDCG\n";
        outSource += shaderSource.Substring(brace + 1);

        System.IO.StreamWriter writer = new System.IO.StreamWriter(outfilename, true);
        writer.Write(outSource);
        writer.Close();
        string cgincpath = System.IO.Path.GetDirectoryName(path) + "/BoxClipTemplate.cginc";
        string targetcgincpath = generatedDir.stringValue + "/BoxClipTemplate.cginc";
        reader = new System.IO.StreamReader(cgincpath);
        string cgincSource = reader.ReadToEnd();
        reader.Close();
        writer = new System.IO.StreamWriter(targetcgincpath);
        writer.Write(cgincSource);
        writer.Close();
        cgincpath = System.IO.Path.GetDirectoryName(path) + "/BoxClipStandardShadow.cginc";
        targetcgincpath = generatedDir.stringValue + "/BoxClipStandardShadow.cginc";
        reader = new System.IO.StreamReader(cgincpath);
        cgincSource = reader.ReadToEnd();
        reader.Close();
        writer = new System.IO.StreamWriter(targetcgincpath);
        writer.Write(cgincSource);
        writer.Close();
        AssetDatabase.ImportAsset(outfilename);
        return AssetDatabase.LoadAssetAtPath<Shader>(outfilename);
    }
    void BakeShaders(Dictionary<Shader, List<Material>> shaderSet) {
        foreach (Shader s in shaderSet.Keys) {
            if (IsBoxClipShader(s)) {
                if (s.name.StartsWith("BoxClip/")) {
                    // This is an unbaked version.
                    Shader baked = BakeShader(s);
                    if (baked != null) {
                        foreach (Material m in shaderSet[s]) {
                            int rq = m.renderQueue;
                            m.shader = baked;
                            m.renderQueue = rq;
                        }
                    }
                }
            }
        }
    }
    void UnbakeShaders(Dictionary<Shader, List<Material>> shaderSet) {
        foreach (Shader s in shaderSet.Keys) {
            if (IsBoxClipShader(s) && !s.name.StartsWith("BoxClip/")) {
                // This is a baked version.
                // All baked shaders have the same prefix, so it is easy to go back.
                int idx = s.name.IndexOf("Inst/");
                Shader unbaked = Shader.Find("BoxClip/" + s.name.Substring(idx + 5));
                if (unbaked != null) {
                    foreach (Material m in shaderSet[s]) {
                        int rq = m.renderQueue;
                        m.shader = unbaked;
                        m.renderQueue = rq;
                    }
                }
                if (IsManagedAsset(s)) {
                    AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(s));
                }
            }
        }
    }

    public override void OnInspectorGUI()
    {
        defaultBackgroundColor = GUI.backgroundColor;
        if (style_BoldLabel == null) {
            style_BoldLabel = new GUIStyle(EditorStyles.label);
            style_BoldLabel.fontStyle = FontStyle.Bold;
            style_BoldCenterLabel = new GUIStyle(EditorStyles.label);
            style_BoldCenterLabel.alignment = TextAnchor.MiddleCenter;
            style_BoldCenterLabel.fontStyle = FontStyle.Bold;
            style_BoldCenterLabel.normal.textColor = Color.red;//new Color ( 0.35f,0.35f,0.35f, 1 );
        }
        if ( ToggleButtonStyleNormal == null ) {
            ToggleButtonStyleNormal = "Button";
            ToggleButtonStyleToggled = new GUIStyle(ToggleButtonStyleNormal);
            ToggleButtonStyleToggled.normal.background = ToggleButtonStyleToggled.active.background;
        }
        serializedObject.UpdateIfRequiredOrScript();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(generatedDir, new GUIContent("Generated asset dir"));
        if (EditorGUI.EndChangeCheck()) {
            generatedDirAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(generatedDir.stringValue);
        }
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.MaxWidth(100));
        DefaultAsset newAsset = (DefaultAsset)EditorGUILayout.ObjectField("", generatedDirAsset, typeof(DefaultAsset), false);
        EditorGUILayout.EndHorizontal();
        if (newAsset == null || generatedDir.stringValue == null || generatedDir.stringValue.Length == 0) {
            EditorGUILayout.HelpBox("Please specify a Generated asset directory.", MessageType.Warning);
        }
        if (newAsset != generatedDirAsset) {
            // if (newAsset == null) {
            //     generatedDir.stringValue = null;
            // } else {
                Debug.Log("Got a change in this asset! " + AssetDatabase.GetAssetPath(newAsset) + " asset " + newAsset);
                generatedDir.stringValue = AssetDatabase.GetAssetPath(newAsset);
                generatedDirAsset = (DefaultAsset)newAsset;
            // }
        }
        EditorGUILayout.PropertyField(origin, new GUIContent("Origin Transform"));
        EditorGUILayout.GetControlRect();
        EditorGUI.BeginDisabledGroup(generatedDir.stringValue == null || generatedDir.stringValue.Length == 0);
        if (GUILayout.Button("Preview", isPreviewMode ? ToggleButtonStyleToggled : ToggleButtonStyleNormal)) {
            isPreviewMode = !isPreviewMode;
            doPreview.boolValue = isPreviewMode;
        }
        bool earlyRet = false;
        if (origin.objectReferenceValue == null || ((Transform)(origin.objectReferenceValue)).gameObject.CompareTag("EditorOnly")) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(origin.objectReferenceValue == null ? "Please specify an origin transform object." : "Avoid using an EditorOnly Origin Transform.", MessageType.Error);
            if (rendererObjects.arraySize > 0 && rendererObjects.GetArrayElementAtIndex(0).objectReferenceValue != null) {
                if (GUILayout.Button("Auto Fix")) {
                    Renderer smrTrans = (Renderer)rendererObjects.GetArrayElementAtIndex(0).objectReferenceValue;
                    Transform newOrigin = new GameObject(this.name + "Origin").transform;
                    Transform oldOrigin = (Transform)origin.objectReferenceValue;
                    if (oldOrigin == null) {
                        oldOrigin = ((BoxClipOrigin)this.target).transform;
                    }
                    newOrigin.SetParent(oldOrigin.transform, false);
                    newOrigin.SetParent(smrTrans.transform, true);
                    origin.objectReferenceValue = newOrigin;
                }
            }
            EditorGUILayout.EndHorizontal();
            earlyRet = true;
        }
        EditorGUILayout.PropertyField(rendererObjects, new GUIContent("List of Renderers"), true);
        Dictionary<Shader, List<Material>> shaderSet = null;
        int unbakedShaderCount = 0;
        int bakedShaderCount = 0;
        if (Event.current.type != EventType.DragPerform) { 
            EditorGUI.BeginDisabledGroup(generatedDirAsset == null);
            if (!earlyRet) { performPreviewChecks(); }
            EditorGUI.EndDisabledGroup();
            drawMaterialList(ref shaderSet);
            foreach (Shader s in shaderSet.Keys) {
                if (!IsBoxClipShader(s)) {
                    EditorGUILayout.BeginHorizontal();
                    Shader boxClipVersion = Shader.Find("BoxClip/" + s.name);
                    if (IsBoxClipBakedShader(s)) {
                        EditorGUILayout.HelpBox("Shader " + s.name + " was baked for a different BoxClip instance!", MessageType.Error);
                        boxClipVersion = null;
                    } else if (boxClipVersion != null) {
                        EditorGUILayout.HelpBox("Materials are not using the BoxClip version of " + s.name + ". Please switch to the BoxClip shader.", MessageType.Warning);
                    } else {
                        EditorGUILayout.HelpBox("Shader " + s.name + " is unsupported. Please port the shader to BoxClip or select a BoxClip shader.", MessageType.Error);
                    }
                    EditorGUILayout.BeginVertical();
                    if (GUILayout.Button("Select Material")) {
                        Selection.objects = shaderSet[s].ToArray();//, this.target);
                    }
                    if (boxClipVersion != null) {
                        if (GUILayout.Button("Auto Fix")) {
                            foreach (Material m in shaderSet[s]) {
                                int q = m.renderQueue;
                                m.shader = boxClipVersion;
                                m.renderQueue = q;
                            }
                        }
                    } else {
                        if (GUILayout.Button("Select Shader")) {
                            Selection.SetActiveObjectWithContext(s, this.target);
                        }
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                } else if (!IsBoxClipBakedShader(s)) {
                    unbakedShaderCount++;
                } else {
                    bakedShaderCount++;
                }
            }
        }
        GUILayout.Label("Other settings", style_BoldLabel);
        EditorGUILayout.PropertyField(scale);
        EditorGUILayout.PropertyField(scaleClipPlanes);
        EditorGUILayout.PropertyField(allowInFront);
        serializedObject.ApplyModifiedProperties();
        if (earlyRet) {
            EditorGUILayout.HelpBox("Fix outstanding errors first.", MessageType.Error);
            return;
        }

        if (unbakedShaderCount != 0) {
            if (GUILayout.Button("Bake to Shader")) {
                BakeShaders(shaderSet);
            }
        }
        if (bakedShaderCount != 0) {
            if (GUILayout.Button("Unbake Shaders")) {
                UnbakeShaders(shaderSet);
            }
        }
        int materialI = 0;
        List<Material> targetManaged = ((BoxClipOrigin)this.target).getManagedMaterialList();
        foreach (MaterialEditor materialEditor in materialEditors.Values) {
            if (materialEditor == null) {
                continue;
            }
            if (materialI > 10) {
                break;
            }
            materialI++;
            string shaderName = ((Material)materialEditor.target).shader.name;
            if (!shaderName.StartsWith("BoxClip/")) {
                GUI.backgroundColor = badShaderHighlight;
            }
            materialEditor.DrawHeader();
            GUI.backgroundColor = defaultBackgroundColor;
            materialEditor.OnInspectorGUI();
        }
        if (unmanagedMaterialEditors.Count > 0) {
            GUILayout.Label("*** UNMANAGED MATERIALS ***", style_BoldCenterLabel);
            foreach (MaterialEditor materialEditor in unmanagedMaterialEditors.Values) {
                if (materialI > 10) {
                    break;
                }
                materialI++;
                if (materialEditor == null) {
                    continue;
                }
                materialEditor.DrawHeader();
                materialEditor.OnInspectorGUI();
            }
        }
        // multiMaterialEditors.DrawHeader();
        // multiMaterialEditors.OnInspectorGUI();
        EditorGUI.EndDisabledGroup();
    }
}