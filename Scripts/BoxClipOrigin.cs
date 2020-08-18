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
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]

public class BoxClipOrigin : MonoBehaviour {
#if UNITY_EDITOR
    public Transform origin;
    [SerializeField] [HideInInspector] private List<Material> knownBuiltinMaterials = new List<Material>();
    public List<Renderer> rendererObjects = new List<Renderer>();

    [SerializeField] private List<Material> managedMaterials = new List<Material>();
    public string generatedDir;

    public float scale = 1;
    public bool scaleClipPlanes = false;
    // public Vector3 billboardOrigin = new Vector3(0,0,0);
    // public bool enableBillboard;
    // public bool enableBillboardWorldSpace;
    // public bool enableBillboardVertical;
    public float allowInFront;

    public bool doPreview;

    private Dictionary<Renderer, MaterialPropertyBlock> rendererToMPB = new Dictionary<Renderer, MaterialPropertyBlock>();
    private HashSet<Transform> initializedXforms = new HashSet<Transform>();
    private Transform zcompressRoot;
    private Transform hideRoot;
    private Transform showRoot;
    private Transform hideVolumeRoot;
    private Transform showVolumeRoot;
    private Transform hideCameraWithinRoot;
    private Transform showCameraWithinRoot;
    private Transform trashRoot;

