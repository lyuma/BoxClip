using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//#if UNITY_EDITOR
public class LyumaMeshUtility {

    public static Bounds expandBounds(Transform origin, Transform localTransform, Bounds localBounds) {
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;
        Matrix4x4 smrToOrigin = origin.worldToLocalMatrix * localTransform.localToWorldMatrix;
        float dist = 0;
        for (int i = 0; i < 8; i++) {
            float newdist = smrToOrigin.MultiplyPoint(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)).magnitude;
            if (newdist > dist) { 
                dist = newdist;
            }
        }
        // worst case distance (wasteful).
        return new Bounds(Vector3.zero, new Vector3(dist * 1.414f, dist * 1.414f, dist * 1.414f));
    }


    public static void RecenterMesh (MeshRenderer mr, Transform targetTransform, string basepath) {
        Mesh sourceMesh;
        MeshFilter mf = mr.transform.GetComponent<MeshFilter> ();
        Transform trans = mr.transform;
        sourceMesh = mf.sharedMesh;
        Matrix4x4 relativeMatrix = targetTransform.worldToLocalMatrix * trans.localToWorldMatrix;
        Transform oldParent = trans.parent;
        Undo.RecordObject (trans, "Switched MeshFilter to adjusted");
        trans.parent = targetTransform;
        trans.localPosition = Vector3.zero;
        trans.localRotation = Quaternion.identity;
        trans.localScale = Vector3.one;
        trans.parent = oldParent;
        Mesh newMesh = new Mesh ();
        newMesh.name = sourceMesh.name + "Adjusted";
        Vector3[] vertices = sourceMesh.vertices;
        for (int i = 0; i < vertices.Length; i++) {
            vertices[i] = relativeMatrix.MultiplyPoint(vertices[i]);
        }
        newMesh.vertices = vertices;
        Vector3[] normals = sourceMesh.normals;
        for (int i = 0; i < normals.Length; i++) {
            normals[i] = relativeMatrix.MultiplyVector(normals[i]);
        }
        newMesh.normals = normals;
        Vector4[] tangents = sourceMesh.tangents;
        Vector3 tmp = new Vector3();
        for (int i = 0; i < tangents.Length; i++) {
            tmp.x = tangents[i].x;
            tmp.y = tangents[i].y;
            tmp.z = tangents[i].z;
            tmp = relativeMatrix.MultiplyVector(tmp);
            tangents[i] = new Vector4(tmp.x, tmp.y, tmp.z, tangents[i].w);
        }
        newMesh.tangents = tangents;
        newMesh.colors = sourceMesh.colors;
        List<Vector4> uvList = new List<Vector4> ();
        for (int i = 0; i < 4; i++) {
            sourceMesh.GetUVs (i, uvList);
            if (uvList.Count() > 0) {
                newMesh.SetUVs(i, uvList);
            }
        }

        newMesh.subMeshCount = sourceMesh.subMeshCount;
        newMesh.bounds = expandBounds(targetTransform, trans, sourceMesh.bounds);
        newMesh.indexFormat = sourceMesh.indexFormat;
        int whichSubMesh = 0;
        for (int i = 0; i < sourceMesh.subMeshCount; i++) {
            var curIndices = sourceMesh.GetIndices (i);
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology (i), whichSubMesh, false, (int)sourceMesh.GetBaseVertex(i));
            whichSubMesh++;
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to adjusted");
            mf.sharedMesh = newMesh;
        }
        // if (!Directory.Exists (pathToGenerated)) {
        //     Directory.CreateDirectory (pathToGenerated);
        // }
        string fileName = basepath + "/" + newMesh.name + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (newMesh, fileName);
        AssetDatabase.SaveAssets ();
    }
}
//#endif