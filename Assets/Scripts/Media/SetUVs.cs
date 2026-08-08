using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SetUVs : MonoBehaviour
{
    [UnityEngine.Range(0, 256)]
    public int index = 0;

    void OnValidate() 
    {
        List<Vector3> uvs = new List<Vector3>();
        Mesh mesh = this.GetComponent<MeshFilter>().mesh;
        mesh.GetUVs(0, uvs);
        for (int i = 0; i < uvs.Count; i++)
            uvs[i] = new Vector3(uvs[i].x, uvs[i].y, index);

        mesh.SetUVs(0, uvs);
    }
}
