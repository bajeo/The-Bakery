using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MeshCombiner : ScriptableWizard
{
    public enum TextureSize : int
    {
        Tex_1024 = 1024,
        Tex_2048 = 2048,
        Tex_4096 = 4096,
    }

    [Tooltip("Root gameobject of meshes.")]
    public GameObject meshesRoot;

    [Tooltip("Gameobject to save new combine mesh.")]
    public GameObject meshSave;

    public TextureSize ResizeTexture = MeshCombiner.TextureSize.Tex_1024;

    [Tooltip("Save each LOD combined.")]
    public bool combineLODs;

    private const int TriangleLimit = 65000;

    [MenuItem("Tool/Mesh Combiner &K")]
    private static void ShowEdit()
    {
        DisplayWizard("Mesh Combiner", typeof(MeshCombiner), "Combine");
    }

    private void OnWizardCreate()
    {
        var meshPath = EditorUtility.SaveFilePanelInProject("Save new Mesh", "CombinedMesh", "Asset", "Enter file name");

        if (string.Empty == meshPath)
            return;

        if(true == HasLods())
        {
            ProcessLODModel(meshPath);
        }
        else
        {
            ProcessNoneLodModel(meshPath);
        }        
    }

    private void ProcessLODModel(string meshPath)
    {
        var lodMeshes = meshesRoot.GetComponentsInChildren<LODGroup>();
        Dictionary<int, List<Renderer>> dictOfLodsAndRenderes = new Dictionary<int, List<Renderer>>();
        float[] lodValues = new float[lodMeshes.Length];
        for(int i = 0; i < lodMeshes.Length; i++)
        {
            //Gather renderes for each lod
            LOD[] lods = lodMeshes[i].GetLODs();
            for (int x = 0; x < lods.Length; x++)
            {
                lodValues[x] = lods[x].screenRelativeTransitionHeight;
                if (false == dictOfLodsAndRenderes.ContainsKey(x))
                {
                    dictOfLodsAndRenderes.Add(x, new List<Renderer>());
                }

                dictOfLodsAndRenderes[x].AddRange(lods[x].renderers);
            }
        }

        LODGroup lodGroup = meshSave.ForceComponent<LODGroup>();
        lodGroup.SetLODs(new LOD[0]);

        foreach (KeyValuePair<int, List<Renderer>> kvp in dictOfLodsAndRenderes)
        {
            //Combine this lod together into a single mesh
            var combines = new CombineInstance[kvp.Value.Count];
            var materialList = new List<Material>();
            List<Renderer> renderers = kvp.Value;
            for (int  i = 0; i < kvp.Value.Count; i++)
            {
                Transform transform = renderers[i].transform;
                MeshFilter meshFilter = renderers[i].GetComponent<MeshFilter>();
                combines[i].mesh = meshFilter.sharedMesh;
                combines[i].transform = Matrix4x4.TRS(transform.position - meshesRoot.transform.position,
                transform.rotation, transform.lossyScale);

                var materials = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
                foreach (var mat in materials)
                {
                    if (null != mat &&
                        false == materialList.Contains(mat))
                    {
                        materialList.Add(mat);
                    }
                }
            }

            var newMesh = new Mesh();
            Debug.Log("Combining LOD: " + kvp.Key + " | " + combines.Length + " models");
            newMesh.CombineMeshes(combines, true);

            //Create in scene
            GameObject child = CreateTransform(meshSave, newMesh, lodMeshes[kvp.Key], materialList, "LOD" + kvp.Key);

            string saveName = meshPath.Replace(".Asset", "_LOD" + kvp.Key + ".Asset");
            AssetDatabase.CreateAsset(newMesh, saveName);
            AssetDatabase.Refresh();
            Selection.activeObject = newMesh;

            Renderer[] combinedRenderers = child.GetComponents<Renderer>();

            List<LOD> lods = new List<LOD>(lodGroup.GetLODs());
            lods.Add(new LOD(lodValues[kvp.Key], combinedRenderers));
            lodGroup.SetLODs(lods.ToArray());
        }
    }

    private GameObject CreateTransform(GameObject parent, Mesh mesh, LODGroup lodData, List<Material> materials, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;

        var filter = child.ForceComponent<MeshFilter>();
        var renderer = child.ForceComponent<MeshRenderer>();

        filter.sharedMesh = mesh;
        renderer.sharedMaterials = materials.ToArray();

        return child;
    }

    private class ModelData
    {
        public Transform transform;
        public MeshFilter filter;
        public MeshRenderer meshRenderer;
        public Material[] materials;
        public Mesh mesh;
        public Texture2D albedo;
        public Texture2D specular;
        public Texture2D occlusion;
        public Texture2D normal;
        public Texture2D emission;
        public Vector2 startUVs;
        public Vector2 endUVs;
        private int OriginalTextureSize;

        public ModelData(Transform obj, int ResizeTexture)
        {
            transform = obj;

            filter = transform.GetComponent<MeshFilter>();
            mesh = filter.sharedMesh;
            meshRenderer = transform.GetComponent<MeshRenderer>();
            materials = meshRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                albedo = materials[i].GetTexture("_MainTex") as Texture2D;
                normal = materials[i].GetTexture("_BumpMap") as Texture2D;
                occlusion = materials[i].GetTexture("_OcclusionMap") as Texture2D;
                specular = materials[i].GetTexture("_MetallicGlossMap") as Texture2D;
                emission = materials[i].GetTexture("_EmissionMap") as Texture2D;

                SetupTextureImporter(albedo, ResizeTexture);
                SetupTextureImporter(normal, ResizeTexture, true);
                SetupTextureImporter(occlusion, ResizeTexture);
                SetupTextureImporter(specular, ResizeTexture);
                SetupTextureImporter(emission, ResizeTexture);
            }
            startUVs = Vector2.zero;
            endUVs = Vector2.one;
        }

        private void SetupTextureImporter(Texture2D texture, int ResizeTexture, bool isNormal = false)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importedNormal = (TextureImporter)TextureImporter.GetAtPath(path);
            if (null != importedNormal)
            {
                importedNormal.isReadable = true;
                if (isNormal)
                {
                    importedNormal.textureType = TextureImporterType.Default;
                }
                OriginalTextureSize = importedNormal.maxTextureSize;
                importedNormal.maxTextureSize = ResizeTexture;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
            }
        }

        private void CleanupTextureImporter(Texture2D texture, bool isNormal = false)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importedNormal = (TextureImporter)TextureImporter.GetAtPath(path);
            if (null != importedNormal)
            {
                importedNormal.isReadable = false;
                if (isNormal)
                {
                    importedNormal.textureType = TextureImporterType.NormalMap;
                }
                importedNormal.maxTextureSize = OriginalTextureSize;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
            }
        }

        public void RevertTemporaryChanges()
        {
            CleanupTextureImporter(albedo);
            CleanupTextureImporter(normal, true);
            CleanupTextureImporter(occlusion);
            CleanupTextureImporter(specular);
            CleanupTextureImporter(emission);
        }
    }

    private List<CombineInstance> CombineModels(Dictionary<Mesh, List<Transform>> models)
    {
        var combineAble = new List<CombineInstance>();
        foreach(KeyValuePair<Mesh, List<Transform>> kvp in models)
        {
            CombineInstance instance = new CombineInstance();

            for (int i = 0; i < kvp.Value.Count; i++)
            {
                Transform transform = kvp.Value[i].transform;
                instance.mesh = kvp.Key;
                instance.transform = Matrix4x4.TRS(transform.position - meshesRoot.transform.position,
                    transform.rotation, transform.lossyScale);

                combineAble.Add(instance);
            }
        }
        return combineAble;
    }

    private List<CombineInstance> CombineModels(List<ModelData> models)
    {
        var combineAble = new List<CombineInstance>();
        for (int i = 0; i < models.Count; i++)
        {
            CombineInstance instance = new CombineInstance();

            Transform transform = models[i].transform;
            instance.mesh = models[i].mesh;
            instance.transform = Matrix4x4.TRS(transform.position - meshesRoot.transform.position,
                transform.rotation, transform.lossyScale);

            combineAble.Add(instance);
        }

        return combineAble;
    }

    private List<Material> GetUniqueMaterials(List<ModelData> models)
    {
        var materialList = new List<Material>();

        for (int i = 0; i < models.Count; i++)
        {
            foreach (var mat in models[i].materials)
            {
                if (null != mat)
                {
                    if (false == materialList.Contains(mat))
                    {
                        materialList.Add(mat);
                    }
                }
            }
        }
        return materialList;
    }

    private Dictionary<Mesh, List<Transform>> MeshFilterLocations = new Dictionary<Mesh, List<Transform>>();

    private bool ToLargeForSingleMesh(Dictionary<Mesh, List<Transform>> dic)
    {
        int iCount = 0;

        foreach(KeyValuePair<Mesh, List<Transform>> kvp in dic)
        {
            iCount += (kvp.Key.vertexCount * kvp.Value.Count);
        }

        Debug.Log("Vertex limit of model to be made: " + iCount);
        return iCount < TriangleLimit;
    }

    private void ProcessNoneLodModel(string meshPath)
    {
        MeshFilterLocations.Clear();
        var meshFilters = meshesRoot.GetComponentsInChildren<MeshFilter>();

        List<ModelData> modelData = new List<ModelData>();

        //For each mesh filter process unique and store all reference positions
        for(int i = 0; i < meshFilters.Length; i++)
        {
            Mesh meshData = meshFilters[i].sharedMesh;
            if(false == MeshFilterLocations.ContainsKey(meshData))
            {
                MeshFilterLocations.Add(meshData, new List<Transform>());
            }

            MeshFilterLocations[meshData].Add(meshFilters[i].transform);
        }

        if(false == ToLargeForSingleMesh(MeshFilterLocations))
        {
            return;
        }

        //process each unique filter extracting textures
        foreach (KeyValuePair<Mesh, List<Transform>> kvp in MeshFilterLocations)
        {
            modelData.Add(new ModelData(kvp.Value[0].transform, (int)ResizeTexture));
        }

        var materialList = GetUniqueMaterials(modelData);

        int iModifier = (int)Mathf.Ceil(Mathf.Sqrt(ToNearest(MeshFilterLocations.Keys.Count)));
        CalculateUVOffsets(modelData, modelData[0].albedo.width, modelData[0].albedo.height, iModifier);
        TextureData texture = CombineTextures(modelData);

        Material material = new Material(materialList[0]);
        material.SetTexture("_MainTex", texture.albedo);
        material.SetTexture("_BumpMap", texture.normal);
        material.SetTexture("_OcclusionMap", texture.occlusion);
        material.SetTexture("_MetallicGlossMap", texture.specular);
        material.SetTexture("_EmissionMap", texture.emission);

        for (int i = 0; i < modelData.Count; i++)
        {
            AdjustMeshUVs(modelData[i].mesh, modelData[i].startUVs, modelData[i].endUVs);
        }

        var combines = CombineModels(MeshFilterLocations);

        var newMesh = new Mesh();
        Debug.Log("Combining: " + combines.Count + " models");
        newMesh.CombineMeshes(combines.ToArray(), true);

        var filter = meshSave.ForceComponent<MeshFilter>();
        var renderer = meshSave.ForceComponent<MeshRenderer>();

        filter.mesh = newMesh;
        renderer.sharedMaterial = material;

        texture.Save(".png", meshPath);
        // For testing purposes, also write to a file in the project folder
        AssetDatabase.CreateAsset(newMesh, meshPath);
        string materialPath = meshPath.Replace(".Asset", ".mat");
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.Refresh();
        Selection.activeObject = newMesh;

        for(int i =0; i < meshFilters.Length; i++)
        {
            modelData[i].RevertTemporaryChanges();
        }
    }

    float ToNearest(int x)
    {
        return Mathf.Pow(2f, Mathf.Round(Mathf.Log(x) / Mathf.Log(2f)));
    }

    //Adjust mesh UVs
    private void AdjustMeshUVs(Mesh mesh, Vector2 minUV, Vector2 maxUV)
    {
        Vector2[] uvs = mesh.uv;

        for(int i =0; i < uvs.Length; i++)
        {
            Vector2 uv = uvs[i];
            uv.x = Mathf.Lerp(minUV.x, maxUV.x, uv.x);
            uv.y = Mathf.Lerp(minUV.y, maxUV.y, uv.y);
            uvs[i] = uv;
        }

        mesh.uv = uvs;
    }

    private bool HasLods()
    {
        var lodMeshes = meshesRoot.GetComponentsInChildren<LODGroup>();

        if(null != lodMeshes)
        {
            return lodMeshes.Length > 0;
        }

        return false;
    }

    private bool AllowModel(GameObject obj)
    {
        //dealing with LODS?
        if(true == obj.name.Contains("_LOD"))
        {
            if(false == combineLODs)
            {
                //Only process LOD0 models
                if(false == obj.name.Contains("_LOD0"))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private List<Texture2D> GetTextures(List<Material> materials)
    {
        List<Texture2D> textures = new List<Texture2D>();
        //foreach unique material
        for (int i = 0; i < materials.Count; i++)
        {
            //get texture and store
            textures.Add(materials[i].mainTexture as Texture2D);
        }

        Debug.Log("Texture count: " + textures.Count);
        return textures;
    }

    private void CalculateUVOffsets(List<ModelData> models, int width, int height, int iModifier)
    {
        int iWidth = width * iModifier;
        int iHeight = height * iModifier;

        int iColumn = 0;
        int iRow = 0;
        for (int i = 0; i < models.Count; i++)
        {
            int iStartPixelX = iColumn * width;
            int iStartPixelY = iRow * height;
            models[i].startUVs.x = (float)iStartPixelX / (float)iWidth;
            models[i].startUVs.y = (float)iStartPixelY / (float)iHeight;
            models[i].endUVs.x = (float)(iStartPixelX + width) / (float)iWidth;
            models[i].endUVs.y = (float)(iStartPixelY + height) / (float)iHeight;

            iColumn++;

            if (iColumn >= iModifier)
            {
                iColumn = 0;
                iRow++;
            }
        }
    }

    public class TextureData
    {
        public Texture2D albedo;
        public Texture2D normal;
        public Texture2D occlusion;
        public Texture2D specular;
        public Texture2D emission;

        public TextureData(int width, int height)
        {
            albedo = new Texture2D(width, height);
            normal = new Texture2D(width, height);
            occlusion = new Texture2D(width, height);
            specular = new Texture2D(width, height);
            emission = new Texture2D(width, height);
        }

        public void Apply()
        {
            albedo.Apply();
            normal.Apply();
            occlusion.Apply();
            specular.Apply();
            emission.Apply();
        }

        public void Save(string extension, string location)
        {
            string getAssetPath = location.Replace(".Asset", "");
            getAssetPath = Application.dataPath + "/" + getAssetPath.Replace("Assets/", "");
            SaveTexture(albedo, getAssetPath, "_alb", extension);
            SaveTexture(normal, getAssetPath, "_nrm", extension);
            SaveTexture(occlusion, getAssetPath, "_occ", extension);
            SaveTexture(specular, getAssetPath, "_spec", extension);
            SaveTexture(emission, getAssetPath, "_Emission", extension);
        }

        private void SaveTexture(Texture2D texture, string location, string type, string extension)
        {
            byte[] bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(location + type + extension, bytes);
        }

        public void Combine(Texture2D source, Texture2D dest, int x, int y, int fillX, int fillY)
        {
            Color[] colors = null;
            if (null == source)
            {
                colors = new Color[fillX * fillY];

                for(int i =0; i < colors.Length;i++)
                {
                    colors[i] = Color.black;
                }
            }
            else
            {                
                colors = source.GetPixels();
            }
            dest.SetPixels(x, y, fillX, fillY, colors);
        }
    }

    private TextureData CombineTextures(List<ModelData> models)
    {
        if(models.Count == 0)
        {
            return null;
        }

        Texture2D texture = models[0].albedo;
        int width = texture.width;
        int height = texture.height;
        int iModifier = (int)Mathf.Ceil(Mathf.Sqrt(ToNearest(MeshFilterLocations.Keys.Count)));
        int iWidth = texture.width * iModifier;
        int iHeight = texture.height * iModifier;

        TextureData textures = new TextureData(iWidth, iHeight);
        int iColumn = 0;
        int iRow = 0;

        for(int i = 0; i < models.Count; i++)
        {
            //Texture2D albedoToCombine = models[i].albedo;
            int iStartPixelX = iColumn * width;
            int iStartPixelY = iRow * height;
            //Color[] colors = albedoToCombine.GetPixels();
            //textures.albedo.SetPixels(iStartPixelX, iStartPixelY, width, height, colors);
            textures.Combine(models[i].albedo, textures.albedo, iStartPixelX, iStartPixelY, width, height);
            textures.Combine(models[i].normal, textures.normal, iStartPixelX, iStartPixelY, width, height);
            textures.Combine(models[i].occlusion, textures.occlusion, iStartPixelX, iStartPixelY, width, height);
            textures.Combine(models[i].specular, textures.specular, iStartPixelX, iStartPixelY, width, height);
            textures.Combine(models[i].emission, textures.emission, iStartPixelX, iStartPixelY, width, height);

            iColumn++;

            if(iColumn >= iModifier)
            {
                iColumn = 0;
                iRow++;
            }
        }

        textures.Apply();
        return textures;
    }
}

public static class Extensions
{
    public static T ForceComponent<T>(this GameObject go) where T : Component
    {
        var component = go?.GetComponent<T>();
        if (component == null)
        {
            return go?.AddComponent<T>();
        }
        return component;
    }
}
