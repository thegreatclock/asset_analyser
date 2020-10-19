using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreatClock.Common.AssetAnalyser {

	public class PrefabReferences : AssetDependenciesCollecter {

		[MenuItem("GreatClock/Asset Analyser/Dependencies/Collect Prefab References")]
		static void CollectReferences() {
			PrefabReferenceData[] prds;
			AssetReferencedData[] ards;
			CollectDependenciesInPrefab(out prds, out ards);

			PrefabReferences win = GetWindow<PrefabReferences>(false, "Prefab References");
			win.SetObjects(prds);
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
		private static GUILayoutOption width_node;
		private static GUILayoutOption width_component;
		private static GUILayoutOption width_property;
		private static GUILayoutOption width_dependency;
		private static GUILayoutOption width_comment;

		private Vector2 mScroll;

		private PrefabReferenceData[] mObjects;
		private PrefabReferenceData[] mFilteredObjects;

		private FilterObject mSearch = new FilterObject();

		private void SetObjects(PrefabReferenceData[] objects) {
			mObjects = objects;
			FilterObjects();
		}

		private void FilterObjects() {
			if (mObjects == null) { return; }
			Dictionary<FilterObject.eTypes, bool> typeFlags = new Dictionary<FilterObject.eTypes, bool>();
			List<FilterObject.eTypes> restrictTypes = new List<FilterObject.eTypes>();
			mSearch.GetRestrictTypes(restrictTypes);
			List<PrefabReferenceData> prds = new List<PrefabReferenceData>();
			foreach (PrefabReferenceData prd in mObjects) {
				bool pass = mSearch.Filter(prd.asset, true);
				if (pass) {
					typeFlags.Clear();
					foreach (FilterObject.eTypes et in restrictTypes) {
						typeFlags.Add(et, false);
					}
					foreach (ReferenceData rd in prd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							foreach (ReferenceProperty rp in rc.properties) {
								FilterObject.eTypes et = mSearch.GetObjectType(rp.asset);
								bool val;
								if (!typeFlags.TryGetValue(et, out val) || val) { continue; }
								if (!mSearch.Filter(rp.asset, false)) { continue; }
								typeFlags.Remove(et);
								typeFlags.Add(et, true);
							}
						}
					}
					foreach (KeyValuePair<FilterObject.eTypes, bool> kv in typeFlags) {
						pass &= kv.Value;
					}
				}
				if (pass) { prds.Add(prd); }
			}
			mFilteredObjects = prds.ToArray();
			int lines = 0;
			foreach (PrefabReferenceData prd in mFilteredObjects) {
				prd.from = lines;
				foreach (ReferenceData rd in prd.references) {
					foreach (ReferenceComponent rc in rd.components) {
						lines += rc.properties.Count;
					}
				}
				if (prd.references.Count <= 0) { lines++; }
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
			GUILayout.Label("Node", style_toolbar_button, width_node);
			GUILayout.Label("Component", style_toolbar_button, width_component);
			GUILayout.Label("Property", style_toolbar_button, width_property);
			GUILayout.Label("Denpendency", style_toolbar_button, width_dependency);
			GUILayout.Label("Comment", style_toolbar_button, width_comment);
			EditorGUILayout.EndHorizontal();
			mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false);
			const float item_height = 20f;
			int from = Mathf.FloorToInt(mScroll.y / item_height);
			int end = Mathf.CeilToInt(position.height / item_height) + from;
			int len = mFilteredObjects == null ? 0 : mFilteredObjects.Length;
			for (int i = 0; i < len; i++) {
				PrefabReferenceData prd = mFilteredObjects[i];
				int ls = 0;
				foreach (ReferenceData rd in prd.references) {
					foreach (ReferenceComponent rc in rd.components) {
						ls += rc.properties.Count;
					}
				}
				if (ls <= 0) { ls = 1; }
				if (prd.from + ls < from || prd.from > end) {
					GUILayout.Space(ls * item_height);
					continue;
				}
				GUILayout.BeginHorizontal(min_height);
				// asset
				GUILayout.BeginVertical(style_cn_box, width_asset);
				float spacels = item_height * (ls - 1) * 0.5f;
				GUILayout.Space(spacels + 2f);
				EditorGUILayout.ObjectField(prd.asset, typeof(Object), false);
				GUILayout.Space(spacels);
				GUILayout.EndVertical();
				if (prd.references.Count > 0) {
					// node
					GUILayout.BeginVertical(width_node);
					foreach (ReferenceData rd in prd.references) {
						int ps = 0;
						foreach (ReferenceComponent rc in rd.components) {
							ps += rc.properties.Count;
						}
						GUILayout.BeginVertical(style_cn_box);
						float space = item_height * (ps - 1) * 0.5f;
						GUILayout.Space(space + 2f);
						EditorGUILayout.ObjectField(rd.node, typeof(Object), false);
						GUILayout.Space(space);
						GUILayout.EndVertical();
					}
					GUILayout.EndVertical();
					// component
					GUILayout.BeginVertical(width_component);
					foreach (ReferenceData rd in prd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							int ps = rc.properties.Count;
							GUILayout.BeginVertical(style_cn_box);
							float space = item_height * (ps - 1) * 0.5f;
							GUILayout.Space(space + 2f);
							EditorGUILayout.ObjectField(rc.component, typeof(Component), false);
							GUILayout.Space(space);
							GUILayout.EndVertical();
						}
					}
					GUILayout.EndVertical();
					// property
					GUILayout.BeginVertical(width_property);
					foreach (ReferenceData rd in prd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							foreach (ReferenceProperty rp in rc.properties) {
								EditorGUILayout.BeginHorizontal(style_cn_box);
								EditorGUILayout.LabelField(rp.propertyPath, min_width);
								EditorGUILayout.EndHorizontal();
							}
						}
					}
					GUILayout.EndVertical();
					// dependency
					GUILayout.BeginVertical(width_dependency);
					foreach (ReferenceData rd in prd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							foreach (ReferenceProperty rp in rc.properties) {
								EditorGUILayout.BeginHorizontal(style_cn_box);
								EditorGUILayout.ObjectField(rp.asset, typeof(Object), false);
								EditorGUILayout.EndHorizontal();
							}
						}
					}
					GUILayout.EndVertical();
					// comment
					GUILayout.BeginVertical(width_comment);
					foreach (ReferenceData rd in prd.references) {
						foreach (ReferenceComponent rc in rd.components) {
							foreach (ReferenceProperty rp in rc.properties) {
								EditorGUILayout.BeginHorizontal(style_cn_box);
								Color cachedColor = GUI.color;
								if (rp.commentWarning) {
									GUI.color = new Color(1f, 0.5f, 0f, 1f);
								}
								EditorGUILayout.LabelField(rp.comment, rp.commentBold ?
									EditorStyles.miniBoldLabel : EditorStyles.miniLabel, min_width);
								GUI.color = cachedColor;
								EditorGUILayout.EndHorizontal();
							}
						}
					}
					GUILayout.EndVertical();
				} else {
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
					GUILayout.BeginVertical(style_cn_box, width_comment);
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
			float wNode = 160f;
			float wComponent = 160f;
			float wProperty = 160f;
			float wDependency = 160f;
			float wComment = 160f;
			width_asset = GUILayout.Width(wAsset);
			width_node = GUILayout.Width(wNode);
			width_component = GUILayout.Width(wComponent);
			width_property = GUILayout.Width(wProperty);
			width_dependency = GUILayout.Width(wDependency);
			width_comment = GUILayout.Width(wComment);
		}

	}

}