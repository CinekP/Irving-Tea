using UnityEngine;
using UnityEditor;

public class UpgradeMaterialsHelper
{
    [MenuItem("Tools/Upgrade Selected Materials to URP")]
    public static void Upgrade()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader not found. Make sure URP is installed in the project.");
            return;
        }

        Object[] selectedObjects = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("No materials selected. Please select materials or a folder containing materials in the Project view.");
            return;
        }

        int upgradedCount = 0;
        foreach (var obj in selectedObjects)
        {
            if (obj is Material mat)
            {
                Undo.RecordObject(mat, "Upgrade to URP");

                // Get original properties before changing the shader
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color mainColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
                
                // Detect if it was cutout/alpha tested
                bool isCutout = mat.IsKeywordEnabled("_ALPHATEST_ON") || 
                                (mat.shader != null && mat.shader.name.ToLower().Contains("cutout")) ||
                                mat.renderQueue == 2450;

                // Change shader to URP Lit
                mat.shader = urpLit;

                // Map old texture/color properties to URP naming conventions
                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    mat.SetTexture("_MainTex", mainTex); // Keep for compatibility
                }
                mat.SetColor("_BaseColor", mainColor);
                mat.SetColor("_Color", mainColor); // Keep for compatibility

                if (isCutout)
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", cutoff);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                }
                else
                {
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "Opaque");
                }

                EditorUtility.SetDirty(mat);
                upgradedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully upgraded {upgradedCount} materials to URP Lit.");
    }
}
