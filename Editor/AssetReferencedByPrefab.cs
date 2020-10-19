using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreatClock.Common.AssetAnalyser {

	public class AssetReferencedByPrefab : AssetDependenciesCollecter {

		[MenuItem("GreatClock/Asset Analyser/Dependencies/Collect Assets Referenced By Prefab")]
		static void CollectReferences() {
			PrefabReferenceData[] prds;
			AssetReferencedData[] ards;
			CollectDependenciesInPrefab(out prds, out ards);

			AssetReferencedByPrefab win = GetWindow<AssetReferencedByPrefab>(false, "Assets Referenced By Prefab");
			win.SetObjects(ards);
			win.minSize = new Vector2(980f, 300f);
			win.maxSize = new Vector2(980f, 4000f);
			win.Show();
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
		private static GUILayoutOption width_user;
		private static GUILayoutOption width_node;
		private static GUILayoutOption width_component;
		private static GUILayoutOption width_property;
		private static GUILayoutOption width_dependency;

		private Vector2 mScroll;

		private AssetReferencedData[] mObjects;
		private AssetReferencedData[] mFilteredObjects;

		private FilterObject mSearch = new FilterObject();

		private void SetObjects(AssetReferencedData[] objects) {
			mObjects = objects;
			FilterObjects();
		}

		private void FilterObjects() {
			if (mObjects == null) { return; }
			List<AssetReferencedData> ards = new List<AssetReferencedData>();
			foreach (AssetReferencedData ard in mObjects) {
				if (ard.referenced.Count <= 0) { continue; }
				bool pass = false;
				foreach (Object asset in ard.assets) {
					if (mSearch.Filter(asset, true)) {
						pass = true;
						break;
					}
				}
				if (pass) { ards.Add(ard); }
			}
			mFilteredObjects = ards.ToArray();
			int lines = 0;
			foreach (AssetReferencedData ard in mFilteredObjects) {
				ard.from = lines;
				foreach (ReferencedData rdd in ard.referenced) {
					foreach (ReferenceData rd in rdd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							lines += rc.properties.Count;
						}
					}
				}
				if (ard.referenced.Count <= 0) { lines++; }
			}
		}

		void OnGUI() {
			InitGUI();
			EditorGUILayout.BeginHorizontal(GUILayout.Width(960f), min_height);
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
			GUILayout.Label("User", style_toolbar_button, width_user);
			GUILayout.Label("Node", style_toolbar_button, width_node);
			GUILayout.Label("Component", style_toolbar_button, width_component);
			GUILayout.Label("Property", style_toolbar_button, width_property);
			GUILayout.Label("Denpendency", style_toolbar_button, width_dependency);
			EditorGUILayout.EndHorizontal();
			mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false);
			const float item_height = 20f;
			int from = Mathf.FloorToInt(mScroll.y / item_height);
			int end = Mathf.CeilToInt(position.height / item_height) + from;
			int len = mFilteredObjects == null ? 0 : mFilteredObjects.Length;
			for (int i = 0; i < len; i++) {
				AssetReferencedData ard = mFilteredObjects[i];
				int ls = 0;
				foreach (ReferencedData rdd in ard.referenced) {
					foreach (ReferenceData rd in rdd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							ls += rc.properties.Count;
						}
					}
				}
				if (ls <= 0) { ls = 1; }
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
				if (ard.referenced.Count > 0) {
					int np = 0;
					// user
					np = 0;
					GUILayout.BeginVertical(width_user);
					foreach (ReferencedData rdd in ard.referenced) {
						int ps = 0;
						foreach (ReferenceData rd in rdd.references) {
							foreach (ReferenceComponent rc in rd.components) {
								ps += rc.properties.Count;
							}
						}
						if (ard.from + np >= from && ard.from + np + ps <= end) {
							GUILayout.BeginVertical(style_cn_box);
							float space = item_height * (ps - 1) * 0.5f;
							GUILayout.Space(space + 2f);
							EditorGUILayout.ObjectField(rdd.mainObject, typeof(Object), false);
							GUILayout.Space(space);
							GUILayout.EndVertical();
						} else {
							GUILayout.Space(item_height * ps);
						}
						np += ps;
					}
					GUILayout.EndVertical();
					// node
					np = 0;
					GUILayout.BeginVertical(width_node);
					foreach (ReferencedData rdd in ard.referenced) {
						foreach (ReferenceData rd in rdd.references) {
							int ps = 0;
							foreach (ReferenceComponent rc in rd.components) {
								ps += rc.properties.Count;
							}
							if (ard.from + np >= from && ard.from + np + ps <= end) {
								GUILayout.BeginVertical(style_cn_box);
								float space = item_height * (ps - 1) * 0.5f;
								GUILayout.Space(space + 2f);
								EditorGUILayout.ObjectField(rd.node, typeof(Object), false);
								GUILayout.Space(space);
								GUILayout.EndVertical();
							} else {
								GUILayout.Space(item_height * ps);
							}
							np += ps;
						}
					}
					GUILayout.EndVertical();
					// component
					np = 0;
					GUILayout.BeginVertical(width_component);
					foreach (ReferencedData rdd in ard.referenced) {
						foreach (ReferenceData rd in rdd.references) {
							foreach (ReferenceComponent rc in rd.components) {
								int ps = rc.properties.Count;
								if (ard.from + np >= from && ard.from + np + ps <= end) {
									GUILayout.BeginVertical(style_cn_box);
									float space = item_height * (ps - 1) * 0.5f;
									GUILayout.Space(space + 2f);
									EditorGUILayout.ObjectField(rc.component, typeof(Component), false);
									GUILayout.Space(space);
									GUILayout.EndVertical();
								} else {
									GUILayout.Space(item_height * ps);
								}
								np += ps;
							}
						}
					}
					GUILayout.EndVertical();
					// property
					np = 0;
					GUILayout.BeginVertical(width_property);
					foreach (ReferencedData rdd in ard.referenced) {
						foreach (ReferenceData rd in rdd.references) {
							foreach (ReferenceComponent rc in rd.components) {
								if (ard.from + np < from || ard.from + np + rc.properties.Count > end) {
									np += rc.properties.Count;
									GUILayout.Space(item_height * rc.properties.Count);
									continue;
								}
								foreach (ReferenceProperty rp in rc.properties) {
									if (ard.from + np >= from && ard.from + np <= end) {
										EditorGUILayout.BeginHorizontal(style_cn_box);
										EditorGUILayout.LabelField(rp.propertyPath, min_width);
										EditorGUILayout.EndHorizontal();
									} else {
										GUILayout.Space(item_height);
									}
									np++;
								}
							}
						}
					}
					GUILayout.EndVertical();
					// dependency
					np = 0;
					GUILayout.BeginVertical(width_dependency);
					foreach (ReferencedData rdd in ard.referenced) {
						foreach (ReferenceData rd in rdd.references) {
							foreach (ReferenceComponent rc in rd.components) {
								if (ard.from + np < from || ard.from + np + rc.properties.Count > end) {
									np += rc.properties.Count;
									GUILayout.Space(item_height * rc.properties.Count);
									continue;
								}
								foreach (ReferenceProperty rp in rc.properties) {
									if (ard.from + np >= from && ard.from + np <= end) {
										EditorGUILayout.BeginHorizontal(style_cn_box);
										EditorGUILayout.ObjectField(rp.asset, typeof(Object), false);
										EditorGUILayout.EndHorizontal();
									} else {
										GUILayout.Space(item_height);
									}
									np++;
								}
							}
						}
					}
					GUILayout.EndVertical();
				} else {
					GUILayout.BeginVertical(style_cn_box, width_user);
					EditorGUILayout.LabelField("  ", min_width);
					GUILayout.EndVertical();
					GUILayout.BeginVertical(style_cn_box, width_node);
					EditorGUILayout.LabelField("  ", min_width);
					GUILayout.EndVertical();
					GUILayout.BeginVertical(style_cn_box, width_component);
					EditorGUILayout.LabelField("  ", min_width);
					GUILayout.EndVertical();
					GUILayout.BeginVertical(style_cn_box, width_property);
					EditorGUILayout.LabelField("  ", min_width);
					GUILayout.EndVertical();
					GUILayout.BeginVertical(style_cn_box, width_dependency);
					EditorGUILayout.LabelField("  ", min_width);
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
			float wUser = 160f;
			float wNode = 160f;
			float wComponent = 160f;
			float wProperty = 160f;
			float wDependency = 160f;
			width_asset = GUILayout.Width(wAsset);
			width_user = GUILayout.Width(wUser);
			width_node = GUILayout.Width(wNode);
			width_component = GUILayout.Width(wComponent);
			width_property = GUILayout.Width(wProperty);
			width_dependency = GUILayout.Width(wDependency);
		}
	}

}