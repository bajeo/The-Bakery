using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheBakery
{
    public class MeshLODS
    {
        private Dictionary<int, SubMeshIngredient> DictOfLODMeshes = new Dictionary<int, SubMeshIngredient>();
        private Mesh m_Mesh;

        public MeshLODS(Mesh mesh)
        {
            m_Mesh = mesh;
        }

        public void AddLODMesh(int iLOD, int iSubmesh, Transform location)
        {
            if (false == DictOfLODMeshes.ContainsKey(iLOD))
            {
                DictOfLODMeshes.Add(iLOD, new SubMeshIngredient(m_Mesh));
            }

            DictOfLODMeshes[iLOD].Add(iSubmesh, location);
        }

        public void CombineLODMeshes(int iLOD, Transform rootPosition, List<CombineInstance> combines)
        {
            if (DictOfLODMeshes.ContainsKey(iLOD))
            {
                DictOfLODMeshes[iLOD].Combine(rootPosition, combines);
            }
        }

        public void BakeLODMeshes(Vector2 startUV, Vector2 endUV, TheBakery.BakeryProgress progress)
        {
            foreach (KeyValuePair<int, SubMeshIngredient> kvp in DictOfLODMeshes)
            {
                kvp.Value.BakeModel(startUV, endUV, progress);
            }
        }
    }
}