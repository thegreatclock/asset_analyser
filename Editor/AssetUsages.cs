using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.U2D;

namespace GreatClock.Common.AssetAnalyser {

	public class AssetUsages : EditorWindow, IHasCustomMenu {

		[MenuItem("GreatClock/Asset Analyser/Dependencies/Asset Usages")]
		static void CollectAssetUsages() {
			AssetReferencedData[] usages = CollectDependencies();
			AssetUsages win = GetWindow<AssetUsages>(false, "Asset Usages");
			win.SetObjects(usages);
			win.minSize = new Vector2(1060f, 300f);
			win.maxSize = new Vector2(1060f, 4000f);
			win.Show();
		}

		private static AssetReferencedData[] CollectDependencies() {
			List<string> scenes = new List<string>();
			foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) {
				if (scene.enabled) { scenes.Add(scene.path); }
			}
			string[] all = AssetDatabase.GetAllAssetPaths();
			float step1 = 0.3f;
			float step2 = 0.85f;
			int len = all.Length;
			List<AssetReferencedData> ars = new List<AssetReferencedData>(2048);
			Dictionary<string, AssetReferencedData> instanceId2Asset = new Dictionary<string, AssetReferencedData>(2048);
			for (int i = 0; i < len; i++) {
				string path = all[i];
				float t = Mathf.Lerp(0f, step1, (i + 0.5f) / len);
				EditorUtility.DisplayProgressBar("Collecting Dependencies",
					string.Format("Step 1 ({0} / {1}) {2}", i + 1, len, path), t);
				if (AssetDatabase.IsValidFolder(path)) { continue; }
				if (!path.StartsWith("Assets/")) { continue; }
				AssetReferencedData ard = new AssetReferencedData();
				ard.path = path;
				ard.asset = AssetDatabase.LoadMainAssetAtPath(path);
				bool isScene = path.LastIndexOf(".unity", System.StringComparison.OrdinalIgnoreCase) == path.Length - 6;
				if (isScene) {
					ard.included = scenes.Contains(path);
				} else if (path.Contains("/Resources/") || path.Contains("/Plugins/")) {
					ard.included = true;
				} else if (path.LastIndexOf(".dll", System.StringComparison.OrdinalIgnoreCase) == path.Length - 4) {
					ard.included = true;
				} else if (ard.asset is MonoScript) {
					ard.included = true;
				} else {
					ard.included = false;
				}
				instanceId2Asset.Add(path, ard);
				ars.Add(ard);
			}
			len = ars.Count;
			for (int i = 0; i < len; i++) {
				AssetReferencedData user = ars[i];
				float t = Mathf.Lerp(step1, step2, (i + 0.5f) / len);
				EditorUtility.DisplayProgressBar("Collecting Dependencies",
					string.Format("Step 2 ({0} / {1}) {2}", i + 1, len, user.path), t);
				string[] dependencies = AssetDatabase.GetDependencies(user.path, false);
				if (dependencies.Length <= 0) { continue; }
				foreach (string dependency in dependencies) {
					AssetReferencedData ard;
					if (!instanceId2Asset.TryGetValue(dependency, out ard)) { continue; }
					ard.usages.Add(user);
				}
			}
			for (int i = 0; i < len; i++) {
				AssetReferencedData ard = ars[i];
				string path = ard.path;
				float t = Mathf.Lerp(step2, 1f, (i + 0.5f) / len);
				EditorUtility.DisplayProgressBar("Analysing Dependencies",
					string.Format("Step 3 ({0} / {1}) {2}", i + 1, len, path), t);
				bool ignoreSpriteNPOT = false;
				foreach (AssetReferencedData user in ard.usages) {
					if (user.asset is SpriteAtlas) {
						ignoreSpriteNPOT = true;
						break;
					}
				}
				SubAssetData sam = new SubAssetData();
				sam.asset = ard.asset;
				sam.comment = AssetComment.GetAssetComment(sam.asset, ignoreSpriteNPOT, out sam.commentBold, out sam.commentWarning);
				ard.subAssets.Add(sam);
				bool isScene = path.LastIndexOf(".unity", System.StringComparison.OrdinalIgnoreCase) == path.Length - 6;
				bool isPrefab = path.LastIndexOf(".prefab", System.StringComparison.OrdinalIgnoreCase) == path.Length - 7;
				if (!isScene && !isPrefab) {
					foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path)) {
						if (asset == ard.asset) { continue; }
						SubAssetData sa = new SubAssetData();
						sa.asset = asset;
						sa.comment = AssetComment.GetAssetComment(asset, ignoreSpriteNPOT, out sa.commentBold, out sa.commentWarning);
						ard.subAssets.Add(sa);
					}
				}
			}
			EditorUtility.ClearProgressBar();
			return ars.ToArray();
		}

		private class AssetReferencedData {
			public string path;
			public Object asset;
			public bool included;
			public List<SubAssetData> subAssets = new List<SubAssetData>();
			public List<AssetReferencedData> usages = new List<AssetReferencedData>();
			private bool mCheckingSelf = false;
			public bool IsIncluded() {
				if (included) { return true; }
				//TODO cache...
				if (mCheckingSelf) { return false; }
				mCheckingSelf = true;
				foreach (AssetReferencedData usage in usages) {
					if (usage.IsIncluded()) { mCheckingSelf = false; return true; }
				}
				mCheckingSelf = false;
				return false;
			}
			public int from;
		}

		private class SubAssetData {
			public Object asset;
			public GUIContent comment;
			public bool commentBold;
			public bool commentWarning;
		}

		private static bool gui_inited = false;
		private static GUIStyle style_toolbar_search_text;
		private static GUIStyle style_toolbar_search_cancel;
		private static GUIStyle style_toolbar_button;
		private static GUIStyle style_cn_box;

		private static GUILayoutOption min_width;
		private static GUILayoutOption min_height;
		private static GUILayoutOption width_filter;
		private static GUILayoutOption width_asset;
		private static GUILayoutOption width_included;
		private static GUILayoutOption width_sub_assets;
		private static GUILayoutOption width_comment;
		private static GUILayoutOption width_user;
		private static GUILayoutOption width_user_object;

		private Vector2 mScroll;

		private AssetReferencedData[] mObjects;
		private AssetReferencedData[] mFilteredObjects;

		private FilterObject mSearch = new FilterObject();

		private void SetObjects(AssetReferencedData[] objects) {
			mObjects = objects;
			FilterObjects();
		}

		protected void FilterObjects() {
			if (mObjects == null) { return; }
			int included = EditorPrefs.GetInt("asset_usages_included", 0);
			bool ignoreScripts = EditorPrefs.GetBool("asset_usage_ignore_scripts", false);
			Dictionary<FilterObject.eTypes, bool> typeFlags = new Dictionary<FilterObject.eTypes, bool>();
			List<FilterObject.eTypes> restrictTypes = new List<FilterObject.eTypes>();
			mSearch.GetRestrictTypes(restrictTypes);
			List<AssetReferencedData> ards = new List<AssetReferencedData>();
			foreach (AssetReferencedData ard in mObjects) {
				if (included == 1 && !ard.IsIncluded()) { continue; }
				if (included == 2 && ard.IsIncluded()) { continue; }
				if (ignoreScripts && ard.asset is MonoScript) { continue; }
				bool pass = false;
				foreach (SubAssetData asset in ard.subAssets) {
					if (mSearch.Filter(asset.asset, true)) {
						pass = true;
						break;
					}
				}
				if (pass) {
					typeFlags.Clear();
					foreach (FilterObject.eTypes et in restrictTypes) {
						typeFlags.Add(et, false);
					}
					foreach (AssetReferencedData user in ard.usages) {
						FilterObject.eTypes et = mSearch.GetObjectType(user.asset);
						bool val;
						if (!typeFlags.TryGetValue(et, out val) || val) { continue; }
						if (!mSearch.Filter(user.asset, false)) { continue; }
						typeFlags.Remove(et);
						typeFlags.Add(et, true);
					}
					foreach (KeyValuePair<FilterObject.eTypes, bool> kv in typeFlags) {
						pass &= kv.Value;
					}
				}
				if (pass) { ards.Add(ard); }
			}
			mFilteredObjects = ards.ToArray();
			int lines = 0;
			foreach (AssetReferencedData ard in mFilteredObjects) {
				ard.from = lines;
				lines += Mathf.Max(ard.usages.Count, ard.subAssets.Count);
			}
		}

		void OnGUI() {
			InitGUI();
			EditorGUILayout.BeginHorizontal(GUILayout.Width(1040f), min_height);
			GUILayout.Label("Filter : ", EditorStyles.miniLabel, width_filter);
			string search = GUILayout.TextField(mSearch.FilterString, style_toolbar_search_text);
			if (GUILayout.Button("", style_toolbar_search_cancel)) {
				search = "";
			}
			if (search != mSearch.FilterString) {
				mSearch.FilterString = search;
				FilterObjects();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Asset", style_toolbar_button, width_asset);
			GUILayout.Label("Included", style_toolbar_button, width_included);
			GUILayout.Label("Sub Assets", style_toolbar_button, width_sub_assets);
			GUILayout.Label("Component", style_toolbar_button, width_comment);
			GUILayout.Label("User", style_toolbar_button, width_user);
			EditorGUILayout.EndHorizontal();
			mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false);
			const float item_height = 20f;
			int from = Mathf.FloorToInt(mScroll.y / item_height);
			int end = Mathf.CeilToInt(position.height / item_height) + from;
			int len = mFilteredObjects == null ? 0 : mFilteredObjects.Length;
			for (int i = 0; i < len; i++) {
				AssetReferencedData ard = mFilteredObjects[i];
				int ls = Mathf.Max(ard.usages.Count, ard.subAssets.Count);
				if (ard.from + ls < from || ard.from > end) {
					GUILayout.Space(ls * item_height);
					continue;
				}
				GUILayout.BeginHorizontal(min_height);
				// asset
				GUILayout.BeginVertical(style_cn_box, width_asset);
				float spacels = item_height * (ls - 1) * 0.5f;
				GUILayout.Space(spacels + 2f);
				EditorGUILayout.ObjectField(ard.asset, typeof(Object), false);
				GUILayout.Space(spacels);
				GUILayout.EndVertical();
				// included
				GUILayout.BeginVertical(style_cn_box, width_included);
				GUILayout.Space(spacels + 2f);
				bool included = ard.IsIncluded();
				Color cachedColor = GUI.color;
				if (!included) {
					GUI.color = Color.red;
				}
				EditorGUILayout.LabelField(included ? "YES" : "NO", min_width);
				GUI.color = cachedColor;
				GUILayout.Space(spacels);
				GUILayout.EndVertical();
				// sub assets
				float spacesa = item_height * (ls - ard.subAssets.Count) * 0.5f / ard.subAssets.Count;
				GUILayout.BeginVertical(min_height, width_sub_assets);
				foreach (SubAssetData sa in ard.subAssets) {
					GUILayout.BeginVertical(style_cn_box);
					GUILayout.Space(spacesa + 2f);
					EditorGUILayout.ObjectField(sa.asset, typeof(Object), false);
					GUILayout.Space(spacesa);
					GUILayout.EndVertical();
				}
				GUILayout.EndVertical();
				// commonet
				GUILayout.BeginVertical(min_height, width_comment);
				foreach (SubAssetData sa in ard.subAssets) {
					GUILayout.BeginVertical(style_cn_box);
					GUILayout.Space(spacesa + 2f);
					cachedColor = GUI.color;
					if (sa.commentWarning) {
						GUI.color = new Color(1f, 0.5f, 0f, 1f);
					}
					EditorGUILayout.LabelField(sa.comment, sa.commentBold ?
						EditorStyles.miniBoldLabel : EditorStyles.miniLabel, min_width);
					GUI.color = cachedColor;
					GUILayout.Space(spacesa);
					GUILayout.EndVertical();
				}
				GUILayout.EndVertical();
				// user
				float spaceur = item_height * (ls - ard.usages.Count);
				if (ard.usages.Count > 0) {
					spaceur = spaceur * 0.5f / ard.usages.Count;
					GUILayout.BeginVertical(width_user);
					foreach (AssetReferencedData user in ard.usages) {
						GUILayout.BeginVertical(style_cn_box);
						GUILayout.Space(spaceur + 2f);
						GUILayout.BeginHorizontal();
						EditorGUILayout.ObjectField(user.asset, typeof(Object), false, width_user_object);
						if (GUILayout.Button(user.path, EditorStyles.label, min_width)) {
							EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(user.path));
						}
						GUILayout.EndHorizontal();
						GUILayout.Space(spaceur);
						GUILayout.EndVertical();
					}
					GUILayout.EndVertical();
				} else {
					GUILayout.BeginVertical(style_cn_box, width_user);
					GUILayout.Space(spaceur);
					GUILayout.EndVertical();
				}
				GUILayout.EndHorizontal();
			}
			EditorGUILayout.EndScrollView();
		}

		void InitGUI() {
			if (gui_inited) { return; }
			gui_inited = true;
			style_toolbar_search_text = "ToolbarSeachTextField";
			style_toolbar_search_cancel = "ToolbarSeachCancelButton";
			style_toolbar_button = "ToolbarButton";
			style_cn_box = "CN Box";
			min_width = GUILayout.MinWidth(8f);
			min_height = GUILayout.MinHeight(8f);
			width_filter = GUILayout.Width(36f);
			float wAsset = 160f;
			float wIncluded = 50f;
			float wSubAssets = 160f;
			float wComment = 160f;
			float wUser = 510f;
			width_asset = GUILayout.Width(wAsset);
			width_included = GUILayout.Width(wIncluded);
			width_sub_assets = GUILayout.Width(wSubAssets);
			width_comment = GUILayout.Width(wComment);
			width_user = GUILayout.Width(wUser);
			width_user_object = GUILayout.Width(160f);
		}

		public void AddItemsToMenu(GenericMenu menu) {
			int included = EditorPrefs.GetInt("asset_usages_included", 0);
			bool ignoreScripts = EditorPrefs.GetBool("asset_usage_ignore_scripts", false);
			menu.AddItem(new GUIContent("Included And Unincluded"), included == 0, OnIncludedFilterChange, 0);
			menu.AddItem(new GUIContent("Included Only"), included == 1, OnIncludedFilterChange, 1);
			menu.AddItem(new GUIContent("Unincluded Only"), included == 2, OnIncludedFilterChange, 2);
			menu.AddSeparator("");
			menu.AddItem(new GUIContent("Ignore Scripts"), ignoreScripts, OnIgnoreScriptChanged);
		}

		private void OnIncludedFilterChange(object obj) {
			EditorPrefs.SetInt("asset_usages_included", (int)obj);
			FilterObjects();
		}

		private void OnIgnoreScriptChanged() {
			bool ignoreScripts = EditorPrefs.GetBool("asset_usage_ignore_scripts", false);
			EditorPrefs.SetBool("asset_usage_ignore_scripts", !ignoreScripts);
			FilterObjects();
		}

	}

}
