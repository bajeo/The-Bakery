using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheBakery
{
    public class MeshIngredients
    {
        private Dictionary<Mesh, MeshLODS> MeshLODs = new Dictionary<Mesh, MeshLODS>();

        private MaterialData m_MaterialData;

        public List<string> GetTextureList()
        {
            return m_MaterialData.GetTextureList();
        }

        public MeshIngredients(Material material, List<string> texturesToProcess, int m_iResize, TheBakery.BakeryProgress progress)
        {
            //Process the textures to make them readable etc...
            m_MaterialData = new MaterialData(material, texturesToProcess, m_iResize, true, progress);
        }

        public void AddMesh(Mesh mesh, int iLOD, int iSubMesh, Transform location)
        {
            if (false == MeshLODs.ContainsKey(mesh))
            {
                MeshLODs.Add(mesh, new MeshLODS(mesh));
            }

            MeshLODs[mesh].AddLODMesh(iLOD, iSubMesh, location);
        }

        public void BakeTextures(ExportedTextureSet exportedTexture, ref int index, int TextureResize, int iGridWidth, int iGridHeight, TheBakery.BakeryProgress progress)
        {
            if (false == m_MaterialData.ValidTextures())
            {
                //We will need to recalculate bounds for internal uving
                Debug.Log("Unable to bake object: texture sizes are different");
                return;
            }

            int m_iMaterialTextureWidth = -1;
            int m_iMaterialTextureHeight = -1;

            m_MaterialData.GetTextureSizes(out m_iMaterialTextureWidth, out m_iMaterialTextureHeight);

            int ActualTextureWidth = Mathf.Min(TextureResize, m_iMaterialTextureWidth);
            int ActualTextureHeight = Mathf.Min(TextureResize, m_iMaterialTextureHeight);

            m_MaterialData.CalculateUVOffset(index, TextureResize, TextureResize, iGridWidth, iGridHeight, ActualTextureWidth, ActualTextureHeight);
            m_MaterialData.BakeTexture(exportedTexture, ActualTextureWidth, ActualTextureHeight, progress);
        }

        public void BakeMeshes(TheBakery.BakeryProgress progress)
        {
            foreach (KeyValuePair<Mesh, MeshLODS> kvp in MeshLODs)
            {
                kvp.Value.BakeLODMeshes(m_MaterialData.GetStartUV, m_MaterialData.GetEndUV, progress);
            }
        }

        public void CombineModels(int iLOD, Transform rootPosition, List<CombineInstance> combines)
        {
            foreach (KeyValuePair<Mesh, MeshLODS> kvp in MeshLODs)
            {
                kvp.Value.CombineLODMeshes(iLOD, rootPosition, combines);//.Combine(rootPosition, combines);
            }
        }

        public void Restore(TheBakery.BakeryProgress progress)
        {
            m_MaterialData.Restore(progress);
        }
    }
}