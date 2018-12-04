using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TheBakery
{
    public class MaterialData
    {
        private const string PBR_MetallicStrength = "_GlossMapScale";
        private Color m_ColorTint;
        private float m_fMetallicStrength;
        private string m_sTextureName;
        public class TextureStorageData
        {
            public Texture2D m_Texture;
            public TextureImporterType ImportedType;
            public int ImportexTextureSize;
        }

        private Dictionary<string, TextureStorageData> TextureStorage = new Dictionary<string, TextureStorageData>();
        private Dictionary<string, System.Action<MaterialData, Rect, Color[]>> TextureAdditionalProcessing = new Dictionary<string, System.Action<MaterialData, Rect, Color[]>>()
        {
            { "_MainTex", ProcessAlbedoColorTransfer },
            { "_MetallicGlossMap", ProcessMetallicColorTransfer }
        };

        private TextureImporterType importedType;
        private int ImportedTextureSize;

        public Vector2 GetStartUV => startUV;
        public Vector2 GetEndUV => endUV;

        //Grid coordinates all textures will go to
        private Vector2 startUV;
        private Vector2 endUV;
        private int iStartPixelX;
        private int iStartPixelY;
        private bool m_bTexturesSameSize;
        private int m_iTextureWidth;
        private int m_iTextureHeight;

        public TextureStorageData GetStorageData(string name)
        {
            if(true == TextureStorage.ContainsKey(name))
            {
                return TextureStorage[name];
            }

            return null;
        }

        public bool HasTexture(string sName)
        {
            return TextureStorage.ContainsKey(sName);
        }

        public List<string> GetTextureList()
        {
            List<string> textureList = new List<string>();
            textureList.AddRange(TextureStorage.Keys);
            return textureList;
        }

        public bool ValidTextures()
        {
            return m_bTexturesSameSize;
        }

        public void GetTextureSizes(out int Width, out int Height)
        {
            Width = m_iTextureWidth;
            Height = m_iTextureHeight;
        }

        private bool TexturesSameSize()
        {
            m_iTextureWidth = -1;
            m_iTextureHeight = -1;

            foreach (KeyValuePair<string, TextureStorageData> kvp in TextureStorage)
            {
                if (m_iTextureWidth == -1)
                {
                    //Set initial
                    m_iTextureWidth = kvp.Value.m_Texture.width;
                }
                else if (m_iTextureWidth != kvp.Value.m_Texture.width)
                {
                    return false;
                }

                if (m_iTextureHeight == -1)
                {
                    m_iTextureHeight = kvp.Value.m_Texture.height;
                }
                else if (m_iTextureHeight != kvp.Value.m_Texture.height)
                {
                    return false;
                }
            }

            return true;
        }

        public MaterialData(Material material, List<string> textureList, int Resize, bool bBakeTextures, TheBakery.BakeryProgress progress)
        {
            if(false == bBakeTextures)
            {
                return;
            }

            m_ColorTint = material.GetColor("_Color");
            m_fMetallicStrength = material.GetFloat(PBR_MetallicStrength);

            for (int i = 0; i < textureList.Count; i++)
            {
                string textureName = textureList[i];
                Texture2D texture = material.GetTexture(textureName) as Texture2D;

                if (null != texture)
                {
                    progress.Details = "Importing texture: " + textureName;
                    progress.IncrementStep();
                    TextureStorageData data = new TextureStorageData();
                    data.m_Texture = texture;
                    SetupTextureForProcessing(data, Resize);
                    TextureStorage[textureName] = data;
                }
                else
                {
                    Debug.Log("Material: " + material.name + " does not contain texture: " + textureName);
                }
            }

            m_bTexturesSameSize = TexturesSameSize();
        }

        public static int GetProcessingCount(Material material, List<string> textureList)
        {
            int iCount = 0;
            for (int i = 0; i < textureList.Count; i++)
            {
                string textureName = textureList[i];
                Texture2D texture = material.GetTexture(textureName) as Texture2D;

                if (null != texture)
                {
                    iCount++;
                }
            }

            return iCount;
        }

        public void CalculateUVOffset(int iTextureCount, int textureWidth, int textureHeight, 
                                        int iGridWidth, int iGridHeight, 
                                        int iActualTextureWidth, int iActualTextureHeight)
        {
            //Get the overal texture size
            int iWidth = textureWidth * iGridWidth;
            int iHeight = textureHeight * iGridHeight;

            //Calculate our position in the grid
            int iColumn = (int)Mathf.Floor(iTextureCount / (float)iGridWidth);
            int iRow = iTextureCount % iGridWidth;
            //Set start pixel to bottom left of this grid position
            iStartPixelX = iRow * textureWidth;
            iStartPixelY = iColumn * textureHeight;

            //Calculate the amount of space we require based on the actual size of our textures compared
            //to the size we have available to us
            //NOTE: Textures are shrunk to the textureWidth and height values eariler so we just need to handle
            //if our texture was smaller than that value originally
            startUV = new Vector2((float)iStartPixelX / (float)iWidth, (float)iStartPixelY / (float)iHeight);
            endUV = new Vector2(((float)iStartPixelX + iActualTextureWidth) / (float)iWidth, 
                                ((float)iStartPixelY + iActualTextureHeight) / (float)iHeight);
        }

        public void BakeTexture(ExportedTextureSet exportedTexture, int iTextureWidth, int iTextureHeight, TheBakery.BakeryProgress progress)
        {
            List<string> texturesToProcess = exportedTexture.GetExportTextureList;

            foreach(string textureName in texturesToProcess)
            {
                progress.Details = "Baking texture: " + textureName;
                progress.IncrementStep();
                Texture2D textureExport = exportedTexture.GetTexture(textureName);

                if (null != textureExport)
                {
                    System.Action<MaterialData, Rect, Color[]> action = null;
                    if (true == TextureAdditionalProcessing.ContainsKey(textureName))
                    {
                        action = TextureAdditionalProcessing[textureName];
                    }

                    Texture2D sourceTexture = null;

                    if(true == TextureStorage.ContainsKey(textureName))
                    {
                        sourceTexture = TextureStorage[textureName].m_Texture;
                    }

                    CombineTexture(sourceTexture, textureExport,
                        iStartPixelX, iStartPixelY, iTextureWidth, iTextureHeight, action);
                }
            }
        }

        private static void ProcessAlbedoColorTransfer(MaterialData materialData, Rect rect, Color[] pixels)
        {
            for(int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= materialData.m_ColorTint;
            }
        }

        private static void ProcessMetallicColorTransfer(MaterialData materialData, Rect rect, Color[] pixels)
        {
            TextureStorageData data = materialData.GetStorageData("_OcclusionMap");

            if (null != data)
            {
                Texture2D occlusionMap = data.m_Texture;
                Color[] dest = occlusionMap.GetPixels();// (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);

                //insert occlusion data into metallicGloss channel G
                for (int i = 0; i < dest.Length; i++)
                {
                    //insert override color
                    Color color = pixels[i];
                    color.a = dest[i].a * materialData.m_fMetallicStrength;
                    pixels[i] = color;
                }
            }
        }

        //private static void ProcessOcclusionColorTransfer(MaterialData materialData, Rect rect, Color[] pixels)
        //{
        //    TextureStorageData data = materialData.GetStorageData("_MetallicGlossMap");

        //    if (null != data)
        //    {
        //        Texture2D metallicGloss = data.m_Texture;
        //        Color[] dest = metallicGloss.GetPixels();//(int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);

        //        //insert occlusion data into metallicGloss channel G
        //        for(int i = 0; i < dest.Length; i++)
        //        {
        //            //insert override color
        //            Color color = dest[i];
        //            color.a = pixels[i].a * materialData.m_fMetallicStrength;
        //            dest[i] = color;
        //            pixels[i] = color;
        //        }

        //        metallicGloss.SetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height, dest);
        //    }
        //}

        private void CombineTexture(Texture2D source, Texture2D dest, int x, int y, int fillX, int fillY, System.Action<MaterialData, Rect, Color[]> action)
        {
            Color[] colors = null;
            if (null == source)
            {
                colors = new Color[fillX * fillY];

                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = Color.black;
                }
            }
            else
            {
                colors = source.GetPixels();
                action?.Invoke(this, new Rect(x, y, fillX, fillY), colors);
            }
            dest.SetPixels(x, y, fillX, fillY, colors);
        }

        private void SetupTextureForProcessing(TextureStorageData data, int Resize)
        {
            string path = AssetDatabase.GetAssetPath(data.m_Texture);
            TextureImporter textureImporter = (TextureImporter)TextureImporter.GetAtPath(path);

            if (null != textureImporter)
            {
                textureImporter.isReadable = true;
                data.ImportedType = textureImporter.textureType;
                data.ImportexTextureSize = textureImporter.maxTextureSize;
                //Change texture to default so we can process normal maps etc...
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.maxTextureSize = Resize;
               // EditorUtility.SetDirty(textureImporter);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
        }

        public void Restore(TheBakery.BakeryProgress progress)
        {
            foreach (KeyValuePair<string, TextureStorageData> kvp in TextureStorage)
            {
                progress.Details = "Restoring texture: " + kvp.Key;
                progress.IncrementStep();
                RestoreTexture(kvp.Value);
            }
        }

        private void RestoreTexture(TextureStorageData data)
        {
            string path = AssetDatabase.GetAssetPath(data.m_Texture);
            TextureImporter textureImporter = (TextureImporter)TextureImporter.GetAtPath(path);

            if (null != textureImporter)
            {
                textureImporter.isReadable = false;
                textureImporter.textureType = data.ImportedType;
                textureImporter.maxTextureSize = data.ImportexTextureSize;
            }

           AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
        }
    }
}