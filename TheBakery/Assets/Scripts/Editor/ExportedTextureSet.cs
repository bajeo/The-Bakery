using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TheBakery
{
    public class ExportedTextureSet
    {
        public class ExportData
        {
            public Texture2D Texture;
            public string TextureTag;
        }

        private Dictionary<string, ExportData> m_Textures = new Dictionary<string, ExportData>();
        public List<string> GetExportTextureList => new List<string>(m_Textures.Keys);

        public ExportedTextureSet(List<string> textureNames, List<string> tags, int iTextureWidth, int iTextureHeight)
        {
            for (int i = 0; i < textureNames.Count; i++)
            {
                string name = textureNames[i];
                if (false == m_Textures.ContainsKey(name))
                {
                    //Create new export texture
                    Texture2D texture = new Texture2D(iTextureWidth, iTextureHeight);
                    ExportData textureData = new ExportData();
                    textureData.Texture = texture;
                    textureData.TextureTag = tags[i];
                    m_Textures.Add(name, textureData);
                }
            }
        }

        public Texture2D GetTexture(string name)
        {
            if (true == m_Textures.ContainsKey(name))
            {
                return m_Textures[name].Texture;
            }

            return null;
        }

        public void FinaliseTexture(string extension, string location, string fileName, TheBakery.BakeryProgress progress)
        {
            string getAssetPath = Application.dataPath + "/" + location.Replace("Assets/", "");

            progress.Title = "Exporting final textures";
            int i = 0;
            //Apply all changes
            foreach (KeyValuePair<string, ExportData> kvp in m_Textures)
            {
                progress.Details = kvp.Key;
                progress.IncrementStep();
                //Apply all changes
                kvp.Value.Texture.Apply();
                SaveTexture(kvp.Value.Texture, getAssetPath, fileName + kvp.Value.TextureTag, extension);
                i++;
            }
        }

        private void SaveTexture(Texture2D texture, string location, string fileName, string extension)
        {
            byte[] bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(location + fileName + extension, bytes);
        }

        public void ApplyToMaterial(Material mat)
        {
            foreach(KeyValuePair<string, ExportData> kvp in m_Textures)
            {
                mat.SetTexture(kvp.Key, kvp.Value.Texture);
            }
        }
    }
}
