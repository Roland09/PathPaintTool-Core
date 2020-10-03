/** 
 * This is based on the work of Unity (Terrain Tools 3.0.1-preview), that way the license is the same as the original Terrain Tools license. 
 * Please be aware that Unity is in no way affiliated with this project!
 *
 * Terrain Tools License:
 * -----------------------------------------------------------------------
 * Terrain Tools copyright © 2019 Unity Technologies ApS
 *
 * Licensed under the Unity Companion License for Unity-dependent projects--see [Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License). 
 *
 * Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
*/
using UnityEngine;
using UnityEngine.Experimental.TerrainAPI;
using UnityEditor.ShortcutManagement;

namespace UnityEditor.Experimental.TerrainAPI
{
    public class PathTool : TerrainPaintTool<PathTool>
    {
#if UNITY_2019_1_OR_NEWER
        [Shortcut("Terrain/Select Path Tool", typeof(TerrainToolShortcutContext))]                // tells shortcut manager what to call the shortcut and what to pass as args
        static void SelectShortcut(ShortcutArguments args) {
            TerrainToolShortcutContext context = (TerrainToolShortcutContext)args.context;          // gets interface to modify state of TerrainTools
            context.SelectPaintTool<PathTool>();                                                  // set active tool
        }
#endif

        [SerializeField]
        IBrushUIGroup m_commonUI;
        private IBrushUIGroup commonUI
        {
            get
            {
                if( m_commonUI == null )
                {
                    m_commonUI = new DefaultBrushUIGroup("PathTool", DefaultBrushUIGroup.Feature.NoScatter );
                    m_commonUI.OnEnterToolMode();
                }

                return m_commonUI;
            }
        }

        Terrain m_StartTerrain = null;
        private Vector3 m_StartPoint;

        Material m_Material = null;
        Material GetPaintMaterial() {
            if (m_Material == null)
                m_Material = new Material(Shader.Find("Hidden/TerrainTools/SetExactHeight"));
            return m_Material;
        }

        [System.Serializable]
        class PathToolSerializedProperties
        {
            public AnimationCurve widthProfile;
            public AnimationCurve heightProfile;
            public AnimationCurve strengthProfile;
            public AnimationCurve jitterProfile;

            public void SetDefaults()
            {
                widthProfile = AnimationCurve.Linear(0, 1, 1, 1);
                heightProfile = AnimationCurve.Linear(0, 0, 1, 0);
                strengthProfile = AnimationCurve.Linear(0, 1, 1, 1);
                jitterProfile = AnimationCurve.Linear(0, 0, 1, 0);
            }
        }

        PathToolSerializedProperties pathToolProperties = new PathToolSerializedProperties();

        public override string GetName()
        {
            return "Sculpt/Path";
        }

        public override string GetDesc()
        {
            return "Paint mode: drag brush to paint a path. Stroke mode: Control + Click to Set the first start point, click to connect the path.";
        }

        public override void OnEnterToolMode() {
            base.OnEnterToolMode();
            commonUI.OnEnterToolMode();
        }

        public override void OnExitToolMode() {
            base.OnExitToolMode();
            commonUI.OnExitToolMode();
        }