    public List<Material> getManagedMaterialList() {
        return managedMaterials;
    }
    private Transform getOrCreateTransform(Transform oldT, string name, bool createColliders, bool isThick) {
        Transform ret = null;
        if (oldT != null && oldT.parent == this.transform) {
            ret = oldT;
        }
        if (ret == null) {
            ret = this.transform.Find(name);
        }
        if (ret == null) {
            ret = (new GameObject(name)).transform;
            ret.SetParent(this.transform, false);
        }
        if (createColliders) {
            for (int i = 0; i < ret.childCount; i++) {
                Transform tmp = ret.GetChild(i);
                if (initializedXforms.Contains(tmp)) {
                    // continue;
                }
                MeshRenderer mr = tmp.GetComponent<MeshRenderer>();
                if (mr != null) {
                    mr.enabled = false;
                }
                BoxCollider bc = tmp.GetComponent<BoxCollider>();
                if (bc == null) {
                    bc = tmp.gameObject.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f,0f,0f);
                    bc.size = new Vector3(1f,1f,isThick?1f:0.001f);
                }
                bc.enabled = false;
                if (!isThick) {
                    if (bc.size.z == 1f) {
                        bc.size = new Vector3(bc.size.x, bc.size.y, 0.001f);
                    } else if (bc.size.z > 0.999999f) {
                        bc.size = new Vector3(bc.size.x, bc.size.y, 0.999999f);
                    } else if (bc.size.z < 0f) {
                        bc.size = new Vector3(bc.size.x, bc.size.y, 0f);
                    }
                }
                initializedXforms.Add(tmp);
            }
        }
        return ret;
    }
    private void ensureChildren() {
        showRoot = getOrCreateTransform(showRoot, "Show", true, false);
        hideRoot = getOrCreateTransform(hideRoot, "Hide", true, false);
        showVolumeRoot = getOrCreateTransform(showVolumeRoot, "ShowVolume", true, true);
        hideVolumeRoot = getOrCreateTransform(hideVolumeRoot, "HideVolume", true, true);
        showCameraWithinRoot = getOrCreateTransform(showCameraWithinRoot, "ShowCameraWithin", true, true);
        hideCameraWithinRoot = getOrCreateTransform(hideCameraWithinRoot, "HideCameraWithin", true, true);
        zcompressRoot = getOrCreateTransform(zcompressRoot, "ZCompress", true, false);
        if (this.transform.childCount > 3) {
            trashRoot = getOrCreateTransform(trashRoot, "__trash", false, true);
        }
        if (this.transform.childCount > 4) {
            for (int i = this.transform.childCount - 1; i >= 0; i--) {
                Transform t = this.transform.GetChild(i);
                if (t != showRoot && t != hideRoot && t != showVolumeRoot && t != hideVolumeRoot && t != showCameraWithinRoot && t != hideCameraWithinRoot && t != zcompressRoot && t != trashRoot) {
                    t.parent = trashRoot;
                }
            }
        }
    }

    private void calculateBoxParams(Transform targetTransform, Vector4[] o, ref int i, bool hasThickness, BoxCollider bc) {
        if (!bc.gameObject.activeInHierarchy) {
            return;
        }
        Transform quadPosition = bc.transform;
        if (bc == null){ 
            return;
        }
        if (i * 4 + 3 >= o.Length) {
            return;
        }
        for (int xsign = -1; xsign <= (bc.isTrigger ? 1 : 0); xsign += 2) {
            float xdiff = xsign * bc.size.x;
            float ydiff = bc.size.y;
            float zdiff = bc.size.z;

            Vector3 localPos = (scaleClipPlanes ? 1f/scale : 1f) * targetTransform.worldToLocalMatrix.MultiplyPoint(quadPosition.localToWorldMatrix.MultiplyPoint(
                new Vector3(bc.center.x - 0.5f * xdiff, bc.center.y - 0.5f * ydiff, bc.center.z)));
            Vector3 lineBA = (scaleClipPlanes ? 1f/scale : 1f) * targetTransform.worldToLocalMatrix.MultiplyVector(quadPosition.localToWorldMatrix.MultiplyVector(new Vector3(0f, ydiff, 0f)));
            Vector3 lineBAnorm = lineBA.normalized;

            Vector3 lineDA = (scaleClipPlanes ? 1f/scale : 1f) * targetTransform.worldToLocalMatrix.MultiplyVector(quadPosition.localToWorldMatrix.MultiplyVector(new Vector3(xdiff, 0f, 0f)));
            Vector3 lineDAnorm = lineDA.normalized;

            Vector3 lineNormal = (scaleClipPlanes ? 1f/scale : 1f) * targetTransform.worldToLocalMatrix.MultiplyVector(quadPosition.localToWorldMatrix.MultiplyVector(new Vector3(0f, 0f, zdiff)));
            // Vector3 planeNormal = Vector3.Cross(lineBA, lineDA).normalized;
            Vector3 planeNormal = lineNormal.normalized;
            if (!(planeNormal.sqrMagnitude > 0.5)) {
                planeNormal = new Vector3(0,1,0);
            }
            float BAscale = 1f / Vector3.Dot(lineBA, lineBAnorm);
            float DAscale = 1f / Vector3.Dot(lineDA, lineDAnorm);
            float NormalScale = Vector3.Dot(lineNormal, planeNormal);
            o[i * 4] = new Vector4(localPos.x, localPos.y, localPos.z, 1f);
            o[i * 4 + 1] = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, hasThickness ? NormalScale : bc.size.z);
            o[i * 4 + 2] = new Vector4(lineBAnorm.x, lineBAnorm.y, lineBAnorm.z, BAscale);
            o[i * 4 + 3] = new Vector4(lineDAnorm.x, lineDAnorm.y, lineDAnorm.z, DAscale);
            i++;
        }
    }

    private Vector4[] createBoxQuadsForTransforms(Transform targetTransform, Transform rectListParent, out int count, bool hasThickness) {
        Vector4[] o = new Vector4[64]; //rectListParent.childCount * 4];
        count = 0;
        BoxCollider []bcs = rectListParent.GetComponentsInChildren<BoxCollider>();
        for (int i = 0; count < 16 && i < bcs.Length; i++) {
            calculateBoxParams(targetTransform, o, ref count, hasThickness, bcs[i]);
        }
        return o;
    }

    public enum BoxClipArray {
        ClipShow,
        ClipHide,
        ShowVolume,
        HideVolume,
        ShowCameraWithin,
        HideCameraWithin,
        ZCompress
    }
    public Vector4[] createBoxQuads(Transform targetTransform, BoxClipArray arrayType, out int count) {
        Transform rectListParent = null;
        bool hasThickness = false;
        switch (arrayType) {
        case BoxClipArray.ClipShow:
            rectListParent = showRoot;
            break;
        case BoxClipArray.ClipHide:
            rectListParent = hideRoot;
            break;
        case BoxClipArray.ShowVolume:
            rectListParent = showVolumeRoot;
            hasThickness = true;
            break;
        case BoxClipArray.HideVolume:
            rectListParent = hideVolumeRoot;
            hasThickness = true;
            break;
        case BoxClipArray.ShowCameraWithin:
            rectListParent = showCameraWithinRoot;
            hasThickness = true;
            break;
        case BoxClipArray.HideCameraWithin:
            rectListParent = hideCameraWithinRoot;
            hasThickness = true;
            break;
        case BoxClipArray.ZCompress:
            rectListParent = zcompressRoot;
            break;
        }
        return createBoxQuadsForTransforms(targetTransform, rectListParent, out count, hasThickness);
    }

    private void updateUniforms() {
        if (!doPreview) {
            foreach (Renderer r in rendererObjects) {
                if (r == null) {
                    continue;
                }
                if (rendererToMPB.ContainsKey(r)) {
                    r.SetPropertyBlock(null);
                }
            }
            rendererToMPB.Clear();
            return;
        }
        foreach (Renderer r in rendererObjects) {
            if (r == null) {
                continue;
            }
            MaterialPropertyBlock mpb;
            if (!rendererToMPB.TryGetValue(r, out mpb)) {
                Debug.Log("Creating MaterialPropertyBlock " + mpb + " on renderer " + r.name, r);
                mpb = new MaterialPropertyBlock();
                rendererToMPB[r] = mpb;
            }
            Transform meshOrigin = r.transform;
            if (r is SkinnedMeshRenderer smr) {
                if (smr.rootBone != null) {
                    meshOrigin = smr.rootBone;
                }
            }
            int count;
            Vector4[] boxQuads = createBoxQuads(meshOrigin, BoxClipArray.ClipShow, out count);
            mpb.SetVectorArray("_BoxClipUniformClipShow", boxQuads);
            mpb.SetFloat("_BoxClipCountClipShow", count);
            boxQuads = createBoxQuads(meshOrigin, BoxClipArray.ClipHide, out count);
            mpb.SetVectorArray("_BoxClipUniformClipHide", boxQuads);
            mpb.SetFloat("_BoxClipCountClipHide", count);
            boxQuads = createBoxQuads(meshOrigin, BoxClipArray.ShowVolume, out count);
            mpb.SetVectorArray("_BoxClipUniformShowVolume", boxQuads);
            mpb.SetFloat("_BoxClipCountShowVolume", count);
            boxQuads = createBoxQuads(meshOrigin, BoxClipArray.HideVolume, out count);
            mpb.SetVectorArray("_BoxClipUniformHideVolume", boxQuads);
            mpb.SetFloat("_BoxClipCountHideVolume", count);
            boxQuads = createBoxQuads(meshOrigin, BoxClipArray.ShowCameraWithin, out count);
            mpb.SetVectorArray("_BoxClipUniformShowCameraWithin", boxQuads);
            mpb.SetFloat("_BoxClipCountShowCameraWithin", count);
            boxQuads = createBoxQuads(meshOrigin, BoxClipArray.HideCameraWithin, out count);
            mpb.SetVectorArray("_BoxClipUniformHideCameraWithin", boxQuads);
            mpb.SetFloat("_BoxClipCountHideCameraWithin", count);
            boxQuads = createBoxQuads(meshOrigin, BoxClipArray.ZCompress, out count);
            mpb.SetVectorArray("_BoxClipUniformZCompress", boxQuads);
            mpb.SetFloat("_BoxClipCountZCompress", count);

            mpb.SetFloat("_BoxClipScale", scale);
            // mpb.SetVector("_BoxClipBillboard", new Vector4(billboardOrigin.x, billboardOrigin.y, billboardOrigin.z, enableBillboard ? 1f : 0f));
            // mpb.SetFloat("_BoxClipBillboardVertical", enableBillboardVertical ? 1f : 0f);
            // mpb.SetFloat("_BoxClipBillboardWorldSpace", enableBillboardWorldSpace ? 1f : 0f);
            mpb.SetFloat("_BoxClipAllowInFront", allowInFront);
            r.SetPropertyBlock(mpb);
        }
    }

    public void Awake() {
        ensureChildren();
        updateUniforms();
    }

    public void Update() {
        ensureChildren();
        updateUniforms();
        if (!this.gameObject.CompareTag("EditorOnly")) {
            this.gameObject.tag = "EditorOnly";
        }
    }

    void drawGizmosTransforms(Transform root, Color color) {
        Color old = Gizmos.color;
        Matrix4x4 oldmat = Gizmos.matrix;
        Gizmos.color = color;
        BoxCollider[] bcs = root.GetComponentsInChildren<BoxCollider>();
        for (int i = 0; i < bcs.Length; i++) {
            BoxCollider bc = bcs[i];
            Gizmos.matrix = bc.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bc.center, bc.size);
        }
        Gizmos.color = old;
        Gizmos.matrix = oldmat;
    }

    void OnDrawGizmos() {
        if (doPreview) {
            OnDrawGizmosSelected();
        }
    }

    void OnDrawGizmosSelected() {
        drawGizmosTransforms(showRoot, Color.green);
        drawGizmosTransforms(hideRoot, new Color(0.3f,0f,0.1f,1f));
        drawGizmosTransforms(showVolumeRoot, new Color(0.4f,0.9f,1.0f,1f));
        drawGizmosTransforms(hideVolumeRoot, new Color(0.8f,0.3f,0.4f,1f));
        drawGizmosTransforms(showCameraWithinRoot, new Color(0.8f,1.0f,1.0f,1f));
        drawGizmosTransforms(hideCameraWithinRoot, new Color(1.0f,0.9f,0.8f,1f));
        drawGizmosTransforms(zcompressRoot, Color.yellow);
    }

#endif
}