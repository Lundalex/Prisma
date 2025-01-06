using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChartAndGraph
{
    partial class EditorMenu
    {
        private static void InstanciateCanvas(string path)
        {
            Canvas[] canvases = GameObject.FindObjectsOfType<Canvas>();
            if (canvases == null || canvases.Length == 0)
            {
                EditorUtility.DisplayDialog("No canvas in scene", "Please add a canvas to the scene and try again", "Ok");
                return;
            }
            Canvas canvas = null;
            foreach(Canvas c in canvases)
            {
                if(c.transform.parent == null)
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                EditorUtility.DisplayDialog("No canvas in scene", "Please add a canvas to the scene and try again", "Ok");
                return;
            }
            GameObject obj =  Resources.Load<GameObject>(path);
            GameObject newObj = (GameObject)GameObject.Instantiate(obj);
            newObj.transform.SetParent(canvas.transform,false);
            newObj.name = newObj.name.Replace("(Clone)","");
            Undo.RegisterCreatedObjectUndo(newObj, "Create Object");
        }

        [MenuItem("Tools/Charts/Clear All")]
        public static void ClearChartGarbage()
        {            
            ChartItem[] children = GameObject.FindObjectsOfType<ChartItem>();
            for (int i = 0; i < children.Length; ++i)
            {
                if (children[i] != null)
                {
                    ChartCommon.SafeDestroy(children[i].gameObject);
                }
            }
        }
        [MenuItem("Tools/Charts/Fix prefabs")]
        public static void ClearPrefabs()
        {
            if (EditorUtility.DisplayDialog("Warning", "Make sure to backup your project before calling Fix Prefabs", "OK", "Cancel") == false)
                return;
            string[] prefabs = AssetDatabase.FindAssets("t:prefab");

            foreach (var guid in prefabs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Load the contents of the Prefab Asset.
                try
                {
                    GameObject contentsRoot = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                    if (contentsRoot.GetComponentInChildren<AnyChart>() != null)
                    {
                        Debug.Log("Fixing " + path);
                        bool save = false;
                        // Modify Prefab contents.
                        foreach (var item in contentsRoot.GetComponentsInChildren<ChartItem>())
                        {
                            if (item == null)
                                continue;
                            if (item.gameObject != null)
                            {
                                save = true;
                                GameObject.DestroyImmediate(item.gameObject);
                            }
                        }
                        if (save)
                        {
                            Debug.Log("Saving " + path);
                            UnityEditor.PrefabUtility.SaveAsPrefabAsset(contentsRoot, path);
                        }
                        else
                        {
                            Debug.Log("No change to prefab " + path);
                        }
                    }
                    UnityEditor.PrefabUtility.UnloadPrefabContents(contentsRoot);
                }
                catch (Exception)
                {
                    Debug.Log("Failed " + path);
                }
            }
        }
        [MenuItem("Tools/Charts/Radar/Canvas")]
        public static void AddRadarChartCanvas()
        {
            InstanciateCanvas("MenuPrefabs/2DRadar");
        }



        [MenuItem("Tools/Charts/Bar/Canvas/Simple")]
        public static void AddBarChartSimpleCanvas()
        {
            InstanciateCanvas("MenuPrefabs/BarCanvasSimple");            
        }

        [MenuItem("Tools/Charts/Bar/Canvas/Multiple Groups")]
        public static void AddBarChartMultipleCanvas()
        {
            InstanciateCanvas("MenuPrefabs/BarCanvasMultiple");
        }





        [MenuItem("Tools/Charts/Torus/Canvas")]
        public static void AddTorusChartCanvas()
        {
            InstanciateCanvas("MenuPrefabs/TorusCanvas");
        }

        [MenuItem("Tools/Charts/Pie/Canvas")]
        public static void AddPieChartCanvas()
        {
            InstanciateCanvas("MenuPrefabs/PieCanvas");
        }




        [MenuItem("Tools/Charts/Graph/Canvas")]
        public static void AddGraphMultiple()
        {
            InstanciateCanvas("MenuPrefabs/GraphMultiple");
        }


        [MenuItem("Tools/Charts/Legend")]
        public static void AddChartLegend()
        {
            InstanciateCanvas("MenuPrefabs/Legend");
        }
    }
}
