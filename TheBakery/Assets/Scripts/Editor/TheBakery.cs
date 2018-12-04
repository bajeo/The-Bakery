using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace TheBakery
{
    public class TheBakery : ScriptableWizard
    {
        public class BakeryProgress
        {
            public int iSteps;
            private int iCurrentStep;
            public string Title;
            public string Details;

            public void IncrementStep()
            {
                iCurrentStep++;
                if (iCurrentStep < iSteps)
                {
                    UpdateBar();
                }
                else
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            public void UpdateBar()
            {
                float lerp = Mathf.InverseLerp(0, iSteps, iCurrentStep);
                EditorUtility.DisplayProgressBar(Title, Details, lerp);
            }
        }

        public enum TextureSize : int
        {
            Tex_512 = 512,
            Tex_1024 = 1024,
            Tex_2048 = 2048,
            Tex_4096 = 4096,
        }

        [Tooltip("Should the result be applied to the output mesh")]
        public bool ApplyToTarget = true;

        [Tooltip("Should none lod objects be added into each LOD layer?")]
        public bool AddInNoneLODs = true;

        private const int MAX_VERTEX_COUNT = 65000;

        public TextureSize ResizeTexture = TheBakery.TextureSize.Tex_1024;

        private List<string> TexturesToSearch = new List<string>() { "_MainTex", "_BumpMap", "_OcclusionMap",
                                                                "_MetallicGlossMap", "_EmissionMap",
                                                                "_DetailAlbedoMap", "_DetailNormalMap" };

        private List<string> TextureTags = new List<string>() { "_alb", "_nrm", "_occ", "_PBR", "_emission", "_dtm", "_dtn" };

        private Dictionary<Mesh, List<Transform>> DictOfMeshLocations = new Dictionary<Mesh, List<Transform>>();
        private BakeIngredients m_BakedIngredients;

        [MenuItem("Tool/Mesh Bakery &B")]
        private static void ShowEdit()
        {
            DisplayWizard("Mesh Bakery", typeof(TheBakery), "Bake");
        }

        private void OnWizardCreate()
        {
            var meshPath = EditorUtility.SaveFilePanelInProject("Save new Mesh", "BakedMesh", "Asset", "Enter file name");

            if (string.Empty == meshPath)
                return;

            string assetName = System.IO.Path.GetFileNameWithoutExtension(meshPath);
            string directoryPath = meshPath.Replace(assetName, "");
            BakeModel(directoryPath, assetName);
        }

        private void BakeModel(string savePath, string assetName)
        {
            BakeryProgress progress = new BakeryProgress();

            GameObject meshesRoot = Selection.activeGameObject;

            savePath = savePath.Replace(".Asset", "");

            m_BakedIngredients = new BakeIngredients(TexturesToSearch, (int)ResizeTexture);
            progress.iSteps = m_BakedIngredients.CountSteps(meshesRoot.transform);
            m_BakedIngredients.ExtractParts(meshesRoot.transform, progress);

            ExportedTextureSet exportedTexture = m_BakedIngredients.Bake(progress);
            
            m_BakedIngredients.Restore(progress);

            List<Material> mats = m_BakedIngredients.GetMaterials();
            Material combinedMaterial = new Material(mats[0].shader);
            exportedTexture.ApplyToMaterial(combinedMaterial);

            string materialPath = savePath + assetName + ".mat";
            AssetDatabase.CreateAsset(combinedMaterial, materialPath);

            exportedTexture.FinaliseTexture(".png", savePath, assetName, progress);

            GameObject target = new GameObject(assetName);
            target.transform.position = meshesRoot.transform.position;
            target.transform.rotation = meshesRoot.transform.rotation;

            CreateModel(m_BakedIngredients, meshesRoot, target, combinedMaterial, savePath, assetName);

            

            AssetDatabase.Refresh();
        }

        private void CreateModel(BakeIngredients m_BakedIngredients, GameObject meshesRoot, GameObject target, 
            Material material, string savePath, string assetName)
        {
            LODGroup lodGroup = null;
            LOD[] lods = null;

            bool hasLODS = m_BakedIngredients.GetHighestLOD > 0;
            if (true == hasLODS)
            {
                lodGroup = target.ForceComponent<LODGroup>();
                lods = new LOD[m_BakedIngredients.GetHighestLOD + 1];
            }

            for (int i = 0; i < (m_BakedIngredients.GetHighestLOD + 1); i++)
            {
                GameObject parent = target;
                int iLOD = i;
                string assetPath = savePath + assetName + ".asset";
                if (true == hasLODS)
                {
                    parent = new GameObject(assetName + "_LOD" + iLOD);
                    parent.transform.SetParent(target.transform);
                    parent.transform.localPosition = Vector3.zero;
                    parent.transform.localRotation = Quaternion.identity;

                    assetPath = savePath + assetName + "_LOD" + iLOD + ".asset";
                }

                var combines = m_BakedIngredients.CombineModels(iLOD, meshesRoot.transform);
                Debug.Log("Combining: " + combines.Count + " for LOD: " + iLOD);
                var newMesh = new Mesh();
                newMesh.CombineMeshes(combines.ToArray(), true);
                AssetDatabase.CreateAsset(newMesh, assetPath);
                CreateMeshAtTarget(parent, material, newMesh);

                if (true == hasLODS)
                {
                    lods[iLOD].renderers = new Renderer[1] { parent.GetComponent<Renderer>() };
                    lods[iLOD].screenRelativeTransitionHeight = 1f - ((iLOD + 1f) / (m_BakedIngredients.GetHighestLOD + 1f));
                }
            }

            if (true == hasLODS)
            {
                lods[m_BakedIngredients.GetHighestLOD - 1].screenRelativeTransitionHeight = 0.1f;
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }

        private void CreateMeshAtTarget(GameObject target, Material mat, Mesh mesh)
        {
            var filter = target.ForceComponent<MeshFilter>();
            var combinedRenderer = target.ForceComponent<MeshRenderer>();
            filter.mesh = mesh;
            combinedRenderer.material = mat;
        }
    }
}