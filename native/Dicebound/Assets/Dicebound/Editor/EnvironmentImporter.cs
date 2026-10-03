using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dicebound.Editor
{
    public static class EnvironmentImporter
    {
        private const string Root="Assets/Resources/Environment";
        public static void Configure()
        {
            foreach(string folder in Directory.GetDirectories(Root)){
                string dir=folder.Replace('\\','/');
                foreach(string path in Directory.GetFiles(dir,"*.jpg")){
                    string asset=path.Replace('\\','/'),key=Path.GetFileNameWithoutExtension(asset);
                    var t=(TextureImporter)AssetImporter.GetAtPath(asset);
                    t.textureType=key=="normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                    t.sRGBTexture=key=="albedo";t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=8;
                    t.isReadable=key=="roughness";t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
                }
                string model=dir+"/model.fbx";
                if(File.Exists(model)){
                    var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
                    importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                }
                var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/roughness.jpg");
                string maskPath=dir+"/metallic-smoothness.asset";
                if(rough){
                    var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);bool created=!mask;
                    if(created)mask=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,true,true){name="Linear roughness to Unity smoothness"};
                    var pixels=rough.GetPixels32();byte metallic=(byte)(dir.Contains("rusty_metal")?178:0);
                    for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(metallic,0,0,(byte)(255-pixels[i].r));
                    mask.SetPixels32(pixels);mask.Apply(true,false);mask.wrapMode=TextureWrapMode.Repeat;mask.anisoLevel=8;
                    if(created)AssetDatabase.CreateAsset(mask,maskPath);else EditorUtility.SetDirty(mask);
                }
                string matPath=dir+"/surface.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,matPath);}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/albedo.jpg"));
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/normal.jpg"));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.75f);
                material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Smoothness",.6f);
                material.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/occlusion.jpg"));material.EnableKeyword("_OCCLUSIONMAP");material.SetFloat("_OcclusionStrength",.65f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
