// WaterCausticsModules
// Copyright (c) 2021 Masataka Hakozaki

#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace MH.WaterCausticsModules {
    /*------------------------------------------------------------------------ 
    アセット上書きアップデート防止
    このアセットがインポートされようとした際、古いバージョンを削除するようダイアログ表示
    -------------------------------------------------------------------------*/
    public class PreventOverwriteAsset {
        [InitializeOnLoadMethod]
        static private void registerCallback () {
            AssetDatabase.importPackageStarted -= importStarted;
            AssetDatabase.importPackageStarted += importStarted;
        }

        static void importStarted (string packageName) {
            var str = packageName.ToLower ();
            if (!str.Contains ("patch") && !str.Contains ("fix") && Constant.CheckPackageName (str)) {
                // フォルダ削除後もこのスクリプトがしばらく残っていてダイアログが表示されてしまうのでFile.Existsでチェック
                if (TexGenModuleRef.findAsset<TexGenModuleRef> (out var asset, out var path) && File.Exists (path)) {
                    if (asset.assetFolder && asset.assetFolder.name == Constant.ASSET_FOLDER_NAME) {
                        // アセットルートPathが取得出来た場合
                        var folderPath = AssetDatabase.GetAssetPath (asset.assetFolder);
                        showDialog (folderPath);
                    } else {
                        // アセットルートPathが取得出来ない場合
                        showDialog ($"Assets/{Constant.ASSET_FOLDER_NAME}");
                    }
                }
            }
        }

#if WCE_DEVELOPMENT
        [MenuItem ("WCM/TestDialog/PreventOverwrite")]
        static void dialogTest2 () {
            if (TexGenModuleRef.findAsset<TexGenModuleRef> (out var asset, out var path) && asset.assetFolder) {
                var folderPath = AssetDatabase.GetAssetPath (asset.assetFolder);
                showDialog (folderPath);
            }
        }
#endif

        static void showDialog (string folderPath) {
            string str0 = "Please delete old version before update.";
            string str1 = $"If this package is a different version,\nthe older version must be removed first to avoid conflicts.\n\n1, Cancel in the Import window appearing after this.\n\n2, Delete the \"{folderPath}\" folder.\n\n3, Import this asset again.\n\n\n({Constant.ASSET_NAME})";
            EditorUtility.DisplayDialog (str0, str1, "OK", "It's not different version.");
        }

    }
}

#endif
