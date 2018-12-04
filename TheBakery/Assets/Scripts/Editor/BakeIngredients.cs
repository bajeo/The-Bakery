using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace TheBakery
{
    public class BakeIngredients
    {
        public Dictionary<Material, MeshIngredients> DictOfIngredients;
        private List<string> TextureTags = new List<string>() { "_alb", "_nrm", "_occ", "_PBR", "_emission", "_dtm", "_dtn" };
        public int GetMaterialCount => DictOfIngredients.Keys.Count;
        private List<string> m_TextureToProcess;
        private int m_iResize;
        private int m_iHighestLOD = -1;

        public int GetHighestLOD => m_iHighestLOD;

        public List<Material> GetMaterials()
        {
            List<Material> mats = new List<Material>();
            mats.AddRange(DictOfIngredients.Keys);
            return mats;
        }

        public BakeIngredients(List<string> textures, int Resize)
        {
            m_TextureToProcess = textures;
            m_iResize = Resize;
            DictOfIngredients = new Dictionary<Material, MeshIngredients>();
        }

        public void AddIngredients(int iLOD, Material[] materials, Mesh mesh, Transform location, TheBakery.BakeryProgress progress)
        {
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Material mat = materials[i];
                if (false == DictOfIngredients.ContainsKey(mat))
                {
                    DictOfIngredients.Add(mat, new MeshIngredients(mat, m_TextureToProcess, m_iResize, progress));
                }

                //Keep record of highest LOD we process
                m_iHighestLOD = Mathf.Max(m_iHighestLOD, iLOD);
                //Add each submesh to the system
                DictOfIngredients[mat].AddMesh(mesh, iLOD, i, location);
            }
        }

        public int CountSteps(Transform transform)
        {
            int iCount = 0;

            List<Material> materials = new List<Material>();
            Renderer[] renderers = transform.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer.transform.GetComponent<MeshFilter>();
                Material[] mats = renderer.sharedMaterials;
                materials.AddRange(mats);
                materials = materials.Distinct().ToList();

                //Processing SubMeshes
                iCount += filter.sharedMesh.subMeshCount;
            }

            int iTexturesToMake = 0;
            for (int i = 0; i < materials.Count; i++)
            {
                int iTextures = MaterialData.GetProcessingCount(materials[i], m_TextureToProcess);
                iTexturesToMake = Mathf.Max(iTexturesToMake, iTextures);
                //Importing materials and restoring
                iCount += (iTextures * 3);
            }

            //Exported textures
            iCount += iTexturesToMake;

            return iCount;
        }

        public void ExtractParts(Transform transform, TheBakery.BakeryProgress progress)
        {
            LODCache lodCache = new LODCache(transform.gameObject);
            Renderer[] renderers = transform.GetComponentsInChildren<Renderer>();

            progress.Title = "Processing";
            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer.transform.GetComponent<MeshFilter>();
                Material[] mats = renderer.sharedMaterials;

                Mesh mesh = filter.sharedMesh;
                if (mesh.subMeshCount != mats.Length)
                {
                    Debug.LogWarning("Model Fault detected: Submesh count != material count: " + renderer.transform.name);
                }
                else
                {
                    int iLOD = lodCache.GetLODGroup(mesh);
                    AddIngredients(iLOD, mats, mesh, renderer.transform, progress);
                }
            }
        }

        private List<string> GetTexturesToBake(List<string> textureList)
        {
            List<string> usedTextures = new List<string>();
            foreach (Material mat in DictOfIngredients.Keys)
            {
                usedTextures.AddRange(DictOfIngredients[mat].GetTextureList());
            }

            usedTextures = usedTextures.Distinct().ToList();
            usedTextures = usedTextures.Where(x => textureList.Contains(x)).ToList();
            return usedTextures;
        }

        public ExportedTextureSet Bake(TheBakery.BakeryProgress progress)
        {
            progress.Title = "Baking...";
            int iSections = GetMaterialCount;
            int width = (int)Mathf.Ceil(Mathf.Sqrt((float)iSections));
            int height = (int)Mathf.Ceil(((float)iSections / (float)width));
            int pixelWidth = NextPowerOfTwo(width * m_iResize);
            int pixelHeight = NextPowerOfTwo(height * m_iResize);
            //Width and height in tiles
            int GridWidth = pixelWidth / m_iResize;
            int GridHeight = pixelHeight / m_iResize;

            List<string> texturesToBake = GetTexturesToBake(m_TextureToProcess);

            ExportedTextureSet exportedTexture = new ExportedTextureSet(texturesToBake, TextureTags, pixelWidth, pixelHeight);
            int iCount = 0;

            foreach (KeyValuePair<Material, MeshIngredients> kvp in DictOfIngredients)
            {
                kvp.Value.BakeTextures(exportedTexture, ref iCount, m_iResize, GridWidth, GridHeight, progress);
                kvp.Value.BakeMeshes(progress);
                iCount++;
            }

            return exportedTexture;
        }

        public List<CombineInstance> CombineModels(int iLOD, Transform rootPosition)
        {
            //List<Mesh> bakedMeshes = 
            var combineAble = new List<CombineInstance>();
            foreach (KeyValuePair<Material, MeshIngredients> kvp in DictOfIngredients)
            {
                kvp.Value.CombineModels(iLOD, rootPosition, combineAble);
                //Combine all none lod models
                kvp.Value.CombineModels(-1, rootPosition, combineAble);
            }
            return combineAble;
        }

        public void Restore(TheBakery.BakeryProgress progress)
        {
            progress.Title = "Restoring";
            //Restore all materials and textures to original states
            foreach (KeyValuePair<Material, MeshIngredients> kvp in DictOfIngredients)
            {
                kvp.Value.Restore(progress);
            }
        }

        private int NextPowerOfTwo(int x)
        {
            x--; // comment out to always take the next biggest power of two, even if x is already a power of two
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (x + 1);
        }
    }
}