        public override void OnSceneGUI(Terrain terrain, IOnSceneGUI editContext)
        {
            commonUI.OnSceneGUI2D(terrain, editContext);

            if (editContext.hitValidTerrain || commonUI.isInUse)
            {
                commonUI.OnSceneGUI(terrain, editContext);

                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

				if (pathToolProperties != null && pathToolProperties.widthProfile != null)
				{
					float endWidth = Mathf.Abs(pathToolProperties.widthProfile.Evaluate(1.0f));

					BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, commonUI.raycastHitUnderCursor.textureCoord, commonUI.brushSize * endWidth, commonUI.brushRotation);
					PaintContext ctx = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds(), 1);
					TerrainPaintUtilityEditor.DrawBrushPreview(ctx, TerrainPaintUtilityEditor.BrushPreview.SourceRenderTexture, editContext.brushTexture, brushXform, TerrainPaintUtilityEditor.GetDefaultBrushPreviewMaterial(), 0);
					TerrainPaintUtility.ReleaseContextResources(ctx);
				}                
			}

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            //display a brush preview at the path starting location, using starting size from width profile
            if (m_StartTerrain != null)
            {
                float startWidth = Mathf.Abs(pathToolProperties.widthProfile.Evaluate(0.0f));

                BrushTransform brushTransform = TerrainPaintUtility.CalculateBrushTransform(m_StartTerrain, m_StartPoint, commonUI.brushSize * startWidth, commonUI.brushRotation);
                PaintContext sampleContext = TerrainPaintUtility.BeginPaintHeightmap(m_StartTerrain, brushTransform.GetBrushXYBounds());
                TerrainPaintUtilityEditor.DrawBrushPreview(sampleContext, TerrainPaintUtilityEditor.BrushPreview.SourceRenderTexture,
                                                           editContext.brushTexture, brushTransform, TerrainPaintUtilityEditor.GetDefaultBrushPreviewMaterial(), 0);
                TerrainPaintUtility.ReleaseContextResources(sampleContext);
            }
        }

        bool m_ShowPathControls = true;
        bool m_initialized = false;
        public override void OnInspectorGUI(Terrain terrain, IOnInspectorGUI editContext)
        {

            if (!m_initialized)
            {
                LoadSettings();
                m_initialized = true;
            }
            EditorGUI.BeginChangeCheck();

            commonUI.OnInspectorGUI(terrain, editContext);

            m_ShowPathControls = TerrainToolGUIHelper.DrawHeaderFoldoutForBrush(Styles.controlHeader, m_ShowPathControls, pathToolProperties.SetDefaults);

            if (m_ShowPathControls) {
                //"Controls the width of the path over the length of the stroke"
                pathToolProperties.widthProfile = EditorGUILayout.CurveField(Styles.widthProfileContent, pathToolProperties.widthProfile);
                pathToolProperties.heightProfile = EditorGUILayout.CurveField(Styles.heightProfileContent, pathToolProperties.heightProfile);
                pathToolProperties.strengthProfile = EditorGUILayout.CurveField(Styles.strengthProfileContent, pathToolProperties.strengthProfile);
                pathToolProperties.jitterProfile = EditorGUILayout.CurveField(Styles.jitterProfileContent, pathToolProperties.jitterProfile);
            }

            if (EditorGUI.EndChangeCheck())
            {
                SaveSetting();
                Save(true);
            }
        }

        private Vector2 transformToWorld(Terrain t, Vector2 uvs)
        {
            Vector3 tilePos = t.GetPosition();
            return new Vector2(tilePos.x, tilePos.z) + uvs * new Vector2(t.terrainData.size.x, t.terrainData.size.z);
        }

        private Vector2 transformToUVSpace(Terrain originTile, Vector2 worldPos) {
            Vector3 originTilePos = originTile.GetPosition();
            Vector2 uvPos = new Vector2((worldPos.x - originTilePos.x) / originTile.terrainData.size.x,
                                        (worldPos.y - originTilePos.z) / originTile.terrainData.size.z);
            return uvPos;
        }

        private void ApplyBrushInternal(Terrain terrain, Vector2 uv, Texture brushTexture, float brushSpacing)
        {
            //get the target position & height
            float targetHeight = terrain.terrainData.GetInterpolatedHeight(uv.x, uv.y) / terrain.terrainData.size.y;
            Vector3 targetPos = new Vector3(uv.x, uv.y, targetHeight);

            if (terrain != m_StartTerrain) {
                //figure out the stroke vector in uv,height space
                Vector2 targetWorld = transformToWorld(terrain, uv);
                Vector2 targetUVs = transformToUVSpace(m_StartTerrain, targetWorld);
                targetPos.x = targetUVs.x;
                targetPos.y = targetUVs.y;
            }

            Vector3 stroke = targetPos - m_StartPoint;
            float strokeLength = stroke.magnitude;
            int numSplats = (int)(strokeLength / (0.1f * Mathf.Max(brushSpacing, 0.01f)));

            Terrain currTerrain = m_StartTerrain;
            Material mat = GetPaintMaterial();

            Vector2 posOffset = new Vector2(0.0f, 0.0f);
            Vector2 currUV = new Vector2();
            Vector4 brushParams = new Vector4();

            Vector2 jitterVec = new Vector2(-stroke.z, stroke.x); //perpendicular to stroke direction
            jitterVec.Normalize();

            

            for (int i = 0; i < numSplats; i++)
            {
                float pct = (float)i / (float)numSplats;

                float widthScale = pathToolProperties.widthProfile.Evaluate(pct);
                float heightOffset = pathToolProperties.heightProfile.Evaluate(pct) / currTerrain.terrainData.size.y;
                float strengthScale = pathToolProperties.strengthProfile.Evaluate(pct);
                float jitterOffset = pathToolProperties.jitterProfile.Evaluate(pct) / Mathf.Max(currTerrain.terrainData.size.x, currTerrain.terrainData.size.z);

                Vector3 currPos = m_StartPoint + pct * stroke;

                //add in jitter offset (needs to happen before tile correction)
                currPos.x += posOffset.x + jitterOffset * jitterVec.x;
                currPos.y += posOffset.y + jitterOffset * jitterVec.y;

                if (currPos.x >= 1.0f && (currTerrain.rightNeighbor != null)) {
                    currTerrain = currTerrain.rightNeighbor;
                    currPos.x -= 1.0f;
                    posOffset.x -= 1.0f;
                }
                if(currPos.x <= 0.0f && (currTerrain.leftNeighbor != null)) {
                    currTerrain = currTerrain.leftNeighbor;
                    currPos.x += 1.0f;
                    posOffset.x += 1.0f;
                }
                if(currPos.y >= 1.0f && (currTerrain.topNeighbor != null)) {
                    currTerrain = currTerrain.topNeighbor;
                    currPos.y -= 1.0f;
                    posOffset.y -= 1.0f;
                }
                if(currPos.y <= 0.0f && (currTerrain.bottomNeighbor != null)) {
                    currTerrain = currTerrain.bottomNeighbor;
                    currPos.y += 1.0f;
                    posOffset.y += 1.0f;
                }

                currUV.x = currPos.x;
                currUV.y = currPos.y;

                int finalBrushSize = (int)(widthScale * (float)commonUI.brushSize);
                float finalHeight =  (m_StartPoint + pct * stroke).z + heightOffset;

                using(IBrushRenderWithTerrain brushRenderWithTerrain = new BrushRenderWithTerrainUiGroup(commonUI, "PathTool", brushTexture))
                {
                    if(brushRenderWithTerrain.CalculateBrushTransform(currTerrain, currUV, finalBrushSize, out BrushTransform brushTransform))
                    {
                        Rect brushBounds = brushTransform.GetBrushXYBounds();
                        PaintContext paintContext = brushRenderWithTerrain.AcquireHeightmap(true, currTerrain, brushBounds);
                
                        mat.SetTexture("_BrushTex", brushTexture);

                        brushParams.x = commonUI.brushStrength * strengthScale;
                        brushParams.y = 0.5f * finalHeight;

                        mat.SetVector("_BrushParams", brushParams);

                        FilterContext fc = new FilterContext(terrain, currPos, finalBrushSize, commonUI.brushRotation);
                        fc.renderTextureCollection.GatherRenderTextures(paintContext.sourceRenderTexture.width, paintContext.sourceRenderTexture.height);
                        RenderTexture filterMaskRT = commonUI.GetBrushMask(fc, paintContext.sourceRenderTexture);
                        mat.SetTexture("_FilterTex", filterMaskRT);

                        brushRenderWithTerrain.SetupTerrainToolMaterialProperties(paintContext, brushTransform, mat);
                        brushRenderWithTerrain.RenderBrush(paintContext, mat, 0);
                    }
                }
            }
        }
        
        public override bool OnPaint(Terrain terrain, IOnPaint editContext)
        {
            commonUI.OnPaint(terrain, editContext);
            Vector2 uv = editContext.uv;

            if(Event.current.shift) { return true; }
            //grab the starting position & height
            if (Event.current.control)
            {
                TerrainData terrainData = terrain.terrainData;
                float height = terrainData.GetInterpolatedHeight(uv.x, uv.y) / terrainData.size.y;

                m_StartPoint = new Vector3(uv.x, uv.y, height);
                m_StartTerrain = terrain;
                return true;
            }
            else if (!m_StartTerrain || (Event.current.type == EventType.MouseDrag)) {
                return true;
            }
            else
            {
                ApplyBrushInternal(terrain, uv, editContext.brushTexture, commonUI.brushSpacing);
                return false;
            }
        }

        private static class Styles
        {
            public static readonly GUIContent controlHeader = EditorGUIUtility.TrTextContent("Path Tool Controls");
            public static readonly GUIContent widthProfileContent = EditorGUIUtility.TrTextContent("Width Profile", "A multiplier that controls the width of the path over the length of the stroke");
            public static readonly GUIContent heightProfileContent = EditorGUIUtility.TrTextContent("Height Offset Profile", "Adds a height offset to the path along the length of the stroke (World Units)");
            public static readonly GUIContent strengthProfileContent = EditorGUIUtility.TrTextContent("Strength Profile", "A multiplier that controls influence of the path along the length of the stroke");
            public static readonly GUIContent jitterProfileContent = EditorGUIUtility.TrTextContent("Horizontal Offset Profile", "Adds an offset perpendicular to the stroke direction (World Units)");

        }

        private void SaveSetting()
        {
            string pathToolData = JsonUtility.ToJson(pathToolProperties);
            EditorPrefs.SetString("Unity.TerrainTools.Path", pathToolData);

        }

        private void LoadSettings()
        {

            string pathToolData = EditorPrefs.GetString("Unity.TerrainTools.Path");
            pathToolProperties.SetDefaults();
            JsonUtility.FromJsonOverwrite(pathToolData, pathToolProperties);
        }
    }
}
