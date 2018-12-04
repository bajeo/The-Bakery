using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheBakery
{
    public class SubMeshIngredient
    {
        private Dictionary<int, List<Transform>> m_DictOfSubMeshLocations = new Dictionary<int, List<Transform>>();
        private Dictionary<int, Mesh> m_DictOfSubmeshes = new Dictionary<int, Mesh>();
        private Mesh m_Mesh = null;

        public SubMeshIngredient(Mesh mesh)
        {
            m_Mesh = mesh;
        }

        public void Add(int iSubmesh, Transform location)
        {
            if (false == m_DictOfSubMeshLocations.ContainsKey(iSubmesh))
            {
                m_DictOfSubMeshLocations.Add(iSubmesh, new List<Transform>());
                m_DictOfSubmeshes.Add(iSubmesh, m_Mesh.GetSubmesh(iSubmesh));
            }

            m_DictOfSubMeshLocations[iSubmesh].Add(location);
        }

        public void Combine(Transform rootPosition, List<CombineInstance> combines)
        {
            foreach (KeyValuePair<int, List<Transform>> kvp in m_DictOfSubMeshLocations)
            {
                for (int x = 0; x < kvp.Value.Count; x++)
                {
                    CombineInstance instance = new CombineInstance();
                    Transform transform = kvp.Value[x];

                    instance.mesh = m_DictOfSubmeshes[kvp.Key];
                    instance.transform = Matrix4x4.TRS(transform.position - rootPosition.position,
                    transform.rotation, transform.lossyScale);

                    combines.Add(instance);
                }
            }
        }

        public void BakeModel(Vector2 startUV, Vector2 endUV, TheBakery.BakeryProgress progress)
        {
            Vector2 minUV = startUV;
            Vector2 maxUV = endUV;
            foreach (KeyValuePair<int, Mesh> kvp in m_DictOfSubmeshes)
            {
                progress.Details = "model: " + kvp.Value.name;
                progress.IncrementStep();
                Mesh mesh = kvp.Value;
                List<int> triList = new List<int>();
                mesh.GetTriangles(triList, 0);
                Vector2[] uvs = mesh.uv;
                Vector2[] bakedUV = new Vector2[mesh.uv.Length];

                foreach (int x in triList)
                {
                    Vector2 uv = uvs[x];
                    uv.x = Mathf.Lerp(minUV.x, maxUV.x, uv.x);
                    uv.y = Mathf.Lerp(minUV.y, maxUV.y, uv.y);
                    bakedUV[x] = uv;
                }

                mesh.uv = bakedUV;
            }
        }

        private Mesh BakeSubmesh(int iSubmesh, Vector2 startUV, Vector2 endUV)
        {
            Mesh BakedMesh = new Mesh
            {
                name = m_Mesh.name,
                vertices = m_Mesh.vertices,
                triangles = m_Mesh.triangles,
                normals = m_Mesh.normals,
                tangents = m_Mesh.tangents,
                colors = m_Mesh.colors,
                subMeshCount = m_Mesh.subMeshCount
            };

            Vector2[] bakedUV = new Vector2[m_Mesh.uv.Length];
            Vector2[] uvs = m_Mesh.uv;

            List<int> keys = new List<int>();
            keys.AddRange(m_DictOfSubMeshLocations.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                int iIndex = keys[i];
                Vector2 minUV = startUV;
                Vector2 maxUV = endUV;
                List<int> triList = new List<int>();
                m_Mesh.GetTriangles(triList, iIndex);

                foreach (int x in triList)
                {
                    Vector2 uv = uvs[x];
                    uv.x = Mathf.Lerp(minUV.x, maxUV.x, uv.x);
                    uv.y = Mathf.Lerp(minUV.y, maxUV.y, uv.y);
                    bakedUV[x] = uv;
                }
            }
            BakedMesh.uv = bakedUV;

            return BakedMesh;
        }
    }
}