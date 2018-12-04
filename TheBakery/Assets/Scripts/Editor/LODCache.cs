using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TheBakery
{
    public class LODCache
    {
        private Dictionary<Mesh, int> DictOfLODGroups = new Dictionary<Mesh, int>();
        private Dictionary<int, List<Mesh>> DictOfMeshesPerLOD = new Dictionary<int, List<Mesh>>();

        public int GetLODGroup(Mesh mesh)
        {
            if (true == DictOfLODGroups.ContainsKey(mesh))
            {
                return DictOfLODGroups[mesh];
            }

            return -1;
        }

        public List<Mesh> GetMeshesForLOD(int iLOD)
        {
            if (true == DictOfMeshesPerLOD.ContainsKey(iLOD))
            {
                return DictOfMeshesPerLOD[iLOD];
            }

            return null;
        }

        public LODCache(GameObject obj)
        {
            LODGroup[] LodGroup = obj.GetComponentsInChildren<LODGroup>();

            if (null != LodGroup)
            {
                foreach (LODGroup group in LodGroup)
                {
                    LOD[] lods = group.GetLODs();
                    for (int i = 0; i < lods.Length; i++)
                    {
                        if (false == DictOfMeshesPerLOD.ContainsKey(i))
                        {
                            DictOfMeshesPerLOD.Add(i, new List<Mesh>());
                        }
                        Renderer[] renderers = lods[i].renderers;

                        foreach (Renderer render in renderers)
                        {
                            if (null != render)
                            {
                                MeshFilter filter = render.GetComponent<MeshFilter>();

                                DictOfMeshesPerLOD[i].Add(filter.sharedMesh);
                                if (false == DictOfLODGroups.ContainsKey(filter.sharedMesh))
                                {
                                    DictOfLODGroups.Add(filter.sharedMesh, i);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}